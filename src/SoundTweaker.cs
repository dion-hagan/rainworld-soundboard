using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SoundboardMod
{
    /// <summary>New numbers for one sound of an existing entry. Null means "leave that one alone".</summary>
    public sealed class SoundTweak
    {
        /// <summary>Which sound of the entry this is (SoundRef.Member).</summary>
        public int Member;

        /// <summary>The file the screen was showing for it; if the file has changed since, the edit is refused.</summary>
        public string File;

        /// <summary>The sound's own volume as written in the file (1 = as recorded).</summary>
        public float? Volume;

        /// <summary>The sound's own delay in seconds.</summary>
        public float? Delay;
    }

    /// <summary>What the options screen's "Edit Sound" page changes on one existing entry.</summary>
    public sealed class EntryTweak
    {
        /// <summary>SoundChoice.Id of the entry.</summary>
        public string ChoiceId;

        /// <summary>The entry's cooldown in seconds (0 = none); for a "together" group it covers the whole group.</summary>
        public float? Cooldown;

        public List<SoundTweak> Sounds = new List<SoundTweak>();
    }

    /// <summary>
    /// Changes the volume, delay and cooldown of entries that are already in soundboard.yaml.
    /// Like SoundAdder it only produces new text, and re-reads the result with the real parser
    /// to check that it says exactly what was asked - the target changed as requested and
    /// nothing else in the whole config moved - before handing it out.
    /// </summary>
    public static class SoundTweaker
    {
        // Anything closer than this to the current value is "no change" (and isn't written).
        private const float Tolerance = 0.0005f;

        /// <summary>
        /// The new text, which is the same as the old when nothing needed changing.
        /// Fails, leaving the text alone, if the entry or a sound isn't what the screen thought it was.
        /// </summary>
        public static YamlEditor.Result Apply(string text, EntryTweak tweak, EventCatalog catalog)
        {
            SoundboardConfig before = SoundboardConfigParser.Parse(text, catalog);
            if (before.Failed)
            {
                return YamlEditor.Result.Failure(text, "soundboard.yaml has an error (" + before.Issues[0] + ") - fix that first");
            }

            SoundChoice entry = before.Events.SelectMany(e => e.Choices).FirstOrDefault(c => c.Id == tweak.ChoiceId);
            if (entry == null)
            {
                return YamlEditor.Result.Failure(text, "that entry is no longer in soundboard.yaml (press RELOAD CONFIG)");
            }

            var edits = new List<YamlEditor.OptionEdit>();

            foreach (SoundTweak change in tweak.Sounds)
            {
                SoundRef sound = entry.Sounds.FirstOrDefault(s => s.Member == change.Member);
                if (sound == null || !string.Equals(sound.File, change.File, StringComparison.Ordinal))
                {
                    return YamlEditor.Result.Failure(text, "soundboard.yaml has changed since the screen was opened (press RELOAD CONFIG)");
                }

                if (change.Volume.HasValue)
                {
                    float volume = change.Volume.Value;
                    if (float.IsNaN(volume) || volume < 0f || volume > NewSound.MaxVolume)
                    {
                        return YamlEditor.Result.Failure(text, "the volume must be between 0 and " + Format(NewSound.MaxVolume));
                    }

                    if (Math.Abs(volume - sound.OwnVolume) > Tolerance)
                    {
                        edits.Add(new YamlEditor.OptionEdit { Line = sound.Line, Key = "volume", Value = Format(volume) });
                    }
                }

                if (change.Delay.HasValue)
                {
                    float delay = change.Delay.Value;
                    if (float.IsNaN(delay) || delay < 0f || delay > NewSound.MaxDelay)
                    {
                        return YamlEditor.Result.Failure(text, "the delay must be between 0 and " + Format(NewSound.MaxDelay) + " seconds");
                    }

                    if (Math.Abs(delay - sound.OwnDelay) > Tolerance)
                    {
                        edits.Add(new YamlEditor.OptionEdit { Line = sound.Line, Key = "delay", Value = Format(delay) });
                    }
                }
            }

            // Last, so on a single entry (where sound and entry are the same item) the cooldown is written after volume and delay.
            if (tweak.Cooldown.HasValue)
            {
                float cooldown = tweak.Cooldown.Value;
                if (float.IsNaN(cooldown) || cooldown < 0f || cooldown > NewSound.MaxCooldown)
                {
                    return YamlEditor.Result.Failure(text, "the cooldown must be between 0 and " + Format(NewSound.MaxCooldown) + " seconds");
                }

                if (Math.Abs(cooldown - entry.Cooldown) > Tolerance)
                {
                    edits.Add(new YamlEditor.OptionEdit { Line = entry.Line, Key = "cooldown", Value = Format(cooldown) });
                }
            }

            if (edits.Count == 0)
            {
                return YamlEditor.Result.Success(text);
            }

            YamlEditor.Result edited = YamlEditor.SetOptions(text, edits);
            if (!edited.Ok)
            {
                return edited;
            }

            string problem = Verify(before, SoundboardConfigParser.Parse(edited.Text, catalog), entry.Id, edits);
            return problem == null
                ? edited
                : YamlEditor.Result.Failure(text, "the edit didn't come out as intended (" + problem + "), so nothing was changed");
        }

        /// <summary>
        /// Null if 'after' is 'before' with exactly the requested numbers changed on the one entry
        /// (every other entry, event and sound identical); otherwise what's off.
        /// </summary>
        private static string Verify(SoundboardConfig before, SoundboardConfig after, string entryId, List<YamlEditor.OptionEdit> edits)
        {
            if (after.Failed)
            {
                return "the new file no longer reads: " + after.Issues.FirstOrDefault();
            }

            if (after.Issues.Count(i => i.Severity == IssueSeverity.Error) > before.Issues.Count(i => i.Severity == IssueSeverity.Error))
            {
                return "it introduced a problem: " + after.Issues.First(i => i.Severity == IssueSeverity.Error);
            }

            List<SoundChoice> was = before.Events.SelectMany(e => e.Choices).ToList();
            List<SoundChoice> now = after.Events.SelectMany(e => e.Choices).ToList();
            if (was.Count != now.Count)
            {
                return "the number of entries changed";
            }

            for (int i = 0; i < was.Count; i++)
            {
                SoundChoice a = was[i];
                SoundChoice b = now[i];
                bool target = a.Id == entryId;

                if (a.Id != b.Id || a.Enabled != b.Enabled || a.Sounds.Count != b.Sounds.Count)
                {
                    return "entry '" + a.Label + "' changed";
                }

                // A sound's line moves when lines are added above it, so sounds are compared by position.
                float expectedCooldown = a.Cooldown;
                if (target)
                {
                    YamlEditor.OptionEdit cooldownEdit = edits.FirstOrDefault(e => e.Line == a.Line && e.Key == "cooldown");
                    if (cooldownEdit != null)
                    {
                        expectedCooldown = Parse(cooldownEdit.Value);
                    }
                }

                if (Math.Abs(b.Cooldown - expectedCooldown) > 0.006f)
                {
                    return "the cooldown of '" + a.Label + "' is " + Format(b.Cooldown) + " instead of " + Format(expectedCooldown);
                }

                for (int s = 0; s < a.Sounds.Count; s++)
                {
                    SoundRef x = a.Sounds[s];
                    SoundRef y = b.Sounds[s];
                    float expectedVolume = x.OwnVolume;
                    float expectedDelay = x.OwnDelay;
                    if (target)
                    {
                        YamlEditor.OptionEdit volumeEdit = edits.FirstOrDefault(e => e.Line == x.Line && e.Key == "volume");
                        YamlEditor.OptionEdit delayEdit = edits.FirstOrDefault(e => e.Line == x.Line && e.Key == "delay");
                        if (volumeEdit != null)
                        {
                            expectedVolume = Parse(volumeEdit.Value);
                        }

                        if (delayEdit != null)
                        {
                            expectedDelay = Parse(delayEdit.Value);
                        }
                    }

                    if (!string.Equals(x.File, y.File, StringComparison.Ordinal)
                        || Math.Abs(y.OwnVolume - expectedVolume) > 0.006f
                        || Math.Abs(y.OwnDelay - expectedDelay) > 0.006f)
                    {
                        return "sound '" + x.File + "' of '" + a.Label + "' doesn't have the expected numbers";
                    }
                }
            }

            return null;
        }

        private static float Parse(string value)
        {
            return float.Parse(value, CultureInfo.InvariantCulture);
        }

        private static string Format(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
