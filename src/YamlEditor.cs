using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SoundboardMod
{
    /// <summary>
    /// Changes an entry's on/off switch directly in the text of
    /// soundboard.yaml - used when a checkbox is ticked in the options screen.
    /// It edits lines rather than re-writing the file from a data model, so the
    /// player's comments, blank lines, quoting and layout all survive.
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
