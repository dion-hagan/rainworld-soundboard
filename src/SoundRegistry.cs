using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using RWCustom;
using UnityEngine;
using UnityEngine.Networking;

namespace SoundboardMod
{
    /// <summary>
    /// Gets the audio files named in soundboard.yaml into the game as real
    /// sounds, at runtime, with no restart.
    ///
    /// The game normally learns about sounds from a data-merge step
    /// (modify/soundeffects/sounds.txt) that only runs when the player applies
    /// mods from the menu - long before any plugin code, so a plugin can't use
    /// it to add sounds for the current launch. Instead we load each file
    /// ourselves and then add it to the game's own SoundLoader tables (the
    /// same tables sounds.txt fills in), so playback goes through the normal
    /// Room.PlaySound path with all of its positional audio behaviour.
    ///
    /// This reaches into private SoundLoader fields by reflection; if a game
    /// update changes them, Available turns false, the reason is logged and
    /// shown in the options menu, and everything else keeps working.
    /// </summary>
    public static class SoundRegistry
    {
        private sealed class Entry
        {
            public string Path;
            public SoundID Id;
            public AudioClip Clip;
            public bool Failed;
            public bool Injected;

            /// <summary>File's last-modified time when it was loaded, to notice a replaced file on reload.</summary>
            public DateTime Stamp;

            /// <summary>The clip array handed to the game, so a replaced file can be swapped in place.</summary>
            public AudioClip[] GameAudio;
        }

        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("SoundboardMod");

        private static readonly FieldInfo SoundTriggersField = typeof(SoundLoader).GetField("soundTriggers", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo AllAudioField = typeof(SoundLoader).GetField("allAudio", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly Type TriggerType = typeof(SoundLoader).GetNestedType("SoundTrigger", BindingFlags.NonPublic);
        private static readonly ConstructorInfo TriggerConstructor = TriggerType?.GetConstructor(new[]
        {
            typeof(SoundID), typeof(SoundLoader.SoundPlayInstruction[]), typeof(float), typeof(SoundLoader), typeof(string[]),
        });

        private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        private static readonly Queue<Entry> LoadQueue = new Queue<Entry>();

        private static MonoBehaviour host;
        private static bool loading;

        // The exact arrays we last wrote into. If the game rebuilds its sound
        // tables (LoadSounds runs again) the fields hold different arrays, and
        // everything we added is gone and must be added again.
        private static Array injectedTriggers;
        private static SoundLoader.ClipLoadData[] injectedAudio;

        /// <summary>True if the SoundLoader looks the way we expect. If false, custom sounds can't play.</summary>
        public static bool Available { get; private set; } = true;

        public static string UnavailableReason { get; private set; }

        /// <summary>Raised (on the main thread) when an audio file can't be loaded: path, reason.</summary>
        public static event Action<string, string> ClipFailed;

        public static void Initialize(MonoBehaviour coroutineHost)
        {
            host = coroutineHost;

            if (SoundTriggersField == null || AllAudioField == null || TriggerConstructor == null)
            {
                MarkUnavailable("this version of Rain World changed how its sound system works (SoundLoader fields not found)");
            }
        }

        /// <summary>
        /// Makes sure every given file has a SoundID and is being loaded.
        /// Safe to call again with a different list (config reload); files
        /// already loaded aren't loaded twice.
        /// </summary>
        public static void Sync(IEnumerable<string> paths)
        {
            if (!Available)
            {
                return;
            }

            foreach (string path in paths)
            {
                if (Entries.TryGetValue(path, out Entry existing))
                {
                    // Retry files that failed before, and reload ones the player has replaced.
                    if (existing.Failed || (existing.Clip != null && File.GetLastWriteTimeUtc(path) != existing.Stamp))
                    {
                        existing.Failed = false;
                        LoadQueue.Enqueue(existing);
                    }

                    continue;
                }

                var entry = new Entry
                {
                    Path = path,
                    Id = new SoundID("Soundboard_" + Sanitize(System.IO.Path.GetFileNameWithoutExtension(path)) + "_" + (path.ToLowerInvariant().GetHashCode() & 0xFFFFFF).ToString("x"), true),
                };
                Entries[path] = entry;
                LoadQueue.Enqueue(entry);
            }

            if (LoadQueue.Count > 0 && !loading && host != null)
            {
                host.StartCoroutine(LoadClips());
            }
        }

        public static SoundID GetId(string path)
        {
            return Entries.TryGetValue(path, out Entry entry) ? entry.Id : null;
        }

        private static string Sanitize(string s)
        {
            var chars = s.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]))
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }

        // --- loading files -----------------------------------------------------

        // One file at a time, a frame apart: dozens of clips decode without a hitch on startup.
        private static IEnumerator LoadClips()
        {
            loading = true;
            try
            {
                while (LoadQueue.Count > 0)
                {
                    Entry entry = LoadQueue.Dequeue();
                    if (entry.Failed || (entry.Clip != null && File.GetLastWriteTimeUtc(entry.Path) == entry.Stamp))
                    {
                        continue;
                    }

                    yield return LoadClip(entry);
                    TryInjectCurrent();
                }
            }
            finally
            {
                loading = false;
            }
        }

        private static IEnumerator LoadClip(Entry entry)
        {
            AudioType type = AudioTypeFor(entry.Path);
            string error = null;
            DateTime stamp = File.GetLastWriteTimeUtc(entry.Path);

            using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(new Uri(entry.Path).AbsoluteUri, type))
            {
                yield return request.SendWebRequest();

                if (!string.IsNullOrEmpty(request.error))
                {
                    error = request.error;
                }
                else
                {
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                    if (clip == null || clip.length <= 0f)
                    {
                        error = "the file couldn't be decoded as " + type + " audio";
                    }
                    else
                    {
                        clip.name = System.IO.Path.GetFileNameWithoutExtension(entry.Path);
                        entry.Clip = clip;
                        entry.Stamp = stamp;
                        if (entry.GameAudio != null)
                        {
                            entry.GameAudio[0] = clip; // already in the game's tables: swap the audio in place
                        }
                    }
                }
            }

            if (error != null)
            {
                entry.Failed = true;
                Log.LogError($"Couldn't load sound '{entry.Path}': {error}");
                ClipFailed?.Invoke(entry.Path, error);
            }
        }

        private static AudioType AudioTypeFor(string path)
        {
            switch (System.IO.Path.GetExtension(path).ToLowerInvariant())
            {
                case ".ogg": return AudioType.OGGVORBIS;
                case ".mp3": return AudioType.MPEG;
                default: return AudioType.WAV;
            }
        }

        // --- adding sounds to the game's SoundLoader -----------------------------

        private static void MarkUnavailable(string reason)
        {
            if (!Available)
            {
                return;
            }

            Available = false;
            UnavailableReason = reason;
            Log.LogError("Custom sounds are disabled: " + reason);
        }

        private static void TryInjectCurrent()
        {
            TryInject(Custom.rainWorld?.processManager?.soundLoader);
        }

        /// <summary>
        /// Adds every loaded clip that isn't in the loader yet. Called when a
        /// clip finishes loading, and after the game (re)builds its tables.
        /// </summary>
        private static void TryInject(SoundLoader loader)
        {
            if (!Available || loader == null || !loader.assetBundlesLoaded)
            {
                return;
            }

            try
            {
                var triggers = (Array)SoundTriggersField.GetValue(loader);
                var audio = (SoundLoader.ClipLoadData[])AllAudioField.GetValue(loader);
                if (triggers == null || audio == null || loader.workingTriggers == null)
                {
                    return; // the game hasn't built its tables yet; LoadSounds' patch calls us again when it has
                }

                if (!ReferenceEquals(triggers, injectedTriggers) || !ReferenceEquals(audio, injectedAudio))
                {
                    foreach (Entry e in Entries.Values)
                    {
                        e.Injected = false;
                    }
                }

                var pending = new List<Entry>();
                foreach (Entry e in Entries.Values)
                {
                    if (e.Clip != null && !e.Injected)
                    {
                        pending.Add(e);
                    }
                }

                if (pending.Count == 0)
                {
                    injectedTriggers = triggers;
                    injectedAudio = audio;
                    return;
                }

                // SoundIDs registered after the game sized its tables need room in them.
                int idCount = ExtEnum<SoundID>.values.Count;
                if (triggers.Length < idCount)
                {
                    Array grown = Array.CreateInstance(TriggerType, idCount);
                    Array.Copy(triggers, grown, triggers.Length);
                    triggers = grown;
                    SoundTriggersField.SetValue(loader, triggers);
                }

                if (loader.workingTriggers.Length < idCount)
                {
                    bool[] grown = new bool[idCount];
                    Array.Copy(loader.workingTriggers, grown, loader.workingTriggers.Length);
                    loader.workingTriggers = grown;
                }

                int firstNew = audio.Length;
                Array.Resize(ref audio, audio.Length + pending.Count);
                AllAudioField.SetValue(loader, audio);

                for (int i = 0; i < pending.Count; i++)
                {
                    Entry e = pending[i];
                    int clipIndex = firstNew + i;
                    string name = System.IO.Path.GetFileNameWithoutExtension(e.Path);

                    e.GameAudio = new[] { e.Clip };
                    audio[clipIndex] = new SoundLoader.ClipLoadData
                    {
                        audioClipThroughUnity = false,
                        audio = e.GameAudio,
                        name = name,
                    };

                    var instructions = new[] { new SoundLoader.SoundPlayInstruction(clipIndex, new[] { name }) };
                    object trigger = TriggerConstructor.Invoke(new object[] { e.Id, instructions, 1f, loader, new[] { name } });
                    triggers.SetValue(trigger, e.Id.Index);
                    loader.workingTriggers[e.Id.Index] = true;
                    e.Injected = true;
                }

                injectedTriggers = triggers;
                injectedAudio = audio;
            }
            catch (Exception ex)
            {
                MarkUnavailable("adding sounds to the game failed: " + ex.Message);
                Log.LogError(ex);
            }
        }

        /// <summary>
        /// LoadSounds builds the game's tables from scratch, so it's the moment
        /// our sounds have to be put back. (It returns early without building
        /// anything until the asset bundles are ready; TryInject copes with that.)
        /// </summary>
        [HarmonyPatch(typeof(SoundLoader), nameof(SoundLoader.LoadSounds))]
        private static class SoundLoader_LoadSounds_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(SoundLoader __instance)
            {
                TryInject(__instance);
            }
        }
    }
}
