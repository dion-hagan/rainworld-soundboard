using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SoundboardMod
{
    /// <summary>
    /// Minimal, dependency-free JSON reader. Parses into plain
    /// Dictionary&lt;string, object&gt; / List&lt;object&gt; / string / double / bool / null.
    /// Written to replace UnityEngine.JsonUtility, which was silently failing
    /// to populate nested arrays for meta.json in-game with no reproducible
    /// cause outside a live Unity process - this can be unit-tested standalone.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string json)
        {
            int i = 0;
            SkipWhitespace(json, ref i);
            object result = ParseValue(json, ref i);
            SkipWhitespace(json, ref i);
            if (i != json.Length)
            {
                throw new FormatException($"Unexpected trailing content at index {i}.");
            }
            return result;
        }

        private static object ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length)
            {
                throw new FormatException("Unexpected end of JSON.");
            }

            switch (s[i])
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return ParseString(s, ref i);
                case 't':
                    Expect(s, ref i, "true");
                    return true;
                case 'f':
                    Expect(s, ref i, "false");
                    return false;
                case 'n':
                    Expect(s, ref i, "null");
                    return null;
                default:
                    return ParseNumber(s, ref i);
            }
        }

        private static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            var result = new Dictionary<string, object>();
            i++; // '{'
            SkipWhitespace(s, ref i);
            if (Peek(s, i) == '}')
            {
                i++;
                return result;
            }

            while (true)
            {
                SkipWhitespace(s, ref i);
                string key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);
                if (Peek(s, i) != ':')
                {
                    throw new FormatException($"Expected ':' at index {i}.");
                }
                i++; // ':'
                object value = ParseValue(s, ref i);
                result[key] = value;

                SkipWhitespace(s, ref i);
                char c = Peek(s, i);
                if (c == ',')
                {
                    i++;
                    continue;
                }
                if (c == '}')
                {
                    i++;
                    break;
                }
                throw new FormatException($"Expected ',' or '}}' at index {i}.");
            }

            return result;
        }

        private static List<object> ParseArray(string s, ref int i)
        {
            var result = new List<object>();
            i++; // '['
            SkipWhitespace(s, ref i);
            if (Peek(s, i) == ']')
            {
                i++;
                return result;
            }

            while (true)
            {
                object value = ParseValue(s, ref i);
                result.Add(value);

                SkipWhitespace(s, ref i);
                char c = Peek(s, i);
                if (c == ',')
                {
                    i++;
                    continue;
                }
                if (c == ']')
                {
                    i++;
                    break;
                }
                throw new FormatException($"Expected ',' or ']' at index {i}.");
            }

            return result;
        }

        private static string ParseString(string s, ref int i)
        {
            if (Peek(s, i) != '"')
            {
                throw new FormatException($"Expected '\"' at index {i}.");
            }
            i++; // opening quote

            var sb = new StringBuilder();
            while (true)
            {
                if (i >= s.Length)
                {
                    throw new FormatException("Unterminated string.");
                }

                char c = s[i++];
                if (c == '"')
                {
                    break;
                }

                if (c == '\\')
                {
                    if (i >= s.Length)
                    {
                        throw new FormatException("Unterminated escape sequence.");
                    }

                    char escaped = s[i++];
                    switch (escaped)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 > s.Length)
                            {
                                throw new FormatException("Invalid unicode escape.");
                            }
                            string hex = s.Substring(i, 4);
                            sb.Append((char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            i += 4;
                            break;
                        default:
                            throw new FormatException($"Invalid escape character '\\{escaped}'.");
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        private static double ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.' || s[i] == 'e' || s[i] == 'E'))
            {
                i++;
            }

            string token = s.Substring(start, i - start);
            if (token.Length == 0)
            {
                throw new FormatException($"Expected a value at index {i}.");
            }

            return double.Parse(token, CultureInfo.InvariantCulture);
        }

        private static void Expect(string s, ref int i, string literal)
        {
            if (i + literal.Length > s.Length || s.Substring(i, literal.Length) != literal)
            {
                throw new FormatException($"Expected '{literal}' at index {i}.");
            }
            i += literal.Length;
        }

        private static char Peek(string s, int i)
        {
            return i < s.Length ? s[i] : '\0';
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i]))
            {
                i++;
            }
        }
    }
}
