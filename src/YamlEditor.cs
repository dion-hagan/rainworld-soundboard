using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SoundboardMod
{
    /// <summary>
    /// Changes soundboard.yaml directly in its text - to switch an entry on or
    /// off (a checkbox in the options screen) or to add a sound to an event (the
    /// "Add Sound" page). It edits lines rather than re-writing the file from a
    /// data model, so the player's comments, blank lines, quoting and layout all
    /// survive.
    ///
    /// The switch is written as "enabled: false" (or, if the entry already
    /// uses "disabled:", that key is kept and flipped). Turning an entry back
    /// on when it has no switch is a no-op: on is the default.
    ///
    /// Callers must build the YamlItemSpan from a fresh parse of the same
    /// text: line numbers from an older version of the file would point at
    /// the wrong lines.
    /// </summary>
    public static class YamlEditor
    {
        public sealed class Result
        {
            public bool Ok;

            /// <summary>The full new text (the same as the input if nothing needed changing).</summary>
            public string Text;

            /// <summary>Plain-English reason when Ok is false.</summary>
            public string Error;

            public static Result Success(string text)
            {
                return new Result { Ok = true, Text = text };
            }

            public static Result Failure(string text, string error)
            {
                return new Result { Ok = false, Text = text, Error = error };
            }
        }

        private static readonly Regex BlockSwitchLine = new Regex(@"^(?<lead>\s*(?:-\s+)?)(?<key>[""']?(?:enabled|disabled)[""']?)(?<colon>\s*:\s*)(?<rest>.*)$");
        private static readonly Regex FlowSwitch = new Regex(@"(?<key>\b(?:enabled|disabled)\s*:\s*)(?<value>[^,}\s#]+)");
        private static readonly Regex DashLine = new Regex(@"^(?<lead>\s*-\s+)(?<rest>.*)$");

        public static Result SetEnabled(string text, YamlItemSpan span, bool enabled)
        {
            if (span == null)
            {
                return Result.Failure(text, "the entry's position in the file isn't known");
            }

            // Lines keep their own "\r" so a Windows-style file stays Windows-style.
            var lines = new List<string>(text.Split('\n'));
            int start = span.StartLine - 1;
            int end = span.EndLine - 1;
            if (start < 0 || end >= lines.Count || start > end)
            {
                return Result.Failure(text, "the file changed since it was read");
            }

            if (span.SwitchKey != null)
            {
                bool flipped = span.SwitchKey == "disabled";
                string newValue = (enabled != flipped) ? "true" : "false";
                return span.IsFlow
                    ? ReplaceInFlow(text, lines, start, end, newValue)
                    : ReplaceInBlock(text, lines, span.SwitchLine - 1, newValue);
            }

            if (enabled)
            {
                return Result.Success(text); // no switch means on already
            }

            switch (span.Kind)
            {
                case YamlKind.Scalar:
                    return ConvertScalarEntry(text, lines, start, end);
                case YamlKind.Mapping when span.IsFlow:
                    return AddToFlow(text, lines, start, end);
                case YamlKind.Mapping:
                    Insert(lines, end, new string(' ', span.KeyIndent) + "enabled: false");
                    return Result.Success(Join(lines));
                default:
                    return Result.Failure(text, "this kind of entry can't be switched off automatically");
            }
        }

        // "  key: value  # note"  ->  same line with only the value replaced.
        private static Result ReplaceInBlock(string text, List<string> lines, int index, string newValue)
        {
            if (index < 0 || index >= lines.Count)
            {
                return Result.Failure(text, "the file changed since it was read");
            }

            string line = lines[index];
            string cr = line.EndsWith("\r") ? "\r" : string.Empty;
            Match m = BlockSwitchLine.Match(line.TrimEnd('\r'));
            if (!m.Success)
            {
                return Result.Failure(text, "couldn't find the enabled/disabled setting on line " + (index + 1));
            }

            string rest = m.Groups["rest"].Value;
            string beforeComment = MiniYaml.StripComment(rest);
            string comment = rest.Substring(beforeComment.Length);
            string value = beforeComment.TrimEnd();
            string spacing = beforeComment.Substring(value.Length);

            if (string.Equals(value, newValue, StringComparison.OrdinalIgnoreCase))
            {
                return Result.Success(text);
            }

            lines[index] = m.Groups["lead"].Value + m.Groups["key"].Value + m.Groups["colon"].Value + newValue + spacing + comment + cr;
            return Result.Success(Join(lines));
        }

        // "- { file: a.wav, enabled: false }"  ->  value swapped inside the braces.
        private static Result ReplaceInFlow(string text, List<string> lines, int start, int end, string newValue)
        {
            if (start != end)
            {
                return Result.Failure(text, "this entry is written across several lines inside { } - change its 'enabled' by hand");
            }

            string line = lines[start];
            string cr = line.EndsWith("\r") ? "\r" : string.Empty;
            string content = line.TrimEnd('\r');
            string code = MiniYaml.StripComment(content);
            Match m = FlowSwitch.Match(code);
            if (!m.Success)
            {
                return Result.Failure(text, "couldn't find the enabled/disabled setting on line " + (start + 1));
            }

            if (string.Equals(m.Groups["value"].Value, newValue, StringComparison.OrdinalIgnoreCase))
            {
                return Result.Success(text);
            }

            Group value = m.Groups["value"];
            lines[start] = content.Substring(0, value.Index) + newValue + content.Substring(value.Index + value.Length) + cr;
            return Result.Success(Join(lines));
        }

        // "- boom.wav"  ->  "- file: boom.wav" plus an "enabled: false" line under it.
        private static Result ConvertScalarEntry(string text, List<string> lines, int start, int end)
        {
            string line = lines[start];
            string cr = line.EndsWith("\r") ? "\r" : string.Empty;
            Match m = DashLine.Match(line.TrimEnd('\r'));
            if (!m.Success || start != end)
            {
                return Result.Failure(text, "this entry isn't a list item ('- file.wav'), so it can't be switched off automatically - add 'enabled: false' by hand");
            }

            string lead = m.Groups["lead"].Value;
            lines[start] = lead + "file: " + m.Groups["rest"].Value + cr;
            Insert(lines, start, new string(' ', lead.Length) + "enabled: false");
            return Result.Success(Join(lines));
        }

        // "- { file: a.wav }"  ->  "- { file: a.wav, enabled: false }"
        private static Result AddToFlow(string text, List<string> lines, int start, int end)
        {
            if (start != end)
            {
                return Result.Failure(text, "this entry is written across several lines inside { } - add 'enabled: false' by hand");
            }

            string line = lines[start];
            string cr = line.EndsWith("\r") ? "\r" : string.Empty;
            string content = line.TrimEnd('\r');
            string code = MiniYaml.StripComment(content);
            int close = code.LastIndexOf('}');
            if (close < 0)
            {
                return Result.Failure(text, "couldn't find the end of the { } on line " + (start + 1));
            }

            string before = code.Substring(0, close).TrimEnd();
            string tail = content.Substring(close); // "}" plus any comment
            lines[start] = before + ", enabled: false " + tail + cr;
            return Result.Success(Join(lines));
        }

        // --- adding a sound --------------------------------------------------------

        /// <summary>
        /// Adds a sound to the end of an event's list - what the options screen's
        /// "Add Sound" page does. The event's block is extended in place (the new
        /// item lines up with the existing ones); an event that isn't in the file yet
        /// gets a new block at the end of 'events:', and a file with no 'events:'
        /// section gets one. Everything else in the file is left exactly as it was.
        ///
        /// Unlike SetEnabled this parses the text itself, so it always works from
        /// the current line numbers.
        /// </summary>
        /// <param name="eventName">The event's canonical name, written as the key if the event is new.</param>
        /// <param name="parts">The sound(s) to add: exactly one for a plain entry.</param>
        /// <param name="together">Write the parts as a "together:" group that plays all at once (one entry in the rotation).</param>
        public static Result AddSound(string text, string eventName, IReadOnlyList<Part> parts, bool together, IReadOnlyList<KeyValuePair<string, string>> entryOptions)
        {
            YamlNode root;
            try
            {
                root = MiniYaml.Parse(text);
            }
            catch (YamlParseException e)
            {
                return Result.Failure(text, "soundboard.yaml has an error on line " + e.Line + " (" + e.Message + ") - fix that first");
            }

            if (root.Kind != YamlKind.Mapping)
            {
                return Result.Failure(text, "soundboard.yaml should be made of 'settings:' and 'events:' sections");
            }

            var lines = new List<string>(text.Split('\n'));
            if (root.EndLine > lines.Count)
            {
                return Result.Failure(text, "the file uses old-style line endings that can't be edited automatically - add the sound by hand");
            }

            YamlEntry events = null;
            foreach (YamlEntry entry in root.Entries)
            {
                if (NameMatch.Normalize(entry.Key) == "events")
                {
                    events = entry;
                    break;
                }
            }

            // No 'events:' at all: start the section after everything that's there.
            if (events == null)
            {
                var section = new List<string> { "events:", "  " + eventName + ":" };
                section.AddRange(BlockItem(parts, together, entryOptions, 4));
                int last = LastContentLine(lines);
                if (last >= 0)
                {
                    section.Insert(0, string.Empty);
                }

                InsertAfter(lines, last, section);
                return Result.Success(Join(lines));
            }

            int eventsIndex = events.Line - 1;
            if (eventsIndex < 0 || eventsIndex >= lines.Count)
            {
                return Result.Failure(text, "the file changed since it was read");
            }

            // "events:" with nothing under it.
            if (events.Value.IsNull)
            {
                if (!EndsWithBareColon(lines[eventsIndex]))
                {
                    return Result.Failure(text, "'events:' on line " + events.Line + " has something after the colon - add the sound by hand");
                }

                var section = new List<string> { "  " + eventName + ":" };
                section.AddRange(BlockItem(parts, together, entryOptions, 4));
                InsertAfter(lines, eventsIndex, section);
                return Result.Success(Join(lines));
            }

            YamlNode eventList = events.Value;
            if (eventList.Kind != YamlKind.Mapping || eventList.IsFlow)
            {
                return Result.Failure(text, "'events:' on line " + events.Line + " isn't written as a plain list of event names, so it can't be edited automatically - add the sound by hand");
            }

            string wanted = NameMatch.Normalize(eventName);
            YamlEntry match = null;
            foreach (YamlEntry entry in eventList.Entries)
            {
                if (NameMatch.Normalize(entry.Key) == wanted)
                {
                    match = entry; // the last block for the event: the new sound goes at the end of its rotation
                }
            }

            int keyIndent = eventList.Indent;

            // Lists are indented by however much the player already indents them under their event name.
            int dashIndent = keyIndent + 2;
            foreach (YamlEntry entry in eventList.Entries)
            {
                if (entry.Value.Kind == YamlKind.Sequence && !entry.Value.IsFlow)
                {
                    dashIndent = entry.Value.Indent;
                    break;
                }
            }

            // A new event: a block after the last one, set apart by a blank line like the others.
            if (match == null)
            {
                var block = new List<string> { string.Empty, new string(' ', keyIndent) + eventName + ":" };
                block.AddRange(BlockItem(parts, together, entryOptions, dashIndent));
                InsertAfter(lines, eventList.EndLine - 1, block);
                return Result.Success(Join(lines));
            }

            YamlNode value = match.Value;

            // "PlayerDeath:" with the list commented out or removed.
            if (value.IsNull)
            {
                if (!EndsWithBareColon(lines[match.Line - 1]))
                {
                    return Result.Failure(text, "'" + match.Key + "' on line " + match.Line + " has something after the colon - add the sound by hand");
                }

                InsertAfter(lines, match.Line - 1, BlockItem(parts, together, entryOptions, dashIndent));
                return Result.Success(Join(lines));
            }

            if (value.Kind == YamlKind.Sequence && !value.IsFlow)
            {
                InsertAfter(lines, value.EndLine - 1, BlockItem(parts, together, entryOptions, value.Indent));
                return Result.Success(Join(lines));
            }

            if (value.Kind == YamlKind.Sequence)
            {
                return AddToFlowList(text, lines, value, FlowItem(parts, together, entryOptions));
            }

            return Result.Failure(text, "'" + match.Key + "' on line " + match.Line + " has a single sound that isn't written as a list ('- file.wav'), so another can't be added automatically - turn it into a list first");
        }

        // "PlayerDeath: [a.wav, b.wav]"  ->  "PlayerDeath: [a.wav, b.wav, c.wav]"
        private static Result AddToFlowList(string text, List<string> lines, YamlNode list, string item)
        {
            if (list.Line != list.EndLine)
            {
                return Result.Failure(text, "the list starting on line " + list.Line + " is written across several lines inside [ ] - add the sound by hand");
            }

            int index = list.Line - 1;
            string line = lines[index];
            string cr = line.EndsWith("\r") ? "\r" : string.Empty;
            string content = line.TrimEnd('\r');
            string code = MiniYaml.StripComment(content);
            int close = code.LastIndexOf(']');
            if (close < 0)
            {
                return Result.Failure(text, "couldn't find the end of the [ ] on line " + list.Line);
            }

            string before = code.Substring(0, close).TrimEnd();
            string tail = content.Substring(close); // "]" plus any comment
            lines[index] = before + (before.EndsWith("[") ? string.Empty : ", ") + item + tail + cr;
            return Result.Success(Join(lines));
        }

        /// <summary>One sound to write: a file plus any options already formatted ("volume" -> "0.5"); none writes a bare "- file.wav".</summary>
        public sealed class Part
        {
            public string File;
            public IReadOnlyList<KeyValuePair<string, string>> Options = new List<KeyValuePair<string, string>>();
        }

        // A plain entry is one item; a group is "- together:" with each sound as an item nested under it.
        // entryOptions belong to the whole entry (e.g. "cooldown"): on the single item, or beside "together:" for a group.
        private static List<string> BlockItem(IReadOnlyList<Part> parts, bool together, IReadOnlyList<KeyValuePair<string, string>> entryOptions, int indent)
        {
            if (!together)
            {
                return SingleItem(parts[0], entryOptions, indent);
            }

            var group = new List<string> { new string(' ', indent) + "- together:" };
            foreach (Part part in parts)
            {
                group.AddRange(SingleItem(part, null, indent + 4));
            }

            foreach (KeyValuePair<string, string> option in entryOptions)
            {
                group.Add(new string(' ', indent + 2) + option.Key + ": " + option.Value);
            }

            return group;
        }

        private static List<string> SingleItem(Part part, IReadOnlyList<KeyValuePair<string, string>> extraOptions, int indent)
        {
            string pad = new string(' ', indent);
            List<KeyValuePair<string, string>> options = part.Options.Concat(extraOptions ?? new List<KeyValuePair<string, string>>()).ToList();
            if (options.Count == 0)
            {
                return new List<string> { pad + "- " + Scalar(part.File) };
            }

            var item = new List<string> { pad + "- file: " + Scalar(part.File) };
            foreach (KeyValuePair<string, string> option in options)
            {
                item.Add(pad + "  " + option.Key + ": " + option.Value);
            }

            return item;
        }

        private static string FlowItem(IReadOnlyList<Part> parts, bool together, IReadOnlyList<KeyValuePair<string, string>> entryOptions)
        {
            if (!together)
            {
                return FlowSound(parts[0], entryOptions);
            }

            var fields = new List<string> { "together: [" + string.Join(", ", parts.Select(p => FlowSound(p, null))) + "]" };
            fields.AddRange(entryOptions.Select(o => o.Key + ": " + o.Value));
            return "{ " + string.Join(", ", fields) + " }";
        }

        private static string FlowSound(Part part, IReadOnlyList<KeyValuePair<string, string>> extraOptions)
        {
            List<KeyValuePair<string, string>> options = part.Options.Concat(extraOptions ?? new List<KeyValuePair<string, string>>()).ToList();
            if (options.Count == 0)
            {
                return Scalar(part.File);
            }

            var fields = new List<string> { "file: " + Scalar(part.File) };
            foreach (KeyValuePair<string, string> option in options)
            {
                fields.Add(option.Key + ": " + option.Value);
            }

            return "{ " + string.Join(", ", fields) + " }";
        }

        /// <summary>
        /// A file name as YAML text: plain if it's made only of ordinary characters,
        /// otherwise 'single quoted' (the only escape there is '' for a quote).
        /// </summary>
        internal static string Scalar(string value)
        {
            bool plain = value.Length > 0 && (char.IsLetterOrDigit(value[0]) || value[0] == '_');
            foreach (char c in value)
            {
                if (!(char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-' || c == '.' || c == '(' || c == ')' || c == '+' || c == '/'))
                {
                    plain = false;
                    break;
                }
            }

            if (plain && value.Trim() == value)
            {
                return value;
            }

            return "'" + value.Replace("'", "''") + "'";
        }

        private static bool EndsWithBareColon(string line)
        {
            return MiniYaml.StripComment(line.TrimEnd('\r')).TrimEnd().EndsWith(":");
        }

        private static int LastContentLine(List<string> lines)
        {
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                if (lines[i].Trim().Length > 0)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>Inserts lines after lines[index] (or at the very top if index is -1), copying that line's line ending.</summary>
        private static void InsertAfter(List<string> lines, int index, List<string> block)
        {
            string cr = index >= 0 && lines[index].EndsWith("\r") ? "\r" : string.Empty;
            for (int i = 0; i < block.Count; i++)
            {
                lines.Insert(index + 1 + i, block[i] + cr);
            }
        }

        /// <summary>Inserts a new line after lines[index], copying that line's line ending.</summary>
        private static void Insert(List<string> lines, int index, string content)
        {
            string cr = lines[index].EndsWith("\r") ? "\r" : string.Empty;
            lines.Insert(index + 1, content + cr);
        }

        private static string Join(List<string> lines)
        {
            return string.Join("\n", lines);
        }
    }
}
