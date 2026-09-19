using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SoundboardMod
{
    /// <summary>A sound the player picked on the options screen's "Add Sound" page.</summary>
    public sealed class NewSound
    {
        public const float MaxVolume = 10f;
        public const float MaxDelay = 120f;

        /// <summary>Any spelling the config accepts ("player death" works); written out in its canonical form.</summary>
        public string EventName;

        /// <summary>File name relative to the sounds folder, e.g. "vine-boom.wav" or "memes/vine-boom.wav".</summary>
        public string File;

        /// <summary>1 = as recorded.</summary>
        public float Volume = 1f;

        /// <summary>Seconds to wait after the event before playing.</summary>
        public float Delay;
    }

    /// <summary>
    /// Adds a NewSound to the text of soundboard.yaml. Nothing is ever written
    /// to disk from here - this only produces the new text - and the result is
    /// read back with the real parser to make sure it says exactly what was
    /// asked for before it's handed out, so a bug in the line editing can't
    /// damage a player's file.
    /// </summary>
    public static class SoundAdder
    {
        // Amounts closer than this to "nothing special" aren't written: volume 1 and delay 0 are the defaults.
        private const float Tolerance = 0.0005f;

        public static YamlEditor.Result Add(string text, NewSound sound, EventCatalog catalog)
        {
            string canonical = catalog.Resolve(sound.EventName ?? string.Empty);
            if (canonical == null)
            {
                return YamlEditor.Result.Failure(text, "'" + sound.EventName + "' isn't an event this mod knows");
            }

            string file = (sound.File ?? string.Empty).Trim().Replace('\\', '/');
            if (file.Length == 0 || file.StartsWith("/") || (file.Length > 1 && file[1] == ':') || file.Split('/').Contains(".."))
            {
                return YamlEditor.Result.Failure(text, "'" + sound.File + "' isn't a usable sound file name");
            }

            if (float.IsNaN(sound.Volume) || sound.Volume < 0f || sound.Volume > NewSound.MaxVolume)
            {
                return YamlEditor.Result.Failure(text, "the volume must be between 0 and " + Format(NewSound.MaxVolume));
            }

            if (float.IsNaN(sound.Delay) || sound.Delay < 0f || sound.Delay > NewSound.MaxDelay)
            {
                return YamlEditor.Result.Failure(text, "the delay must be between 0 and " + Format(NewSound.MaxDelay) + " seconds");
            }

            SoundboardConfig before = SoundboardConfigParser.Parse(text, catalog);
            if (before.Failed)
            {
                return YamlEditor.Result.Failure(text, "soundboard.yaml has an error (" + before.Issues[0] + ") - fix that first");
            }

            var options = new List<KeyValuePair<string, string>>();
            if (Math.Abs(sound.Volume - 1f) > Tolerance)
            {
                options.Add(new KeyValuePair<string, string>("volume", Format(sound.Volume)));
            }

            if (sound.Delay > Tolerance)
            {
                options.Add(new KeyValuePair<string, string>("delay", Format(sound.Delay)));
            }

            YamlEditor.Result edited = YamlEditor.AddSound(text, canonical, file, options);
            if (!edited.Ok)
            {
                return edited;
            }

            string problem = Verify(before, SoundboardConfigParser.Parse(edited.Text, catalog), canonical, file, sound);
            return problem == null
                ? edited
                : YamlEditor.Result.Failure(text, "the edit didn't come out as intended (" + problem + "), so nothing was changed");
        }

        /// <summary>Null if 'after' is 'before' plus exactly the requested sound at the end of the event's list; otherwise what's off.</summary>
        private static string Verify(SoundboardConfig before, SoundboardConfig after, string eventName, string file, NewSound sound)
        {
            if (after.Failed)
            {
                return "the new file no longer reads: " + after.Issues.FirstOrDefault();
            }

            int errorsBefore = before.Issues.Count(i => i.Severity == IssueSeverity.Error);
            int errorsAfter = after.Issues.Count(i => i.Severity == IssueSeverity.Error);
            if (errorsAfter > errorsBefore)
            {
                return "it introduced a problem: " + after.Issues.First(i => i.Severity == IssueSeverity.Error);
            }

            string wanted = NameMatch.Normalize(eventName);
            EventBinding eventBefore = before.Events.FirstOrDefault(e => NameMatch.Normalize(e.EventName) == wanted);
            EventBinding eventAfter = after.Events.FirstOrDefault(e => NameMatch.Normalize(e.EventName) == wanted);
            int countBefore = eventBefore?.Choices.Count ?? 0;
            if (eventAfter == null || eventAfter.Choices.Count != countBefore + 1)
            {
                return "the event's list has " + (eventAfter?.Choices.Count ?? 0) + " sound(s) instead of " + (countBefore + 1);
            }

            // Every other event must be unchanged.
            foreach (EventBinding other in before.Events)
            {
                if (NameMatch.Normalize(other.EventName) == wanted)
                {
                    continue;
                }

                EventBinding same = after.Events.FirstOrDefault(e => NameMatch.Normalize(e.EventName) == NameMatch.Normalize(other.EventName));
                if (same == null || same.Choices.Count != other.Choices.Count)
                {
                    return "another event ('" + other.EventName + "') changed";
                }
            }

            SoundChoice added = eventAfter.Choices.Last();
            SoundRef reference = added.Sounds.Count == 1 ? added.Sounds[0] : null;
            if (reference == null
                || !string.Equals(reference.File, file, StringComparison.Ordinal)
                || Math.Abs(reference.Volume - sound.Volume) > 0.006f
                || Math.Abs(reference.Delay - sound.Delay) > 0.006f)
            {
                return "the added entry doesn't match what was asked for";
            }

            return null;
        }

        private static string Format(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
