using System;
using System.Collections.Generic;
using System.Text;

namespace SoundboardMod
{
    public enum YamlKind
    {
        Scalar,
        Sequence,
        Mapping,
    }

    /// <summary>One entry of a YAML mapping. The key is always a string.</summary>
    public sealed class YamlEntry
    {
        public string Key;
        public int Line;
        public YamlNode Value;
    }

    /// <summary>
    /// A parsed YAML value. Scalars are kept as raw text - what they mean
    /// (number, yes/no, file name) is up to whoever reads them, which avoids
    /// YAML's habit of turning things like "no" or "1e3" into surprises.
    /// </summary>
    public sealed class YamlNode
    {
        public YamlKind Kind;

        /// <summary>1-based line the value starts on, for error messages.</summary>
        public int Line;

        /// <summary>Scalar text; null for an empty value ("key:" or "~" / "null").</summary>
        public string Text;

        public List<YamlNode> Items;
        public List<YamlEntry> Entries;

        public bool IsNull => Kind == YamlKind.Scalar && Text == null;

        public YamlEntry Find(string key)
        {
            if (Entries != null)
            {
                foreach (YamlEntry entry in Entries)
                {
                    if (entry.Key == key)
                    {
                        return entry;
                    }
                }
            }

            return null;
        }

        public static YamlNode Scalar(string text, int line)
        {
            return new YamlNode { Kind = YamlKind.Scalar, Text = text, Line = line };
        }
    }

    public sealed class YamlParseException : Exception
    {
        public int Line { get; }

        public YamlParseException(int line, string message)
            : base(message)
        {
            Line = line;
        }
    }

    /// <summary>
    /// A small YAML reader covering the subset the soundboard config uses,
    /// with plain-English error messages (people who edit the config aren't
    /// necessarily programmers). No dependencies, so it can be tested outside
    /// the game - same reasoning as MiniJson before it.
    ///
    /// Supported: nested mappings and "- " lists by indentation, "[a, b]" and
    /// "{k: v}" inline forms (may span lines), plain / 'single' / "double"
    /// quoted text, and # comments.
    /// Not supported (rejected with a clear message): tabs for indentation,
    /// anchors/aliases/tags, multi-line text blocks (| and >), multi-line
    /// plain text, several documents in one file, duplicate keys.
    /// </summary>
    public static class MiniYaml
    {
        private sealed class Line
        {
            public int Number;
            public int Indent;
            public string Text;
        }

        public static YamlNode Parse(string text)
        {
            var parser = new Parser(Tokenize(text ?? string.Empty));
            return parser.ParseDocument();
        }

        // --- Line splitting -------------------------------------------------

        private static List<Line> Tokenize(string text)
        {
            if (text.Length > 0 && text[0] == '﻿')
            {
                text = text.Substring(1);
            }

            var lines = new List<Line>();
            string[] raw = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < raw.Length; i++)
            {
                int number = i + 1;
                string content = raw[i];

                int indent = 0;
                while (indent < content.Length && content[indent] == ' ')
                {
                    indent++;
                }

                if (indent < content.Length && content[indent] == '\t')
                {
                    // Only an error if there's real content on the line.
                    if (StripComment(content.Substring(indent)).Trim().Length > 0)
                    {
                        throw new YamlParseException(number, "This line is indented with a TAB. YAML only allows spaces for indentation - replace the tab with spaces.");
                    }

                    continue;
                }

                string stripped = StripComment(content.Substring(indent)).TrimEnd();
                if (stripped.Length == 0)
                {
                    continue;
                }

                if (lines.Count == 0 && stripped == "---")
                {
                    continue;
                }

                if (stripped == "---" || stripped == "...")
                {
                    throw new YamlParseException(number, "Only one YAML document per file is supported - remove this '" + stripped + "' line.");
                }

                lines.Add(new Line { Number = number, Indent = indent, Text = stripped });
            }

            return lines;
        }

        /// <summary>
        /// Removes a trailing "# comment". A '#' only starts a comment at the
        /// start of the line or after whitespace, and never inside quotes, so
        /// "title: C# rocks" keeps its text but "a: 1 # note" loses "# note".
        /// </summary>
        internal static string StripComment(string s)
        {
            char quote = '\0';
            bool valueStart = true; // could a quoted scalar begin here?
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (quote != '\0')
                {
                    if (c == '\\' && quote == '"')
                    {
                        i++;
                    }
                    else if (c == quote)
                    {
                        // '' inside a single-quoted string is an escaped quote.
                        if (quote == '\'' && i + 1 < s.Length && s[i + 1] == '\'')
                        {
                            i++;
                        }
                        else
                        {
                            quote = '\0';
                        }
                    }

                    continue;
                }

                if (c == '#' && (i == 0 || char.IsWhiteSpace(s[i - 1])))
                {
                    return s.Substring(0, i);
                }

                if ((c == '"' || c == '\'') && valueStart)
                {
                    quote = c;
                    valueStart = false;
                    continue;
                }

                if (!char.IsWhiteSpace(c))
                {
                    // A quote opens a scalar only right after these; an
                    // apostrophe inside a word ("don't") is just text.
                    valueStart = c == ':' || c == '-' || c == '[' || c == '{' || c == ',';
                }
            }

            return s;
        }

        // --- Structure parsing ---------------------------------------------

        private sealed class Parser
        {
            private readonly List<Line> lines;
            private int pos;

            public Parser(List<Line> lines)
            {
                this.lines = lines;
            }

            public YamlNode ParseDocument()
            {
                if (lines.Count == 0)
                {
                    return new YamlNode { Kind = YamlKind.Mapping, Entries = new List<YamlEntry>(), Line = 1 };
                }

                if (lines[0].Indent != 0)
                {
                    throw new YamlParseException(lines[0].Number, "The first line must start at the left edge (no indentation).");
                }

                YamlNode root = ParseBlock(0);
                if (pos < lines.Count)
                {
                    throw new YamlParseException(lines[pos].Number, "This line is indented differently from the lines around it. Lines at the same level must line up exactly.");
                }

                return root;
            }

            private static bool IsDash(string text)
            {
                return text == "-" || text.StartsWith("- ");
            }

            private YamlNode ParseBlock(int indent)
            {
                return IsDash(lines[pos].Text) ? ParseSequence(indent) : ParseMapping(indent);
            }

            private YamlNode ParseSequence(int indent)
            {
                var node = new YamlNode { Kind = YamlKind.Sequence, Items = new List<YamlNode>(), Line = lines[pos].Number };

                while (pos < lines.Count && lines[pos].Indent == indent && IsDash(lines[pos].Text))
                {
                    Line line = lines[pos];
                    string rest = line.Text.Substring(1);
                    int gap = 0;
                    while (gap < rest.Length && rest[gap] == ' ')
                    {
                        gap++;
                    }

                    rest = rest.Substring(gap);

                    if (rest.Length == 0)
                    {
                        pos++;
                        if (pos < lines.Count && lines[pos].Indent > indent)
                        {
                            node.Items.Add(ParseBlock(lines[pos].Indent));
                        }
                        else
                        {
                            node.Items.Add(YamlNode.Scalar(null, line.Number));
                        }

                        continue;
                    }

                    if (IsDash(rest))
                    {
                        throw new YamlParseException(line.Number, "A list inside a list on one line isn't supported. Put the inner list on its own indented lines, or use [a, b] brackets.");
                    }

                    if (LooksLikeMapping(rest))
                    {
                        // "- key: value" - treat what follows the dash as a
                        // mapping line indented past the dash, so following
                        // "  key2: value" lines join the same mapping.
                        line.Indent = indent + 1 + gap;
                        line.Text = rest;
                        node.Items.Add(ParseMapping(line.Indent));
                    }
                    else
                    {
                        node.Items.Add(ParseInlineValue(rest, line, indent));
                    }
                }

                return node;
            }

            private YamlNode ParseMapping(int indent)
            {
                var node = new YamlNode { Kind = YamlKind.Mapping, Entries = new List<YamlEntry>(), Line = lines[pos].Number };

                while (pos < lines.Count && lines[pos].Indent == indent)
                {
                    Line line = lines[pos];
                    if (IsDash(line.Text))
                    {
                        throw new YamlParseException(line.Number, "Unexpected '-' here. A list item needs to sit under a name, like 'PlayerDeath:' on the line above.");
                    }

                    if (!TrySplitKey(line.Text, out string key, out string rest))
                    {
                        throw new YamlParseException(line.Number, "Expected 'name: value' on this line but found '" + line.Text + "'. Is there a missing colon (:) or a stray line?");
                    }

                    if (node.Find(key) != null)
                    {
                        throw new YamlParseException(line.Number, "'" + key + "' appears twice at the same level (first on line " + node.Find(key).Line + "). Each name may only be used once - merge the two blocks into one list.");
                    }

                    YamlNode value;
                    if (rest.Length == 0)
                    {
                        pos++;
                        if (pos < lines.Count && lines[pos].Indent > indent)
                        {
                            value = ParseBlock(lines[pos].Indent);
                        }
                        else if (pos < lines.Count && lines[pos].Indent == indent && IsDash(lines[pos].Text))
                        {
                            // "key:" followed by "- item" lines at the same
                            // indentation is valid YAML and a common way to write lists.
                            value = ParseSequence(indent);
                        }
                        else
                        {
                            value = YamlNode.Scalar(null, line.Number);
                        }
                    }
                    else
                    {
                        value = ParseInlineValue(rest, line, indent);
                    }

                    node.Entries.Add(new YamlEntry { Key = key, Line = line.Number, Value = value });
                }

                if (pos < lines.Count && lines[pos].Indent > indent)
                {
                    throw new YamlParseException(lines[pos].Number, "This line is indented more than the line before it, but there's nothing for it to belong to. Check the spacing (and that the line above ends with a colon if this is meant to be nested).");
                }

                return node;
            }

            /// <summary>
            /// Parses the value text that follows "key: " or "- " on the same
            /// line: a scalar, or an inline [list] / {map} (which may run on
            /// over several lines until its brackets balance).
            /// </summary>
            private YamlNode ParseInlineValue(string text, Line line, int indent)
            {
                pos++;

                if (text[0] == '[' || text[0] == '{')
                {
                    var sb = new StringBuilder(text);
                    while (BracketDepth(sb.ToString()) > 0)
                    {
                        if (pos >= lines.Count)
                        {
                            throw new YamlParseException(line.Number, "This '" + text[0] + "' is never closed.");
                        }

                        sb.Append(' ').Append(lines[pos].Text);
                        pos++;
                    }

                    var flow = new FlowParser(sb.ToString(), line.Number);
                    return flow.ParseAll();
                }

                YamlNode scalar = ParseScalar(text, line.Number);

                if (pos < lines.Count && lines[pos].Indent > indent)
                {
                    throw new YamlParseException(lines[pos].Number, "This line looks like a continuation of the value on line " + line.Number + ", but a value has to fit on one line. If the text contains ':' or '#', wrap it in quotes.");
                }

                return scalar;
            }

            private static int BracketDepth(string s)
            {
                int depth = 0;
                char quote = '\0';
                for (int i = 0; i < s.Length; i++)
                {
                    char c = s[i];
                    if (quote != '\0')
                    {
                        if (c == '\\' && quote == '"')
                        {
                            i++;
                        }
                        else if (c == quote)
                        {
                            quote = '\0';
                        }

                        continue;
                    }

                    if (c == '"' || c == '\'')
                    {
                        // Same rule as StripComment: only a quote at a value start opens one.
                        char prev = PreviousNonSpace(s, i);
                        if (prev == '\0' || prev == ':' || prev == '[' || prev == '{' || prev == ',' || prev == '-')
                        {
                            quote = c;
                        }
                    }
                    else if (c == '[' || c == '{')
                    {
                        depth++;
                    }
                    else if (c == ']' || c == '}')
                    {
                        depth--;
                    }
                }

                return depth;
            }

            private static char PreviousNonSpace(string s, int index)
            {
                for (int i = index - 1; i >= 0; i--)
                {
                    if (!char.IsWhiteSpace(s[i]))
                    {
                        return s[i];
                    }
                }

                return '\0';
            }

            // --- key: value splitting ---------------------------------------

            private static bool LooksLikeMapping(string text)
            {
                return text[0] != '[' && text[0] != '{' && TrySplitKey(text, out _, out _);
            }

            /// <summary>
            /// Splits "key: rest" at the first colon that is followed by a
            /// space (or ends the line). Handles quoted keys.
            /// </summary>
            private static bool TrySplitKey(string text, out string key, out string rest)
            {
                key = null;
                rest = null;

                if (text.Length == 0)
                {
                    return false;
                }

                int keyEnd;
                int colon;
                if (text[0] == '"' || text[0] == '\'')
                {
                    int close = FindClosingQuote(text, 0);
                    if (close < 0)
                    {
                        return false;
                    }

                    int i = close + 1;
                    while (i < text.Length && text[i] == ' ')
                    {
                        i++;
                    }

                    if (i >= text.Length || text[i] != ':' || (i + 1 < text.Length && text[i + 1] != ' '))
                    {
                        return false;
                    }

                    key = ParseScalar(text.Substring(0, close + 1), 0).Text ?? string.Empty;
                    colon = i;
                    keyEnd = close + 1;
                }
                else
                {
                    colon = -1;
                    for (int i = 0; i < text.Length; i++)
                    {
                        if (text[i] == ':' && (i + 1 == text.Length || text[i + 1] == ' '))
                        {
                            colon = i;
                            break;
                        }
                    }

                    if (colon <= 0)
                    {
                        return false;
                    }

                    key = text.Substring(0, colon).Trim();
                    keyEnd = colon;
                }

                if (key.Length == 0 && keyEnd == 0)
                {
                    return false;
                }

                rest = text.Substring(colon + 1).Trim();
                return true;
            }

            internal static int FindClosingQuote(string s, int open)
            {
                char quote = s[open];
                for (int i = open + 1; i < s.Length; i++)
                {
                    if (quote == '"' && s[i] == '\\')
                    {
                        i++;
                    }
                    else if (s[i] == quote)
                    {
                        if (quote == '\'' && i + 1 < s.Length && s[i + 1] == '\'')
                        {
                            i++;
                            continue;
                        }

                        return i;
                    }
                }

                return -1;
            }

            // --- scalars ------------------------------------------------------

            internal static YamlNode ParseScalar(string text, int line)
            {
                text = text.Trim();

                if (text.Length > 0 && (text[0] == '"' || text[0] == '\''))
                {
                    int close = FindClosingQuote(text, 0);
                    if (close < 0)
                    {
                        throw new YamlParseException(line, "The quote at the start of '" + Shorten(text) + "' is never closed.");
                    }

                    if (close != text.Length - 1)
                    {
                        throw new YamlParseException(line, "Unexpected text after the closing quote in '" + Shorten(text) + "'.");
                    }

                    string inner = text.Substring(1, close - 1);
                    string unquoted = text[0] == '"' ? UnescapeDouble(inner, line) : inner.Replace("''", "'");
                    return YamlNode.Scalar(unquoted, line);
                }

                if (text.Length > 0 && (text[0] == '|' || text[0] == '>'))
                {
                    throw new YamlParseException(line, "Multi-line text blocks ('" + text[0] + "') aren't supported. Put the text on one line, in quotes.");
                }

                if (text.Length > 0 && (text[0] == '&' || text[0] == '*' || text[0] == '!'))
                {
                    throw new YamlParseException(line, "YAML anchors, aliases and tags ('" + text[0] + "') aren't supported here.");
                }

                if (text.Length == 0 || text == "~" || text == "null" || text == "Null" || text == "NULL")
                {
                    return YamlNode.Scalar(null, line);
                }

                return YamlNode.Scalar(text, line);
            }

            private static string UnescapeDouble(string s, int line)
            {
                if (s.IndexOf('\\') < 0)
                {
                    return s;
                }

                var sb = new StringBuilder();
                for (int i = 0; i < s.Length; i++)
                {
                    if (s[i] != '\\')
                    {
                        sb.Append(s[i]);
                        continue;
                    }

                    i++;
                    if (i >= s.Length)
                    {
                        throw new YamlParseException(line, "A backslash at the end of a quoted text has nothing to escape. Use \\\\ for a literal backslash.");
                    }

                    switch (s[i])
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case '0': sb.Append('\0'); break;
                        case 'u':
                            if (i + 4 >= s.Length || !int.TryParse(s.Substring(i + 1, 4), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out int code))
                            {
                                throw new YamlParseException(line, "\\u needs four hex digits.");
                            }

                            sb.Append((char)code);
                            i += 4;
                            break;
                        default:
                            throw new YamlParseException(line, "Unknown escape '\\" + s[i] + "' in double-quoted text. To write a Windows path, use single quotes instead, or double the backslash (\\\\).");
                    }
                }

                return sb.ToString();
            }

            internal static string Shorten(string s)
            {
                return s.Length <= 40 ? s : s.Substring(0, 37) + "...";
            }
        }

        // --- Inline [a, b] / {k: v} ------------------------------------------

        private sealed class FlowParser
        {
            private readonly string s;
            private readonly int line;
            private int i;

            public FlowParser(string text, int line)
            {
                s = text;
                this.line = line;
            }

            public YamlNode ParseAll()
            {
                YamlNode node = ParseValue(false);
                SkipSpace();
                if (i < s.Length)
                {
                    throw new YamlParseException(line, "Unexpected '" + s[i] + "' after the closing bracket.");
                }

                return node;
            }

            private void SkipSpace()
            {
                while (i < s.Length && char.IsWhiteSpace(s[i]))
                {
                    i++;
                }
            }

            private YamlNode ParseValue(bool inMapValue)
            {
                SkipSpace();
                if (i >= s.Length)
                {
                    throw new YamlParseException(line, "The inline list/map ended unexpectedly.");
                }

                if (s[i] == '[')
                {
                    return ParseList();
                }

                if (s[i] == '{')
                {
                    return ParseMap();
                }

                return ParseFlowScalar(inMapValue, isKey: false);
            }

            private YamlNode ParseList()
            {
                var node = new YamlNode { Kind = YamlKind.Sequence, Items = new List<YamlNode>(), Line = line };
                i++; // [
                while (true)
                {
                    SkipSpace();
                    if (i >= s.Length)
                    {
                        throw new YamlParseException(line, "This '[' is never closed with a ']'.");
                    }

                    if (s[i] == ']')
                    {
                        i++;
                        return node;
                    }

                    node.Items.Add(ParseValue(false));
                    SkipSpace();
                    if (i < s.Length && s[i] == ',')
                    {
                        i++;
                    }
                    else if (i < s.Length && s[i] != ']')
                    {
                        throw new YamlParseException(line, "Expected ',' or ']' but found '" + s[i] + "'. Items in [ ] need a comma between them.");
                    }
                }
            }

            private YamlNode ParseMap()
            {
                var node = new YamlNode { Kind = YamlKind.Mapping, Entries = new List<YamlEntry>(), Line = line };
                i++; // {
                while (true)
                {
                    SkipSpace();
                    if (i >= s.Length)
                    {
                        throw new YamlParseException(line, "This '{' is never closed with a '}'.");
                    }

                    if (s[i] == '}')
                    {
                        i++;
                        return node;
                    }

                    string key = ParseFlowScalar(false, isKey: true).Text;
                    SkipSpace();
                    if (i >= s.Length || s[i] != ':')
                    {
                        throw new YamlParseException(line, "Expected ':' after '" + key + "' inside { }. Write it like { file: boom.wav, volume: 0.5 }.");
                    }

                    i++; // :
                    if (node.Find(key) != null)
                    {
                        throw new YamlParseException(line, "'" + key + "' appears twice inside the same { }.");
                    }

                    YamlNode value = ParseValue(true);
                    node.Entries.Add(new YamlEntry { Key = key, Line = line, Value = value });

                    SkipSpace();
                    if (i < s.Length && s[i] == ',')
                    {
                        i++;
                    }
                    else if (i < s.Length && s[i] != '}')
                    {
                        throw new YamlParseException(line, "Expected ',' or '}' but found '" + s[i] + "'. Items in { } need a comma between them.");
                    }
                }
            }

            private YamlNode ParseFlowScalar(bool inMapValue, bool isKey)
            {
                SkipSpace();
                if (i < s.Length && (s[i] == '"' || s[i] == '\''))
                {
                    int close = Parser.FindClosingQuote(s, i);
                    if (close < 0)
                    {
                        throw new YamlParseException(line, "The quote at '" + Parser.Shorten(s.Substring(i)) + "' is never closed.");
                    }

                    YamlNode quoted = Parser.ParseScalar(s.Substring(i, close - i + 1), line);
                    i = close + 1;
                    return quoted;
                }

                int start = i;
                while (i < s.Length)
                {
                    char c = s[i];
                    if (c == ',' || c == ']' || c == '}')
                    {
                        break;
                    }

                    if (isKey && c == ':' && (i + 1 >= s.Length || s[i + 1] == ' '))
                    {
                        break;
                    }

                    if ((c == '[' || c == '{') && i == start)
                    {
                        break;
                    }

                    i++;
                }

                string text = s.Substring(start, i - start).Trim();
                if (text.Length == 0 && !inMapValue)
                {
                    throw new YamlParseException(line, "Expected a value here but found '" + (i < s.Length ? s[i].ToString() : "end of line") + "'.");
                }

                return Parser.ParseScalar(text, line);
            }
        }
    }
}
