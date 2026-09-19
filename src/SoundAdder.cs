using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SoundboardMod
{
    /// <summary>One sound in a NewSound: a file with its own volume and delay.</summary>
    public sealed class NewSoundPart
    {
        /// <summary>File name relative to the sounds folder, e.g. "vine-boom.wav" or "memes/vine-boom.wav".</summary>
        public string File;

        /// <summary>1 = as recorded.</summary>
        public float Volume = 1f;

        /// <summary>Seconds to wait after the event before playing.</summary>
        public float Delay;
    }

    /// <summary>
    /// What the player picked on the options screen's "Add Sound" page: one sound, or - with
    /// Together - a group of two or more that all play at once as a single step of the event's rotation.
    /// </summary>
    public sealed class NewSound
    {
        public const float MaxVolume = 10f;
        public const float MaxDelay = 120f;

        /// <summary>Any spelling the config accepts ("player death" works); written out in its canonical form.</summary>
        public string EventName;

        /// <summary>True for a "together:" group (needs at least two parts); false for a single sound (exactly one part).</summary>
        public bool Together;

        public List<NewSoundPart> Parts = new List<NewSoundPart>();

        public static NewSound Single(string eventName, string file, float volume = 1f, float delay = 0f)
        {
            var sound = new NewSound { EventName = eventName };
            sound.Parts.Add(new NewSoundPart { File = file, Volume = volume, Delay = delay });
            return sound;
        }

        /// <summary>The name shown in the options menu for this entry ("Vine Boom", or "Boom + Crash" for a group).</summary>
        public string Label => string.Join(" + ", Parts.Select(p => SoundboardConfigParser.PrettyName(p.File ?? string.Empty)));
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

            if (sound.Parts == null || sound.Parts.Count == 0)
            {
                return YamlEditor.Result.Failure(text, "no sound was chosen");
            }

            if (sound.Together && sound.Parts.Count < 2)
            {
                return YamlEditor.Result.Failure(text, "a group needs at least two sounds");
            }

            if (!sound.Together && sound.Parts.Count != 1)
            {
                return YamlEditor.Result.Failure(text, "several sounds can only be added as a 'together' group");
            }

            var parts = new List<YamlEditor.Part>();
            var files = new List<string>();
            foreach (NewSoundPart part in sound.Parts)
            {
                string file = (part.File ?? string.Empty).Trim().Replace('\\', '/');
                if (file.Length == 0 || file.StartsWith("/") || (file.Length > 1 && file[1] == ':') || file.Split('/').Contains(".."))
                {
                    return YamlEditor.Result.Failure(text, "'" + part.File + "' isn't a usable sound file name");
                }

                if (float.IsNaN(part.Volume) || part.Volume < 0f || part.Volume > NewSound.MaxVolume)
                {
                    return YamlEditor.Result.Failure(text, "the volume must be between 0 and " + Format(NewSound.MaxVolume));
                }

                if (float.IsNaN(part.Delay) || part.Delay < 0f || part.Delay > NewSound.MaxDelay)
                {
                    return YamlEditor.Result.Failure(text, "the delay must be between 0 and " + Format(NewSound.MaxDelay) + " seconds");
                }

                var options = new List<KeyValuePair<string, string>>();
                if (Math.Abs(part.Volume - 1f) > Tolerance)
                {
                    options.Add(new KeyValuePair<string, string>("volume", Format(part.Volume)));
                }

                if (part.Delay > Tolerance)
                {
                    options.Add(new KeyValuePair<string, string>("delay", Format(part.Delay)));
                }

                parts.Add(new YamlEditor.Part { File = file, Options = options });
                files.Add(file);
            }

            SoundboardConfig before = SoundboardConfigParser.Parse(text, catalog);
            if (before.Failed)
            {
                return YamlEditor.Result.Failure(text, "soundboard.yaml has an error (" + before.Issues[0] + ") - fix that first");
            }

            YamlEditor.Result edited = YamlEditor.AddSound(text, canonical, parts, sound.Together);
            if (!edited.Ok)
            {
                return edited;
            }

            string problem = Verify(before, SoundboardConfigParser.Parse(edited.Text, catalog), canonical, files, sound);
            return problem == null
                ? edited
                : YamlEditor.Result.Failure(text, "the edit didn't come out as intended (" + problem + "), so nothing was changed");
        }

        /// <summary>Null if 'after' is 'before' plus exactly the requested entry at the end of the event's list; otherwise what's off.</summary>
        private static string Verify(SoundboardConfig before, SoundboardConfig after, string eventName, List<string> files, NewSound sound)
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
                return "the event's list has " + (eventAfter?.Choices.Count ?? 0) + " entries instead of " + (countBefore + 1);
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

            // The new entry: one choice holding the requested sounds, in order, each as asked.
            SoundChoice added = eventAfter.Choices.Last();
            if (added.Sounds.Count != files.Count)
            {
                return "the added entry has " + added.Sounds.Count + " sound(s) instead of " + files.Count;
            }

            for (int i = 0; i < files.Count; i++)
            {
                SoundRef reference = added.Sounds[i];
                NewSoundPart part = sound.Parts[i];
                if (!string.Equals(reference.File, files[i], StringComparison.Ordinal)
                    || Math.Abs(reference.Volume - part.Volume) > 0.006f
                    || Math.Abs(reference.Delay - part.Delay) > 0.006f)
                {
                    return "the added entry doesn't match what was asked for";
                }
            }

            return null;
        }

        private static string Format(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
