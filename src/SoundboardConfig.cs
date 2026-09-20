using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace SoundboardMod
{
    public enum IssueSeverity
    {
        Warning,
        Error,
    }

    /// <summary>A problem found in the config: shown in the options menu and written to the log.</summary>
    public sealed class ConfigIssue
    {
        public IssueSeverity Severity;

        /// <summary>1-based line in soundboard.yaml, or 0 if not tied to one.</summary>
        public int Line;

        public string Message;

        public override string ToString()
        {
            return (Line > 0 ? "Line " + Line + ": " : string.Empty) + Message;
        }
    }

    /// <summary>Tunable numbers behind some of the built-in events (the "settings:" block).</summary>
    public sealed class SoundboardSettings
    {
        public float HardLandingSpeed = 30f;
        public float TerminalVelocity = 40f;
        public float PlayerJumpCooldown = 2f;
        public float ArtificerPyroJumpCooldown = 10f;
        public float SpottedCooldown = 10f;
        public float SwimUnderwaterCooldown = 5f;
        public bool Debug;
    }

    /// <summary>One thing to play: a file plus how loud and how late.</summary>
    public sealed class SoundRef
    {
        /// <summary>File name as written in the config, relative to the sounds folder.</summary>
        public string File;

        /// <summary>1 = as recorded, 0.5 = half as loud.</summary>
        public float Volume = 1f;

        /// <summary>Seconds to wait after the event before playing.</summary>
        public float Delay;

        /// <summary>
        /// The volume and delay exactly as written on this sound in the file. For a sound in a
        /// "together" group, Volume and Delay above also include the group's own volume/delay
        /// (multiplied / added); these don't, so they're what an edit of the sound changes.
        /// </summary>
        public float OwnVolume = 1f;

        public float OwnDelay;

        /// <summary>
        /// Position of this sound among the sounds written in its entry's "together" list (0 for a
        /// plain entry), counting sounds that couldn't be read - so it names the same sound in the
        /// file even if an earlier one is missing here. Unlike Line it doesn't change when lines are
        /// added above it.
        /// </summary>
        public int Member;

        public int Line;

        /// <summary>Full path once found on disk (filled in by SoundFileResolver).</summary>
        public string ResolvedPath;
    }

    /// <summary>
    /// One step in an event's rotation: a single sound, or a "together"
    /// group whose sounds all play at once. Each choice gets one on/off
    /// checkbox in the options menu.
    /// </summary>
    public sealed class SoundChoice
    {
        /// <summary>Stable key for the saved on/off setting (letters, digits and underscores only).</summary>
        public string Id;

        public string Label;
        public string Description;

        /// <summary>
        /// Whether this entry is switched on. Comes from "enabled:" / "disabled:" in the
        /// file and is what the options-screen checkbox shows and changes.
        /// </summary>
        public bool Enabled = true;

        /// <summary>
        /// Seconds after this entry plays before it can play again ("cooldown:"); 0 = no limit.
        /// While it waits, the event's rotation skips it. For a "together" group it covers the whole group.
        /// </summary>
        public float Cooldown;

        /// <summary>True for a "together:" group of sounds, false for a plain single sound.</summary>
        public bool IsGroup;

        public List<SoundRef> Sounds = new List<SoundRef>();
        public int Line;

        /// <summary>Where the entry sits in the file, so its on/off switch can be edited in place.</summary>
        public YamlItemSpan Source;
    }

    /// <summary>The lines an entry occupies in soundboard.yaml (see YamlEditor).</summary>
    public sealed class YamlItemSpan
    {
        public YamlKind Kind;
        public bool IsFlow;
        public int StartLine;
        public int EndLine;

        /// <summary>Column the entry's own keys start at (block form).</summary>
        public int KeyIndent;

        /// <summary>"enabled" or "disabled" if the entry already has one of them, else null.</summary>
        public string SwitchKey;

        /// <summary>1-based line that switch is on (block form).</summary>
        public int SwitchLine;
    }

    /// <summary>Everything mapped to one event, in the order they take turns.</summary>
    public sealed class EventBinding
    {
        public string EventName;
        public List<SoundChoice> Choices = new List<SoundChoice>();
    }

    public sealed class SoundboardConfig
    {
        public SoundboardSettings Settings = new SoundboardSettings();
        public List<EventBinding> Events = new List<EventBinding>();
        public List<ConfigIssue> Issues = new List<ConfigIssue>();

        /// <summary>True if the file couldn't be read at all (a YAML syntax error), as opposed to having fixable problems.</summary>
        public bool Failed;

        public bool HasErrors => Failed || Issues.Any(i => i.Severity == IssueSeverity.Error);

        public int SoundCount => Events.Sum(e => e.Choices.Sum(c => c.Sounds.Count));

        public void AddIssue(IssueSeverity severity, int line, string message)
        {
            Issues.Add(new ConfigIssue { Severity = severity, Line = line, Message = message });
        }
    }

    /// <summary>
    /// Turns the text of soundboard.yaml into a SoundboardConfig. Never
    /// throws for bad input: every problem becomes a ConfigIssue with a
    /// line number, and whatever is still usable is kept, so one typo doesn't
    /// silence everything else.
    /// </summary>
    public static class SoundboardConfigParser
    {
        private static readonly string[] TopLevelKeys = { "settings", "events" };
        private static readonly string[] EntryKeys = { "file", "volume", "delay", "name", "description", "enabled", "disabled", "together", "cooldown" };
        private static readonly string[] MemberKeys = { "file", "volume", "delay" };

        private const float MaxVolume = 10f;
        private const float MaxDelay = 120f;
        private const float MaxCooldown = 3600f;

        private sealed class FloatSetting
        {
            public string Name;
            public float Min;
            public float Max;
            public Action<SoundboardSettings, float> Set;
        }

        private static readonly FloatSetting[] FloatSettings =
        {
            new FloatSetting { Name = "hard-landing-speed", Min = 1f, Max = 500f, Set = (s, v) => s.HardLandingSpeed = v },
            new FloatSetting { Name = "terminal-velocity", Min = 1f, Max = 500f, Set = (s, v) => s.TerminalVelocity = v },
            new FloatSetting { Name = "player-jump-cooldown", Min = 0f, Max = 600f, Set = (s, v) => s.PlayerJumpCooldown = v },
            new FloatSetting { Name = "artificer-pyro-jump-cooldown", Min = 0f, Max = 600f, Set = (s, v) => s.ArtificerPyroJumpCooldown = v },
            new FloatSetting { Name = "spotted-cooldown", Min = 0f, Max = 600f, Set = (s, v) => s.SpottedCooldown = v },
            new FloatSetting { Name = "swim-underwater-cooldown", Min = 0f, Max = 600f, Set = (s, v) => s.SwimUnderwaterCooldown = v },
        };

        private const string DebugSettingName = "debug";

        public static SoundboardConfig Parse(string yaml, EventCatalog catalog)
        {
            var config = new SoundboardConfig();

            YamlNode root;
            try
            {
                root = MiniYaml.Parse(yaml);
            }
            catch (YamlParseException e)
            {
                config.Failed = true;
                config.AddIssue(IssueSeverity.Error, e.Line, e.Message);
                return config;
            }

            if (root.Kind != YamlKind.Mapping)
            {
                config.Failed = true;
                config.AddIssue(IssueSeverity.Error, root.Line, "The file should be made of 'settings:' and 'events:' sections, but it starts with a " + (root.Kind == YamlKind.Sequence ? "list" : "plain value") + ".");
                return config;
            }

            foreach (YamlEntry entry in root.Entries)
            {
                switch (NameMatch.Normalize(entry.Key))
                {
                    case "settings":
                        ParseSettings(entry.Value, config);
                        break;
                    case "events":
                        ParseEvents(entry.Value, config, catalog);
                        break;
                    default:
                        string hint = catalog.Resolve(entry.Key) != null
                            ? " '" + entry.Key + "' is an event name - put it inside the 'events:' section."
                            : SuggestionText(entry.Key, TopLevelKeys);
                        config.AddIssue(IssueSeverity.Warning, entry.Line, "Unknown section '" + entry.Key + "' (ignored)." + hint);
                        break;
                }
            }

            return config;
        }

        // --- settings -------------------------------------------------------

        private static void ParseSettings(YamlNode node, SoundboardConfig config)
        {
            if (node.IsNull)
            {
                return;
            }

            if (node.Kind != YamlKind.Mapping)
            {
                config.AddIssue(IssueSeverity.Error, node.Line, "'settings:' should contain lines like 'hard-landing-speed: 30'.");
                return;
            }

            foreach (YamlEntry entry in node.Entries)
            {
                string key = NameMatch.Normalize(entry.Key);

                if (key == NameMatch.Normalize(DebugSettingName))
                {
                    if (TryParseBool(entry.Value, out bool debug))
                    {
                        config.Settings.Debug = debug;
                    }
                    else
                    {
                        config.AddIssue(IssueSeverity.Warning, entry.Line, "'" + entry.Key + "' should be true or false.");
                    }

                    continue;
                }

                FloatSetting setting = FloatSettings.FirstOrDefault(s => NameMatch.Normalize(s.Name) == key);
                if (setting == null)
                {
                    config.AddIssue(IssueSeverity.Warning, entry.Line, "Unknown setting '" + entry.Key + "' (ignored)." + SuggestionText(entry.Key, FloatSettings.Select(s => s.Name).Concat(new[] { DebugSettingName })));
                    continue;
                }

                if (!TryParseNumber(entry.Value, out float value, out string problem))
                {
                    config.AddIssue(IssueSeverity.Warning, entry.Line, "'" + entry.Key + "' " + problem + " Keeping the default.");
                    continue;
                }

                if (value < setting.Min || value > setting.Max)
                {
                    float clamped = Math.Max(setting.Min, Math.Min(setting.Max, value));
                    config.AddIssue(IssueSeverity.Warning, entry.Line, "'" + entry.Key + "' must be between " + Format(setting.Min) + " and " + Format(setting.Max) + "; using " + Format(clamped) + ".");
                    value = clamped;
                }

                setting.Set(config.Settings, value);
            }
        }

        // --- events -----------------------------------------------------------

        private static void ParseEvents(YamlNode node, SoundboardConfig config, EventCatalog catalog)
        {
            if (node.IsNull)
            {
                return;
            }

            if (node.Kind != YamlKind.Mapping)
            {
                config.AddIssue(IssueSeverity.Error, node.Line, "'events:' should contain event names, each followed by a list of sounds - for example 'PlayerDeath:' then '  - boom.wav' on the next line.");
                return;
            }

            var bindings = new Dictionary<string, EventBinding>();
            var firstLine = new Dictionary<string, int>();
            var usedIds = new HashSet<string>();

            foreach (YamlEntry entry in node.Entries)
            {
                string canonical = catalog.Resolve(entry.Key);
                if (canonical == null)
                {
                    config.AddIssue(IssueSeverity.Warning, entry.Line, "'" + entry.Key + "' isn't an event this mod knows, so it will never play." + SuggestionText(entry.Key, catalog.Suggest(entry.Key)) + " See events.txt next to this file for the full list.");
                    canonical = entry.Key.Trim();
                }

                string normalized = NameMatch.Normalize(canonical);
                if (!bindings.TryGetValue(normalized, out EventBinding binding))
                {
                    binding = new EventBinding { EventName = canonical };
                    bindings[normalized] = binding;
                    firstLine[normalized] = entry.Line;
                    config.Events.Add(binding);
                }
                else
                {
                    config.AddIssue(IssueSeverity.Warning, entry.Line, "'" + entry.Key + "' is the same event as the one on line " + firstLine[normalized] + ". Their sounds have been combined - consider keeping it in one place.");
                }

                if (entry.Value.IsNull)
                {
                    config.AddIssue(IssueSeverity.Warning, entry.Line, "'" + entry.Key + "' has no sounds listed under it.");
                    continue;
                }

                IEnumerable<YamlNode> items = entry.Value.Kind == YamlKind.Sequence ? entry.Value.Items : new List<YamlNode> { entry.Value };

                foreach (YamlNode item in items)
                {
                    SoundChoice choice = ParseChoice(item, binding.EventName, usedIds, config);
                    if (choice != null)
                    {
                        binding.Choices.Add(choice);
                    }
                }
            }

            config.Events.RemoveAll(e => e.Choices.Count == 0);
        }

        private static SoundChoice ParseChoice(YamlNode item, string eventName, HashSet<string> usedIds, SoundboardConfig config)
        {
            if (item.IsNull)
            {
                config.AddIssue(IssueSeverity.Warning, item.Line, "Empty list item (ignored).");
                return null;
            }

            var choice = new SoundChoice { Line = item.Line };

            if (item.Kind == YamlKind.Scalar)
            {
                SoundRef single = ParseSoundRef(item, config, isMember: false);
                if (single == null)
                {
                    return null;
                }

                choice.Sounds.Add(single);
                choice.Label = PrettyName(single.File);
                choice.Id = UniqueId(usedIds, eventName, StemOf(single.File));
                choice.Source = new YamlItemSpan { Kind = YamlKind.Scalar, StartLine = item.Line, EndLine = item.EndLine };
                return choice;
            }

            if (item.Kind != YamlKind.Mapping)
            {
                config.AddIssue(IssueSeverity.Error, item.Line, "Each item should be a file name like 'boom.wav', or a group of settings like 'file: boom.wav' with 'volume: 0.5' below it.");
                return null;
            }

            CheckKeys(item, EntryKeys, config, "sound");

            string name = ReadString(item, "name", config);
            choice.Description = ReadString(item, "description", config);
            choice.Cooldown = ReadNumber(item, "cooldown", 0f, 0f, MaxCooldown, config);

            // "enabled: false" and "disabled: true" mean the same thing; people
            // reach for either. If both are there, enabled wins.
            YamlEntry enabled = item.Find("enabled");
            YamlEntry disabled = item.Find("disabled");
            if (enabled != null && disabled != null)
            {
                config.AddIssue(IssueSeverity.Warning, disabled.Line, "This entry has both 'enabled' and 'disabled'. Only 'enabled' is used - remove one of them.");
            }

            YamlEntry switchEntry = enabled ?? disabled;
            if (switchEntry != null)
            {
                if (TryParseBool(switchEntry.Value, out bool on))
                {
                    choice.Enabled = switchEntry == enabled ? on : !on;
                }
                else
                {
                    config.AddIssue(IssueSeverity.Warning, switchEntry.Line, "'" + switchEntry.Key + "' should be true or false.");
                }
            }

            choice.Source = new YamlItemSpan
            {
                Kind = YamlKind.Mapping,
                IsFlow = item.IsFlow,
                StartLine = item.Line,
                EndLine = item.EndLine,
                KeyIndent = item.Indent,
                SwitchKey = switchEntry?.Key,
                SwitchLine = switchEntry?.Line ?? 0,
            };

            YamlEntry together = item.Find("together");
            if (together == null)
            {
                SoundRef single = ParseSoundRef(item, config, isMember: false);
                if (single == null)
                {
                    return null;
                }

                choice.Sounds.Add(single);
                choice.Label = name ?? PrettyName(single.File);
                choice.Id = UniqueId(usedIds, eventName, name ?? StemOf(single.File));
                return choice;
            }

            choice.IsGroup = true;

            // A group: everything in 'together' plays at once. Its own
            // volume multiplies and its delay adds to each member's.
            if (item.Find("file") != null)
            {
                config.AddIssue(IssueSeverity.Error, item.Find("file").Line, "'file' can't be used next to 'together'. Put each file inside the 'together' list.");
                return null;
            }

            float groupVolume = ReadVolume(item, config);
            float groupDelay = ReadDelay(item, config);

            if (together.Value.IsNull || together.Value.Kind != YamlKind.Sequence || together.Value.Items.Count == 0)
            {
                config.AddIssue(IssueSeverity.Error, together.Line, "'together' needs a list of sounds under it, like '- boom.wav'.");
                return null;
            }

            int memberIndex = -1;
            foreach (YamlNode memberNode in together.Value.Items)
            {
                memberIndex++;
                if (memberNode.IsNull || (memberNode.Kind != YamlKind.Scalar && memberNode.Kind != YamlKind.Mapping))
                {
                    config.AddIssue(IssueSeverity.Error, memberNode.Line, "Each sound in 'together' should be a file name or a 'file: ...' block.");
                    continue;
                }

                SoundRef member = ParseSoundRef(memberNode, config, isMember: true);
                if (member == null)
                {
                    continue;
                }

                member.Member = memberIndex;
                member.Volume = Clamp(member.Volume * groupVolume, 0f, MaxVolume);
                member.Delay = Clamp(member.Delay + groupDelay, 0f, MaxDelay);
                choice.Sounds.Add(member);
            }

            if (choice.Sounds.Count == 0)
            {
                return null;
            }

            choice.Label = name ?? string.Join(" + ", choice.Sounds.Select(s => PrettyName(s.File)));
            choice.Id = UniqueId(usedIds, eventName, name ?? string.Join("_", choice.Sounds.Select(s => StemOf(s.File))));
            return choice;
        }

        /// <summary>Reads 'file', 'volume' and 'delay' from a scalar (just a file name) or a mapping.</summary>
        private static SoundRef ParseSoundRef(YamlNode node, SoundboardConfig config, bool isMember)
        {
            string file;
            var sound = new SoundRef { Line = node.Line };

            if (node.Kind == YamlKind.Scalar)
            {
                file = node.Text;
            }
            else
            {
                if (isMember)
                {
                    CheckKeys(node, MemberKeys, config, "sound in a 'together' group");
                }

                YamlEntry fileEntry = node.Find("file");
                if (fileEntry == null)
                {
                    config.AddIssue(IssueSeverity.Error, node.Line, "This sound is missing 'file:' - which audio file should play?");
                    return null;
                }

                if (fileEntry.Value.Kind != YamlKind.Scalar)
                {
                    config.AddIssue(IssueSeverity.Error, fileEntry.Line, "'file' should be a single file name.");
                    return null;
                }

                file = fileEntry.Value.Text;
                sound.Volume = ReadVolume(node, config);
                sound.Delay = ReadDelay(node, config);
                sound.OwnVolume = sound.Volume;
                sound.OwnDelay = sound.Delay;
            }

            file = file?.Trim();
            if (string.IsNullOrEmpty(file))
            {
                config.AddIssue(IssueSeverity.Error, node.Line, "The file name is empty.");
                return null;
            }

            string normalizedPath = file.Replace('\\', '/');
            if (normalizedPath.StartsWith("/") || (normalizedPath.Length > 1 && normalizedPath[1] == ':') || normalizedPath.Split('/').Contains(".."))
            {
                config.AddIssue(IssueSeverity.Error, node.Line, "'" + file + "' - file names must be relative to the sounds folder (no drive letters and no '..').");
                return null;
            }

            sound.File = normalizedPath;
            return sound;
        }

        private static float ReadVolume(YamlNode mapping, SoundboardConfig config)
        {
            return ReadNumber(mapping, "volume", 1f, 0f, MaxVolume, config);
        }

        private static float ReadDelay(YamlNode mapping, SoundboardConfig config)
        {
            return ReadNumber(mapping, "delay", 0f, 0f, MaxDelay, config);
        }

        private static float ReadNumber(YamlNode mapping, string key, float fallback, float min, float max, SoundboardConfig config)
        {
            YamlEntry entry = mapping.Find(key);
            if (entry == null)
            {
                return fallback;
            }

            if (!TryParseNumber(entry.Value, out float value, out string problem))
            {
                config.AddIssue(IssueSeverity.Warning, entry.Line, "'" + key + "' " + problem + " Using " + Format(fallback) + ".");
                return fallback;
            }

            if (value < min || value > max)
            {
                float clamped = Clamp(value, min, max);
                config.AddIssue(IssueSeverity.Warning, entry.Line, "'" + key + "' must be between " + Format(min) + " and " + Format(max) + "; using " + Format(clamped) + ".");
                return clamped;
            }

            return value;
        }

        private static string ReadString(YamlNode mapping, string key, SoundboardConfig config)
        {
            YamlEntry entry = mapping.Find(key);
            if (entry == null || entry.Value.IsNull)
            {
                return null;
            }

            if (entry.Value.Kind != YamlKind.Scalar)
            {
                config.AddIssue(IssueSeverity.Warning, entry.Line, "'" + key + "' should be plain text.");
                return null;
            }

            return entry.Value.Text;
        }

        private static void CheckKeys(YamlNode mapping, string[] allowed, SoundboardConfig config, string what)
        {
            foreach (YamlEntry entry in mapping.Entries)
            {
                if (!allowed.Any(a => NameMatch.Normalize(a) == NameMatch.Normalize(entry.Key)))
                {
                    config.AddIssue(IssueSeverity.Warning, entry.Line, "Unknown option '" + entry.Key + "' for a " + what + " (ignored)." + SuggestionText(entry.Key, allowed));
                }
            }
        }

        // --- small helpers ------------------------------------------------------

        private static string SuggestionText(string input, IEnumerable<string> candidates)
        {
            return SuggestionText(input, NameMatch.Closest(input, candidates));
        }

        private static string SuggestionText(string input, string suggestion)
        {
            return suggestion == null ? string.Empty : " Did you mean '" + suggestion + "'?";
        }

        private static bool TryParseNumber(YamlNode node, out float value, out string problem)
        {
            value = 0f;
            problem = null;

            if (node.Kind != YamlKind.Scalar || node.IsNull)
            {
                problem = "needs a number, like 0.5.";
                return false;
            }

            string text = node.Text.Trim();
            if (text.IndexOf(',') >= 0 && text.IndexOf('.') < 0)
            {
                problem = "'" + text + "' uses a comma - write decimals with a dot, like 0.5.";
                return false;
            }

            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || float.IsNaN(value) || float.IsInfinity(value))
            {
                problem = "'" + text + "' isn't a number - use something like 0.5 or 2.";
                return false;
            }

            return true;
        }

        private static bool TryParseBool(YamlNode node, out bool value)
        {
            value = false;
            if (node.Kind != YamlKind.Scalar || node.IsNull)
            {
                return false;
            }

            switch (node.Text.Trim().ToLowerInvariant())
            {
                case "true":
                case "yes":
                case "on":
                    value = true;
                    return true;
                case "false":
                case "no":
                case "off":
                    value = false;
                    return true;
                default:
                    return false;
            }
        }

        private static float Clamp(float v, float min, float max)
        {
            return Math.Max(min, Math.Min(max, v));
        }

        private static string Format(float v)
        {
            return v.ToString("0.##", CultureInfo.InvariantCulture);
        }

        /// <summary>"vine-boom.wav" -> "vine-boom"; keeps any sub-folder out of the name.</summary>
        internal static string StemOf(string file)
        {
            string name = file.Replace('\\', '/');
            int slash = name.LastIndexOf('/');
            if (slash >= 0)
            {
                name = name.Substring(slash + 1);
            }

            int dot = name.LastIndexOf('.');
            return dot > 0 ? name.Substring(0, dot) : name;
        }

        /// <summary>"vine-boom.wav" -> "Vine Boom".</summary>
        internal static string PrettyName(string file)
        {
            string stem = StemOf(file).Replace('-', ' ').Replace('_', ' ');
            var sb = new StringBuilder();
            bool startOfWord = true;
            foreach (char c in stem)
            {
                if (char.IsWhiteSpace(c))
                {
                    if (!startOfWord)
                    {
                        sb.Append(' ');
                    }

                    startOfWord = true;
                }
                else
                {
                    sb.Append(startOfWord ? char.ToUpperInvariant(c) : c);
                    startOfWord = false;
                }
            }

            string pretty = sb.ToString().Trim();
            return pretty.Length == 0 ? file : pretty;
        }

        /// <summary>
        /// Builds the key for a choice's saved on/off setting. The game's
        /// settings system only allows letters, digits and underscores.
        /// Derived from the event and file name (not list position) so
        /// reordering entries doesn't scramble people's saved choices.
        /// </summary>
        private static string UniqueId(HashSet<string> used, string eventName, string seed)
        {
            string baseId = Sanitize(eventName) + "_" + Sanitize(seed);
            string id = baseId;
            for (int n = 2; !used.Add(id); n++)
            {
                id = baseId + "_" + n;
            }

            return id;
        }

        private static string Sanitize(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s ?? string.Empty)
            {
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            }

            return sb.ToString().Trim('_');
        }
    }
}
