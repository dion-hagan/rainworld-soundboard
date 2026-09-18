using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Logging;

namespace SoundboardMod
{
    public class SoundEntry
    {
        public string id;
        public string displayName;
        public string description;
        public string file;
        public string @event;
        public bool defaultEnabled = true;

        // Optional. Sounds sharing the same event AND the same non-empty
        // group are picked/played as a single unit (see ChooseGroup in
        // EventHooks.cs): the random pick is between groups, not individual
        // sounds, and every sound in the winning group plays together.
        // Sounds with no group are their own group of one.
        public string group;

        // Populated at runtime, not read from JSON.
        public SoundID soundId;
    }

    /// <summary>
    /// Loads soundeffects/meta.json and registers a SoundID for every entry.
    /// This must run from Awake/OnEnable, before the sound system loads.
    /// </summary>
    public static class SoundboardData
    {
        public static List<SoundEntry> Sounds { get; private set; } = new List<SoundEntry>();

        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("SoundboardMod");

        private static bool initialized;

        public static void Initialize()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;

            try
            {
                LoadSounds();
            }
            catch (Exception e)
            {
                // Never let a bug here take down the rest of mod init silently.
                Log.LogError($"Unexpected error while loading sounds: {e}");
            }
        }

        private static void LoadSounds()
        {
            string metaPath = Path.Combine(GetModRoot(), "soundeffects", "meta.json");
            Log.LogInfo($"Loading sound metadata from {metaPath}");
            if (!File.Exists(metaPath))
            {
                Log.LogError($"Could not find {metaPath}. No custom sounds will be available.");
                return;
            }

            List<object> soundList;
            try
            {
                string json = File.ReadAllText(metaPath);
                var root = (Dictionary<string, object>)MiniJson.Parse(json);
                soundList = root.TryGetValue("sounds", out object rawSounds) ? (List<object>)rawSounds : null;
            }
            catch (Exception e)
            {
                Log.LogError($"Failed to parse meta.json: {e}");
                return;
            }

            if (soundList == null)
            {
                Log.LogError("meta.json parsed but its \"sounds\" array was null/missing.");
                return;
            }

            foreach (object rawEntry in soundList)
            {
                var fields = (Dictionary<string, object>)rawEntry;
                string id = GetString(fields, "id");
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                var entry = new SoundEntry
                {
                    id = id,
                    displayName = GetString(fields, "displayName"),
                    description = GetString(fields, "description"),
                    file = GetString(fields, "file"),
                    @event = GetString(fields, "event"),
                    defaultEnabled = GetBool(fields, "defaultEnabled", true),
                    group = GetString(fields, "group"),
                };

                // register: true creates (or reuses) the abstract SoundID that
                // modify/soundeffects/sounds.txt attaches an audio file to.
                entry.soundId = new SoundID(entry.id, true);
                Sounds.Add(entry);
            }

            Log.LogInfo($"Registered {Sounds.Count} custom sound(s).");
        }

        private static string GetString(Dictionary<string, object> fields, string key)
        {
            return fields.TryGetValue(key, out object value) ? value as string : null;
        }

        private static bool GetBool(Dictionary<string, object> fields, string key, bool fallback)
        {
            return fields.TryGetValue(key, out object value) && value is bool b ? b : fallback;
        }

        /// <summary>
        /// Returns the mod's root folder (the one containing modinfo.json),
        /// derived from where this assembly's .dll actually sits on disk:
        /// .../<modid>/plugins/SoundboardMod.dll -> .../<modid>/
        /// </summary>
        public static string GetModRoot()
        {
            string pluginsDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Directory.GetParent(pluginsDir).FullName;
        }
    }
}
