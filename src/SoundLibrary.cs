using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SoundboardMod
{
    /// <summary>
    /// The audio files a player can pick from on the "Add Sound" page: every
    /// .wav / .ogg / .mp3 in the sounds folders, named the way soundboard.yaml
    /// names them (relative to the sounds folder, forward slashes) - so a name
    /// from this list is always one SoundFileResolver will find again.
    /// </summary>
    public static class SoundLibrary
    {
        /// <summary>Enough for any sensible collection; keeps the dropdown (and the scan) bounded if the folder is huge.</summary>
        public const int MaxFiles = 3000;

        /// <summary>
        /// Every playable file in the folders, sorted by name. If the same relative name
        /// exists in several folders it's listed once - the first folder wins, exactly as
        /// when the game looks a file up.
        /// </summary>
        public static List<string> List(IReadOnlyList<string> folders)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var found = new List<string>();

            foreach (string folder in folders)
            {
                try
                {
                    if (!Directory.Exists(folder))
                    {
                        continue;
                    }

                    string root = Path.GetFullPath(folder).TrimEnd('\\', '/');
                    foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                    {
                        if (!SoundFileResolver.SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        string relative = path.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');
                        if (relative.Length > 0 && seen.Add(relative))
                        {
                            found.Add(relative);
                        }

                        if (found.Count >= MaxFiles)
                        {
                            return Sorted(found);
                        }
                    }
                }
                catch (Exception)
                {
                    // An unreadable folder (permissions, removed drive) just contributes nothing.
                }
            }

            return Sorted(found);
        }

        private static List<string> Sorted(List<string> files)
        {
            return files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ThenBy(f => f, StringComparer.Ordinal).ToList();
        }
    }
}
