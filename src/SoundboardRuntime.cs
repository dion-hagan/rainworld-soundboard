using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SoundboardMod
{
    /// <summary>
    /// Who should hear a sound. World events happen somewhere in the level and
    /// are heard only if that room is on screen. Player events are about you,
    /// so if the camera is showing a different room (it lags a moment behind
    /// you when you move between rooms, or you've left before a delayed sound
    /// plays) they're still played, in whatever room the camera is showing.
    /// </summary>
    public enum SoundScope
    {
        World,
        Player,
    }

    /// <summary>
    /// Owns the loaded soundboard.yaml and turns "event X just happened" into
    /// playing the right sounds: picks the next entry in that event's
    /// rotation, applies volume, and holds back sounds that have a delay.
    /// </summary>
    public static class SoundboardRuntime
    {
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("SoundboardMod");

        // Types in the game's creature list that aren't creatures anyone can meet.
        private static readonly HashSet<string> NotRealCreatures = new HashSet<string> { "StandardGroundCreature", "LizardTemplate", "Slugcat" };

        private static readonly Dictionary<string, EventBinding> ByEvent = new Dictionary<string, EventBinding>();

        // Which choice played last for each event, so the next one is picked in
        // turn. In memory only: every launch starts each rotation at the top.
        private static readonly Dictionary<string, string> LastChoice = new Dictionary<string, string>();

        private static readonly List<ConfigIssue> IssueList = new List<ConfigIssue>();

        private static readonly DelayQueue Delays = new DelayQueue(e => Log.LogError($"A delayed sound failed: {e}"));
        private static RainWorldGame delaysBelongTo;

        public static ConfigLocations Locations { get; private set; }
        public static EventCatalog Catalog { get; private set; }
        public static SoundboardConfig Config { get; private set; } = new SoundboardConfig();

        /// <summary>The soundboard.yaml actually in use (the player's copy, or the bundled default).</summary>
        public static string ConfigPath { get; private set; }

        public static SoundboardSettings Settings => Config.Settings;

        /// <summary>Everything wrong with the setup, for the options menu: config problems, missing/broken audio files, and so on.</summary>
        public static IReadOnlyList<ConfigIssue> Issues => IssueList;

        public static void Initialize(MonoBehaviour host)
        {
            Catalog = new EventCatalog(CreatureTypeNames());

            string pluginsDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string modRoot = Directory.GetParent(pluginsDir).FullName;
            Locations = new ConfigLocations(modRoot, Path.Combine(Application.persistentDataPath, "Soundboard"));

            var notes = new List<ConfigIssue>();

            if (Locations.TryEnsureUserFiles(out string folderProblem))
            {
                try
                {
                    ConfigLocations.WriteIfChanged(Path.Combine(Locations.UserFolder, ConfigLocations.EventListFileName), Catalog.DescribeAll());
                }
                catch (Exception e)
                {
                    Log.LogWarning($"Couldn't write {ConfigLocations.EventListFileName}: {e.Message}");
                }
            }
            else
            {
                Log.LogWarning($"Couldn't set up {Locations.UserFolder}: {folderProblem}. Using the files that came with the mod.");
                notes.Add(new ConfigIssue { Severity = IssueSeverity.Warning, Message = "Couldn't create your personal soundboard folder (" + folderProblem + "), so the config that came with the mod is being used and changes can't be saved there." });
            }

            SoundRegistry.Initialize(host);
            SoundRegistry.ClipFailed += OnClipFailed;

            Apply(Load(useDefaultsIfBroken: true, notes: notes, path: out string path), path, notes);
        }

        /// <summary>
        /// Re-reads soundboard.yaml without restarting the game. If the file
        /// has a syntax error the previous settings stay in force.
        /// </summary>
        public static string Reload()
        {
            var notes = new List<ConfigIssue>();
            SoundboardConfig loaded = Load(useDefaultsIfBroken: false, notes: notes, path: out string path);

            if (loaded.Failed)
            {
                IssueList.Clear();
                IssueList.AddRange(notes);
                IssueList.AddRange(loaded.Issues);
                IssueList.Add(new ConfigIssue { Severity = IssueSeverity.Warning, Message = "Nothing was changed - the previous settings are still in use. Fix the error above and reload again." });
                foreach (ConfigIssue issue in loaded.Issues)
                {
                    Log.LogError($"soundboard.yaml: {issue}");
                }

                return "Reload failed - the file has an error (see below). Nothing was changed.";
            }

            Apply(loaded, path, notes);
            int problems = IssueList.Count;
            return $"Reloaded {Config.SoundCount} sound(s) for {Config.Events.Count} event(s)" + (problems > 0 ? $" - {problems} problem(s), see below." : ".");
        }

        private static SoundboardConfig Load(bool useDefaultsIfBroken, List<ConfigIssue> notes, out string path)
        {
            string text = null;
            path = Locations.UserConfigPath;
            try
            {
                text = Locations.ReadConfig(out path);
            }
            catch (Exception e)
            {
                Log.LogError($"Couldn't read {path}: {e.Message}");
                notes.Add(new ConfigIssue { Severity = IssueSeverity.Error, Message = "Couldn't read " + path + ": " + e.Message });
            }

            SoundboardConfig config = text == null ? FailedConfig() : SoundboardConfigParser.Parse(text, Catalog);

            if (config.Failed && useDefaultsIfBroken)
            {
                // A typo shouldn't leave a player with a silent game, so fall
                // back to the defaults that shipped - and say so loudly.
                notes.AddRange(config.Issues);
                notes.Add(new ConfigIssue { Severity = IssueSeverity.Warning, Message = "Because of that error, the default soundboard.yaml that came with the mod is being used instead. Fix the error and press RELOAD CONFIG." });

                try
                {
                    path = Locations.BundledConfigPath;
                    config = SoundboardConfigParser.Parse(Locations.ReadBundledConfig(), Catalog);
                }
                catch (Exception e)
                {
                    Log.LogError($"Couldn't read the bundled config: {e.Message}");
                    config = FailedConfig();
                }
            }

            if (!config.Failed)
            {
                SoundFileResolver.Resolve(config, Locations.SoundFolders);
            }

            return config;
        }

        private static SoundboardConfig FailedConfig()
        {
            return new SoundboardConfig { Failed = true };
        }

        private static void Apply(SoundboardConfig config, string path, List<ConfigIssue> notes)
        {
            Config = config;
            ConfigPath = path;

            ByEvent.Clear();
            foreach (EventBinding binding in config.Events)
            {
                ByEvent[NameMatch.Normalize(binding.EventName)] = binding;
            }

            LastChoice.Clear();
            Delays.Clear();

            SoundRegistry.Sync(config.Events.SelectMany(e => e.Choices).SelectMany(c => c.Sounds).Select(s => s.ResolvedPath).Distinct());
            Options.Instance?.BindChoices(config);

            IssueList.Clear();
            IssueList.AddRange(notes);
            IssueList.AddRange(config.Issues);
            if (!SoundRegistry.Available)
            {
                IssueList.Add(new ConfigIssue { Severity = IssueSeverity.Error, Message = "Custom sounds can't play: " + SoundRegistry.UnavailableReason + ". The mod needs an update for this game version." });
            }

            Log.LogInfo($"Loaded {config.SoundCount} sound(s) for {config.Events.Count} event(s) from {path}");
            foreach (ConfigIssue issue in IssueList)
            {
                string line = "soundboard.yaml: " + issue;
                if (issue.Severity == IssueSeverity.Error)
                {
                    Log.LogError(line);
                }
                else
                {
                    Log.LogWarning(line);
                }
            }
        }

        private static void OnClipFailed(string path, string reason)
        {
            IssueList.Add(new ConfigIssue { Severity = IssueSeverity.Error, Message = "Couldn't load '" + Path.GetFileName(path) + "' - " + reason + ". Try re-saving it as a standard .wav or .ogg." });
        }

        private static IEnumerable<string> CreatureTypeNames()
        {
            ExtEnumType values = ExtEnum<CreatureTemplate.Type>.values;
            if (values == null)
            {
                yield break;
            }

            for (int i = 0; i < values.Count; i++)
            {
                string name = values.GetEntry(i);
                if (!string.IsNullOrEmpty(name) && !NotRealCreatures.Contains(name))
                {
                    yield return name;
                }
            }
        }

        // --- playing -------------------------------------------------------------

        public static bool IsEnabled(SoundChoice choice)
        {
            return Options.Instance == null || Options.Instance.IsEnabled(choice.Id, choice.DefaultEnabled);
        }

        /// <summary>
        /// An event just happened. Plays the next enabled entry from its list
        /// (if the config has one). pos is where it happened in the room, or
        /// null for a sound with no position (played centred).
        /// </summary>
        public static void Fire(string eventKey, Room room, Vector2? pos, SoundScope scope)
        {
            if (room == null)
            {
                return;
            }

            string normalized = NameMatch.Normalize(eventKey);
            if (!ByEvent.TryGetValue(normalized, out EventBinding binding))
            {
                if (Settings.Debug)
                {
                    Log.LogInfo($"[event] {eventKey} - no sounds set up for it");
                }

                return;
            }

            LastChoice.TryGetValue(normalized, out string lastId);
            string nextId = SoundRotation.NextKey(binding.Choices.Select(c => c.Id).ToList(), id => IsEnabled(binding.Choices.First(c => c.Id == id)), lastId);
            if (nextId == null)
            {
                if (Settings.Debug)
                {
                    Log.LogInfo($"[event] {eventKey} - every sound for it is switched off");
                }

                return;
            }

            LastChoice[normalized] = nextId;
            SoundChoice choice = binding.Choices.First(c => c.Id == nextId);

            if (Settings.Debug)
            {
                Log.LogInfo($"[event] {eventKey} -> {choice.Label}");
            }

            foreach (SoundRef sound in choice.Sounds)
            {
                SoundID id = SoundRegistry.GetId(sound.ResolvedPath);
                if (id == null)
                {
                    continue;
                }

                float volume = sound.Volume;
                int ticks = DelayQueue.SecondsToTicks(sound.Delay);
                if (ticks == 0)
                {
                    PlayNow(id, volume, room, pos, scope);
                }
                else
                {
                    Delays.Add(ticks, () => PlayNow(id, volume, room, pos, scope));
                }
            }
        }

        private static void PlayNow(SoundID id, float volume, Room room, Vector2? pos, SoundScope scope)
        {
            RainWorldGame game = room.game;
            if (game?.cameras == null)
            {
                return;
            }

            Room target = room;
            bool positional = pos.HasValue;

            if (scope == SoundScope.Player && !game.cameras.Any(c => c.room == room))
            {
                target = game.cameras.Select(c => c.room).FirstOrDefault(r => r != null);
                positional = false;
            }

            if (target == null)
            {
                return;
            }

            if (positional)
            {
                target.PlaySound(id, pos.Value, volume, 1f);
            }
            else
            {
                target.PlaySound(id, 0f, volume, 1f);
            }
        }

        /// <summary>Called once per game tick to release sounds whose delay has run out.</summary>
        public static void Tick(RainWorldGame game)
        {
            if (!ReferenceEquals(game, delaysBelongTo))
            {
                // A different game session (or back at the menu): anything still waiting belongs to the old one.
                Delays.Clear();
                delaysBelongTo = game;
            }

            Delays.Tick();
        }

        // Runs after each game tick; the game isn't ticking while paused, so delays don't run out behind the pause menu.
        [HarmonyPatch(typeof(RainWorldGame), nameof(RainWorldGame.Update))]
        private static class RainWorldGame_Update_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(RainWorldGame __instance)
            {
                Tick(__instance);
            }
        }
    }
}
