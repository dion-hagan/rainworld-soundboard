using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SoundboardMod
{
    /// <summary>
    /// Where the config and sounds live. The mod folder itself gets
    /// overwritten whenever the mod updates (Steam Workshop replaces it), so
    /// anything a player edits has to live somewhere else: a "Soundboard"
    /// folder in the game's user-data area. The first run copies the default
    /// soundboard.yaml there; sounds the config mentions are looked for in the
    /// user's sounds folder first, then in the sounds bundled with the mod.
    /// </summary>
    public sealed class ConfigLocations
    {
        public const string ConfigFileName = "soundboard.yaml";
        public const string SoundsFolderName = "sounds";
        public const string EventListFileName = "events.txt";

        public string UserFolder { get; }
        public string UserConfigPath { get; }
        public string UserSoundsFolder { get; }
        public string BundledConfigPath { get; }
        public string BundledSoundsFolder { get; }

        /// <summary>Folders searched for sound files, in priority order.</summary>
        public IReadOnlyList<string> SoundFolders => new[] { UserSoundsFolder, BundledSoundsFolder };

        public ConfigLocations(string modRoot, string userFolder)
        {
            UserFolder = userFolder;
            UserConfigPath = Path.Combine(userFolder, ConfigFileName);
            UserSoundsFolder = Path.Combine(userFolder, SoundsFolderName);
            BundledConfigPath = Path.Combine(modRoot, ConfigFileName);
            BundledSoundsFolder = Path.Combine(modRoot, SoundsFolderName);
        }

        /// <summary>
        /// Creates the user's folders and, if they have no config yet, gives
        /// them a copy of the default one to start from. Returns false (and
        /// says why) if the user folder can't be written - the mod then just
        /// runs from the bundled files.
        /// </summary>
        public bool TryEnsureUserFiles(out string problem)
        {
            problem = null;
            try
            {
                Directory.CreateDirectory(UserSoundsFolder);
                if (!File.Exists(UserConfigPath) && File.Exists(BundledConfigPath))
                {
                    File.Copy(BundledConfigPath, UserConfigPath);
                }

                return true;
            }
            catch (Exception e)
            {
                problem = e.Message;
                return false;
            }
        }

        /// <summary>The text of the config to use: the user's file if there is one, otherwise the bundled default.</summary>
        public string ReadConfig(out string path)
        {
            path = File.Exists(UserConfigPath) ? UserConfigPath : BundledConfigPath;
            return File.ReadAllText(path);
        }

        public string ReadBundledConfig()
        {
            return File.ReadAllText(BundledConfigPath);
        }

        /// <summary>
        /// True if the soundboard.yaml inside the mod's folder was changed more
        /// recently than the player's own copy and says something different.
        /// That file is only a template (the game reads the personal copy, which
        /// is never overwritten), so this is the situation where someone has
        /// probably edited the wrong file - or a mod update brought new defaults.
        /// </summary>
        public bool TemplateIsNewerThanUserCopy()
        {
            try
            {
                if (!File.Exists(UserConfigPath) || !File.Exists(BundledConfigPath))
                {
                    return false;
                }

                if (File.GetLastWriteTimeUtc(BundledConfigPath) <= File.GetLastWriteTimeUtc(UserConfigPath))
                {
                    return false;
                }

                return Normalize(File.ReadAllText(BundledConfigPath)) != Normalize(File.ReadAllText(UserConfigPath));
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string Normalize(string text)
        {
            return text.Replace("\r\n", "\n").TrimEnd();
        }

        /// <summary>Writes a file only if its content would change (avoids touching the disk every launch).</summary>
        public static void WriteIfChanged(string path, string content)
        {
            if (File.Exists(path) && File.ReadAllText(path) == content)
            {
                return;
            }

            File.WriteAllText(path, content);
        }
    }

    /// <summary>
    /// Finds the audio file behind every sound in a parsed config and drops
    /// the ones that can't be played, leaving a friendly issue for each
    /// (missing file, wrong type, probable typo in the name).
    /// </summary>
    public static class SoundFileResolver
    {
        public static readonly string[] SupportedExtensions = { ".wav", ".ogg", ".mp3" };

        public static void Resolve(SoundboardConfig config, IReadOnlyList<string> folders)
        {
            foreach (EventBinding binding in config.Events)
            {
                foreach (SoundChoice choice in binding.Choices)
                {
                    foreach (SoundRef sound in choice.Sounds)
                    {
                        sound.ResolvedPath = Find(sound.File, folders, out string problem);
                        if (sound.ResolvedPath == null)
                        {
                            config.AddIssue(IssueSeverity.Error, sound.Line, problem);
                        }
                    }

                    choice.Sounds.RemoveAll(s => s.ResolvedPath == null);
                }

                binding.Choices.RemoveAll(c => c.Sounds.Count == 0);
            }

            config.Events.RemoveAll(e => e.Choices.Count == 0);
        }

        internal static string Find(string file, IReadOnlyList<string> folders, out string problem)
        {
            problem = null;
            string extension = Path.GetExtension(file);
            bool hasExtension = extension.Length > 0;

            if (hasExtension && !SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                // "1.5" and "v2" style names have a dot but no real extension - only complain about audio-looking ones.
                if (extension.Length <= 5 && extension.Skip(1).All(char.IsLetterOrDigit) && extension.Skip(1).Any(char.IsLetter))
                {
                    problem = "'" + file + "' isn't a supported audio type. Use .wav, .ogg or .mp3 (a free tool like Audacity can convert it).";
                    return null;
                }

                hasExtension = false;
            }

            foreach (string folder in folders)
            {
                if (hasExtension)
                {
                    string candidate = Path.GetFullPath(Path.Combine(folder, file));
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                else
                {
                    foreach (string ext in SupportedExtensions)
                    {
                        string candidate = Path.GetFullPath(Path.Combine(folder, file + ext));
                        if (File.Exists(candidate))
                        {
                            return candidate;
                        }
                    }
                }
            }

            problem = "Couldn't find the sound file '" + file + "'." + NearMiss(file, folders) + " Put audio files in: " + folders[0];
            return null;
        }

        private static string NearMiss(string file, IReadOnlyList<string> folders)
        {
            try
            {
                string wanted = Path.GetFileNameWithoutExtension(file);
                var names = new List<string>();
                foreach (string folder in folders)
                {
                    if (Directory.Exists(folder))
                    {
                        names.AddRange(Directory.GetFiles(folder)
                            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                            .Select(Path.GetFileName));
                    }
                }

                string closest = NameMatch.Closest(wanted, names.Select(Path.GetFileNameWithoutExtension));
                return closest == null ? string.Empty : " Did you mean '" + names.First(n => Path.GetFileNameWithoutExtension(n) == closest) + "'?";
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}
