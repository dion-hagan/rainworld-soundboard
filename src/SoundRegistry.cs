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

            /// <summary>Why the last load failed, for the Test button to repeat.</summary>
            public string Error;

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

        /// <summary>
        /// Plays a file once, straight away, through the menu's microphone: the Test button on the
        /// Add Sound page. The file doesn't have to be in soundboard.yaml - it is loaded and added to
        /// the game's tables here the first time, and then stays there like any other sound. If the
        /// file is still loading the sound starts as soon as it's ready. At most the first
        /// <see cref="PreviewMaxSeconds"/> seconds play, so a long file can't run on after the
        /// player has left the screen. <paramref name="onProblem"/>
        /// is called (on the main thread, possibly a moment later) with a reason if it can't be played.
        ///
        /// <paramref name="volume"/> is on the same scale as a sound's volume in soundboard.yaml
        /// (1 = as recorded), handed to the microphone unchanged - exactly what Room.PlaySound hands
        /// it in-game, so the game applies the same Sound Effects setting and volume curve
        /// (SoundLoader.volumeExponent) and the level matches what the player hears from a
        /// sound that isn't positioned in the room. What a menu can't reproduce is anything the
        /// room adds: distance from the sound's source, muffling, rain drowning it out.
        /// </summary>
        public static void Preview(string path, float volume, Action<string> onProblem)
        {
            if (!Available)
            {
                onProblem("custom sounds can't play (" + UnavailableReason + ")");
                return;
            }

            if (host == null)
            {
                onProblem("the mod hasn't finished starting up");
                return;
            }

            Sync(new[] { path });
            host.StartCoroutine(PreviewWhenReady(path, volume, onProblem));
        }

        /// <summary>How long a Test press waits for clips that are still loading before giving up.</summary>
        private const float PreviewWaitSeconds = 15f;

        /// <summary>A Test never plays more than this much of a file; a shorter file plays to its end.</summary>
        private const float PreviewMaxSeconds = 10f;

        /// <summary>How much of the end of a cut-off Test is faded out.</summary>
        private const float PreviewFadeSeconds = 0.3f;

        private static IEnumerator PreviewWhenReady(string path, float volume, Action<string> onProblem)
        {
            // This file's first Test (or files queued ahead of it) may still be loading, one a frame.
            float waited = 0f;
            while ((loading || LoadQueue.Count > 0) && waited < PreviewWaitSeconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!Entries.TryGetValue(path, out Entry entry))
            {
                yield break;
            }

            if (entry.Failed)
            {
                onProblem("the file couldn't be loaded (" + entry.Error + ")");
                yield break;
            }

            // Covers the game having rebuilt its sound tables since the clip finished loading.
            TryInjectCurrent();
            if (!entry.Injected)
            {
                onProblem(Available ? "the file didn't finish loading - try again in a moment" : "custom sounds can't play (" + UnavailableReason + ")");
                yield break;
            }

            MenuMicrophone mic = Custom.rainWorld?.processManager?.menuMic;
            if (mic == null)
            {
                onProblem("sounds can only be tested from the menus");
                yield break;
            }

            int before = mic.soundObjects.Count;
            mic.PlaySound(entry.Id, 0f, volume, 1f);

            if (mic.soundObjects.Count <= before)
            {
                Log.LogWarning($"Test: the menu microphone didn't start '{entry.Path}' (sound {entry.Id}, volume {volume:0.###})");
                onProblem("the game didn't start the sound");
                yield break;
            }

            MenuMicrophone.MenuSoundObject started = mic.soundObjects[mic.soundObjects.Count - 1];
            Log.LogInfo($"Test: playing '{entry.Path}' - volume {volume:0.###}, audio source volume {started.audioSource.volume:0.####}, playing {started.audioSource.isPlaying}, clip {(started.audioSource.clip != null ? started.audioSource.clip.length.ToString("0.0#") + "s" : "none")}");

            // A song shouldn't keep playing once the player has moved on (e.g. pressed APPLY): cut it off
            // after PreviewMaxSeconds, with a short fade so it doesn't end on a click. A shorter file just plays out.
            AudioSource source = started.audioSource;
            if (source.clip == null || source.clip.length <= PreviewMaxSeconds)
            {
                yield break;
            }

            float startVolume = source.volume;
            while (!started.slatedForDeletion && source.isPlaying)
            {
                // slatedForDeletion first: once the microphone is done with the sound, its audio source goes back to a pool and may be someone else's.
                float left = PreviewMaxSeconds - source.time;
                if (left <= 0f)
                {
                    started.Stop();
                    yield break;
                }

                if (left < PreviewFadeSeconds)
                {
                    source.volume = startVolume * left / PreviewFadeSeconds;
                }

                yield return null;
            }
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
                entry.Error = error;
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
