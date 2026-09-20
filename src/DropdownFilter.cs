using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SoundboardMod
{
    /// <summary>
    /// The matching and ordering behind the search in the Add/Edit Sound dropdowns, kept free of
    /// the game so it can be tested. Built once from each item's searchable texts, then asked
    /// for the items matching whatever has been typed so far.
    ///
    /// A query is split into words at spaces, and an item matches when every word occurs
    /// somewhere in one of its texts, ignoring case and anything that isn't a letter or digit
    /// (the way <see cref="NameMatch.Normalize"/> sees names) - so "player death", "playerdeath"
    /// and "PLAYER_DEATH" all find PlayerDeath, and "boom1" finds boom_1.wav. Matches come
    /// back best first: a text that starts with the word, then one with a word (or a CamelCase
    /// part) starting with it, then anywhere else; a text that IS the query beats a longer one;
    /// and among equals the original order of the list is kept.
    /// </summary>
    public sealed class DropdownFilter
    {
        private readonly Field[][] items;

        /// <summary>One array of searchable texts per item (e.g. its name and its shown name).</summary>
        public DropdownFilter(IReadOnlyList<string[]> texts)
        {
            items = new Field[texts.Count][];
            for (int i = 0; i < items.Length; i++)
            {
                string[] itemTexts = texts[i] ?? new string[0];
                var fields = new List<Field>(itemTexts.Length);
                foreach (string text in itemTexts)
                {
                    if (!string.IsNullOrEmpty(text))
                    {
                        fields.Add(new Field(text));
                    }
                }

                items[i] = fields.ToArray();
            }
        }

        public int Count => items.Length;

        /// <summary>
        /// Indexes of the matching items, best first. A blank query matches everything,
        /// in the original order.
        /// </summary>
        public List<int> Search(string query)
        {
            var result = new List<int>();
            string[] words = (query ?? string.Empty).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                for (int i = 0; i < items.Length; i++)
                {
                    result.Add(i);
                }

                return result;
            }

            var tokens = new Token[words.Length];
            for (int i = 0; i < words.Length; i++)
            {
                tokens[i] = new Token(words[i]);
            }

            var hits = new List<Hit>();
            for (int i = 0; i < items.Length; i++)
            {
                int best = -1;
                foreach (Field field in items[i])
                {
                    int score = Score(field, tokens);
                    if (score >= 0 && (best < 0 || score < best))
                    {
                        best = score;
                    }
                }

                if (best >= 0)
                {
                    hits.Add(new Hit { Index = i, Score = best });
                }
            }

            hits.Sort((a, b) => a.Score != b.Score ? a.Score.CompareTo(b.Score) : a.Index.CompareTo(b.Index));
            foreach (Hit hit in hits)
            {
                result.Add(hit.Index);
            }

            return result;
        }

        /// <summary>The same for a plain list of texts: the indexes of those matching query, best first.</summary>
        public static List<int> Filter(IReadOnlyList<string> texts, string query)
        {
            var wrapped = new string[texts.Count][];
            for (int i = 0; i < wrapped.Length; i++)
            {
                wrapped[i] = new[] { texts[i] };
            }

            return new DropdownFilter(wrapped).Search(query);
        }

        // Lower is better; -1 means the text doesn't match every word.
        private static int Score(Field field, Token[] tokens)
        {
            int score = 0;
            foreach (Token token in tokens)
            {
                int tier = Tier(field, token);
                if (tier < 0)
                {
                    return -1;
                }

                score += tier;
            }

            // "player death" is as exact for PlayerDeath as "playerdeath" is.
            bool exact = field.Compact.Length > 0 && field.Compact == string.Concat(tokens.Select(t => t.Compact));
            return score * 2 + (exact ? 0 : 1);
        }

        // 0 = the word is at the very start of the text, 1 = at the start of a word or CamelCase
        // part, 2 = somewhere inside one; -1 = not there at all.
        private static int Tier(Field field, Token token)
        {
            if (token.Compact.Length == 0)
            {
                // Nothing but punctuation was typed (e.g. "."): fall back to the text as written.
                return field.Lower.IndexOf(token.Raw, StringComparison.Ordinal) >= 0 ? 2 : -1;
            }

            int at = field.Compact.IndexOf(token.Compact, StringComparison.Ordinal);
            if (at < 0)
            {
                return -1;
            }

            int tier = 2;
            while (at >= 0)
            {
                if (at == 0)
                {
                    return 0;
                }

                if (field.WordStart[at])
                {
                    tier = 1;
                }

                at = field.Compact.IndexOf(token.Compact, at + 1, StringComparison.Ordinal);
            }

            return tier;
        }

        private struct Hit
        {
            public int Index;
            public int Score;
        }

        private sealed class Token
        {
            public readonly string Raw;
            public readonly string Compact;

            public Token(string word)
            {
                Raw = word.ToLowerInvariant();
                Compact = NameMatch.Normalize(word);
            }
        }

        private sealed class Field
        {
            public readonly string Lower;

            /// <summary>The text as <see cref="NameMatch.Normalize"/> sees it: lower-case letters and digits only.</summary>
            public readonly string Compact;

            /// <summary>For each character of Compact, whether it begins a word or CamelCase part of the original.</summary>
            public readonly bool[] WordStart;

            public Field(string text)
            {
                Lower = text.ToLowerInvariant();

                var compact = new StringBuilder(text.Length);
                var starts = new List<bool>(text.Length);
                bool afterBreak = true;
                for (int i = 0; i < text.Length; i++)
                {
                    char c = text[i];
                    if (!char.IsLetterOrDigit(c))
                    {
                        afterBreak = true;
                        continue;
                    }

                    // "PlayerDeath" -> P and D; "XMLParser" -> X and P; "boom_1" -> b and 1.
                    bool camel = i > 0 && char.IsUpper(c) && (char.IsLower(text[i - 1]) || char.IsDigit(text[i - 1])
                        || (char.IsUpper(text[i - 1]) && i + 1 < text.Length && char.IsLower(text[i + 1])));
                    starts.Add(afterBreak || camel);
                    compact.Append(char.ToLowerInvariant(c));
                    afterBreak = false;
                }

                Compact = compact.ToString();
                WordStart = starts.ToArray();
            }
        }
    }
}
