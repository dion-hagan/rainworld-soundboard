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

        // Types in the game's creature list that don't get their own events: templates
        // nobody can meet, and the slugcat types, which are Player objects and are
        // covered by the Player* events instead (a slugpup dying is PlayerDeath).
        private static readonly HashSet<string> NotRealCreatures = new HashSet<string> { "StandardGroundCreature", "LizardTemplate", "Slugcat", "SlugNPC" };

        private static readonly Dictionary<string, EventBinding> ByEvent = new Dictionary<string, EventBinding>();

        // Which choice played last for each event, so the next one is picked in
        // turn. In memory only: every launch starts each rotation at the top.
        private static readonly Dictionary<string, string> LastChoice = new Dictionary<string, string>();

        // Entries that have played and are waiting out their "cooldown:". In memory only, and started
        // fresh for every game session.
        private static readonly EntryCooldowns Cooldowns = new EntryCooldowns();

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

            if (path == Locations.UserConfigPath && Locations.TemplateIsNewerThanUserCopy())
            {
                notes.Add(new ConfigIssue
                {
                    Severity = IssueSeverity.Warning,
                    Message = "The soundboard.yaml in the mod's own folder is just a template and has changed, but the game reads YOUR copy: " + Locations.UserConfigPath + ". Edit that one (OPEN FOLDER). To start over from the template, rename your copy and restart the game.",
                });
            }

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
            Cooldowns.Clear();

            SoundRegistry.Sync(config.Events.SelectMany(e => e.Choices).SelectMany(c => c.Sounds).Select(s => s.ResolvedPath).Distinct());
            Options.Instance?.RefreshToggles();

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
            return choice.Enabled;
        }

        /// <summary>
        /// Switches an entry on or off - what a checkbox in the options screen
        /// does. The change takes effect immediately and is written into
        /// soundboard.yaml so the file always says what the game is doing.
        /// Returns null on success, otherwise a message for the player.
        /// </summary>
        public static string SetEnabled(string choiceId, bool enabled)
        {
            return SetEnabled(new[] { new KeyValuePair<string, bool>(choiceId, enabled) });
        }

        /// <summary>Same as SetEnabled for several entries at once (ENABLE ALL / DISABLE ALL): the file is written once.</summary>
        public static string SetEnabled(IEnumerable<KeyValuePair<string, bool>> changes)
        {
            var wanted = changes.ToList();
            IEnumerable<SoundChoice> all = Config.Events.SelectMany(e => e.Choices);
            foreach (KeyValuePair<string, bool> change in wanted)
            {
                SoundChoice choice = all.FirstOrDefault(c => c.Id == change.Key);
                if (choice != null)
                {
                    choice.Enabled = change.Value;
                }
            }

            if (ConfigPath != Locations.UserConfigPath)
            {
                return "Switched for now, but not saved: the game isn't using your own soundboard.yaml right now (see the problems listed).";
            }

            try
            {
                string text = File.ReadAllText(Locations.UserConfigPath);
                foreach (KeyValuePair<string, bool> change in wanted)
                {
                    // Re-read the entry's position from the current text every time:
                    // each edit can add a line, and the player may have edited the file by hand.
                    SoundboardConfig fresh = SoundboardConfigParser.Parse(text, Catalog);
                    if (fresh.Failed)
                    {
                        return "Switched for now, but soundboard.yaml has an error (" + fresh.Issues[0] + ") so it wasn't saved. Fix it and press RELOAD CONFIG.";
                    }

                    SoundChoice entry = fresh.Events.SelectMany(e => e.Choices).FirstOrDefault(c => c.Id == change.Key);
                    if (entry == null)
                    {
                        return "Switched for now, but that entry is no longer in soundboard.yaml, so it wasn't saved. Press RELOAD CONFIG.";
                    }

                    YamlEditor.Result result = YamlEditor.SetEnabled(text, entry.Source, change.Value);
                    if (!result.Ok)
                    {
                        return "Switched for now, but not saved to soundboard.yaml: " + result.Error + ".";
                    }

                    text = result.Text;
                }

                File.WriteAllText(Locations.UserConfigPath, text);
                return null;
            }
            catch (Exception e)
            {
                Log.LogError($"Couldn't save the on/off change to {Locations.UserConfigPath}: {e}");
                return "Switched for now, but couldn't save to soundboard.yaml: " + e.Message;
            }
        }

        /// <summary>
        /// Adds a sound to an event - what SAVE does on the options screen's Add Sound
        /// page. The sound is written into the player's soundboard.yaml (at the end of the
        /// event's list, keeping the rest of the file as it was) and the config is re-read,
        /// so it works immediately. Returns null on success, otherwise a reason for the player.
        /// <paramref name="written"/> says whether the sound made it into the file: it can be
        /// true together with a reason, if only the re-read afterwards went wrong - the caller
        /// must not offer to add it again then, or it would be in the file twice.
        /// </summary>
        public static string AddSound(NewSound sound, out bool written)
        {
            written = false;
            if (Locations == null || ConfigPath != Locations.UserConfigPath)
            {
                return "the game isn't using your own soundboard.yaml right now (see the problems listed), so there's nothing to save it to";
            }

            try
            {
                string text = File.ReadAllText(Locations.UserConfigPath);
                YamlEditor.Result result = SoundAdder.Add(text, sound, Catalog);
                if (!result.Ok)
                {
                    return result.Error;
                }

                File.WriteAllText(Locations.UserConfigPath, result.Text);
                written = true;
            }
            catch (Exception e)
            {
                Log.LogError($"Couldn't add a sound to {Locations.UserConfigPath}: {e}");
                return "couldn't write soundboard.yaml (" + e.Message + ")";
            }

            string files = string.Join(", ", sound.Parts.Select(p => p.File));
            Log.LogInfo($"Added {(sound.Together ? "together group" : "sound")} [{files}] to {sound.EventName} in {Locations.UserConfigPath}");

            try
            {
                Reload();
            }
            catch (Exception e)
            {
                Log.LogError($"Added the sound, but re-reading soundboard.yaml failed: {e}");
                return "it was added to soundboard.yaml, but re-reading the file failed (" + e.Message + ") - press RELOAD CONFIG or restart the game";
            }

            return null;
        }

        /// <summary>
        /// Changes the volume, delay and cooldown of an entry that's already in the file - what SAVE
        /// does on the options screen's Edit Sound page - and re-reads the config so it takes effect
        /// at once. Returns null on success, otherwise a reason for the player.
        /// <paramref name="written"/> says whether the file was changed: false with no reason means
        /// there was nothing to change; true with a reason means only the re-read afterwards went wrong.
        /// </summary>
        public static string EditEntry(EntryTweak tweak, out bool written)
        {
            written = false;
            if (Locations == null || ConfigPath != Locations.UserConfigPath)
            {
                return "the game isn't using your own soundboard.yaml right now (see the problems listed), so there's nothing to save it to";
            }

            try
            {
                string text = File.ReadAllText(Locations.UserConfigPath);
                YamlEditor.Result result = SoundTweaker.Apply(text, tweak, Catalog);
                if (!result.Ok)
                {
                    return result.Error;
                }

                if (result.Text == text)
                {
                    return null; // already as asked
                }

                File.WriteAllText(Locations.UserConfigPath, result.Text);
                written = true;
            }
            catch (Exception e)
            {
                Log.LogError($"Couldn't change an entry in {Locations.UserConfigPath}: {e}");
                return "couldn't write soundboard.yaml (" + e.Message + ")";
            }

            Log.LogInfo($"Changed entry {tweak.ChoiceId} in {Locations.UserConfigPath}");

            try
            {
                Reload();
            }
            catch (Exception e)
            {
                Log.LogError($"Changed the entry, but re-reading soundboard.yaml failed: {e}");
                return "it was changed in soundboard.yaml, but re-reading the file failed (" + e.Message + ") - press RELOAD CONFIG or restart the game";
            }

            return null;
        }

        /// <summary>How many timestamped backups of soundboard.yaml to keep (this also matches what scripts/sync-config.ps1 keeps).</summary>
        private const int BackupsToKeep = 10;

        /// <summary>
        /// Copies the player's soundboard.yaml to "soundboard.yaml.yyyyMMdd-HHmmss.bak" in the same
        /// folder and forgets all but the newest few. Returns the copy's path.
        /// </summary>
        private static string BackUpConfig()
        {
            string source = Locations.UserConfigPath;
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            string target = source + "." + stamp + ".bak";
            for (int n = 2; File.Exists(target); n++)
            {
                target = source + "." + stamp + "-" + n + ".bak";
            }

            File.Copy(source, target);

            try
            {
                string pattern = Path.GetFileName(source) + ".*.bak";
                foreach (FileInfo old in new DirectoryInfo(Path.GetDirectoryName(source)).GetFiles(pattern).OrderByDescending(f => f.LastWriteTimeUtc).Skip(BackupsToKeep))
                {
                    old.Delete();
                }
            }
            catch (Exception e)
            {
                Log.LogWarning($"Couldn't tidy old backups of soundboard.yaml: {e.Message}");
            }

            return target;
        }

        /// <summary>
        /// Deletes an entry from the file, or some sounds of a "together" group - what SAVE does when
        /// a Delete box is ticked on the options screen's Edit Sound page. The file is backed up first
        /// (and nothing is deleted if that fails), then the config is re-read so it takes effect at once.
        /// Returns null on success, otherwise a reason for the player. <paramref name="written"/> says
        /// whether the file was changed (it can be true together with a reason, if only the re-read
        /// afterwards went wrong); <paramref name="backup"/> is the backup's path when one was made.
        /// </summary>
        public static string DeleteFromEntry(EntryDelete request, out bool written, out string backup)
        {
            written = false;
            backup = null;
            if (Locations == null || ConfigPath != Locations.UserConfigPath)
            {
                return "the game isn't using your own soundboard.yaml right now (see the problems listed), so there's nothing to delete from";
            }

            try
            {
                string text = File.ReadAllText(Locations.UserConfigPath);
                YamlEditor.Result result = SoundTweaker.Delete(text, request, Catalog);
                if (!result.Ok)
                {
                    return result.Error;
                }

                if (result.Text == text)
                {
                    return null; // nothing to delete
                }

                try
                {
                    backup = BackUpConfig();
                }
                catch (Exception e)
                {
                    Log.LogError($"Couldn't back up {Locations.UserConfigPath} before deleting: {e}");
                    return "couldn't make a backup of soundboard.yaml first (" + e.Message + "), so nothing was deleted";
                }

                File.WriteAllText(Locations.UserConfigPath, result.Text);
                written = true;
            }
            catch (Exception e)
            {
                Log.LogError($"Couldn't delete from {Locations.UserConfigPath}: {e}");
                return "couldn't write soundboard.yaml (" + e.Message + ")";
            }

            Log.LogInfo($"Deleted {(request.WholeEntry ? "entry " + request.ChoiceId : request.Sounds.Count + " sound(s) of " + request.ChoiceId)} from {Locations.UserConfigPath} (backup: {backup})");

            try
            {
                Reload();
            }
            catch (Exception e)
            {
                Log.LogError($"Deleted from the file, but re-reading soundboard.yaml failed: {e}");
                return "it was deleted from soundboard.yaml, but re-reading the file failed (" + e.Message + ") - press RELOAD CONFIG or restart the game";
            }

            return null;
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

            // An entry that's switched off, or still cooling down from its last play, is skipped
            // (keeping its place in the rotation).
            string nextId = SoundRotation.NextKey(
                binding.Choices.Select(c => c.Id).ToList(),
                id => IsEnabled(binding.Choices.First(c => c.Id == id)) && !Cooldowns.IsCoolingDown(id),
                lastId);
            if (nextId == null)
            {
                if (Settings.Debug)
                {
                    Log.LogInfo($"[event] {eventKey} - every sound for it is switched off or cooling down");
                }

                return;
            }

            LastChoice[normalized] = nextId;
            SoundChoice choice = binding.Choices.First(c => c.Id == nextId);
            Cooldowns.Start(choice.Id, choice.Cooldown);

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

        /// <summary>Called once per game tick to release sounds whose delay has run out and to run down cooldowns.</summary>
        public static void Tick(RainWorldGame game)
        {
            if (!ReferenceEquals(game, delaysBelongTo))
            {
                // A different game session (or back at the menu): anything still waiting belongs to the old one.
                Delays.Clear();
                Cooldowns.Clear();
                delaysBelongTo = game;
            }

            Delays.Tick();
            Cooldowns.Tick();
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
