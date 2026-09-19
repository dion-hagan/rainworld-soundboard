using System;
using System.Collections.Generic;

namespace SoundboardMod
{
    /// <summary>
    /// Decides which sound group plays next for an event: steps through the
    /// groups in order and wraps back to the start, so the same sound isn't
    /// picked twice in a row. Kept free of any game types so it can be
    /// tested on its own.
    /// </summary>
    public static class SoundRotation
    {
        /// <summary>
        /// Returns the next playable key after lastKey in orderedKeys
        /// (wrapping around), or null if none are playable.
        ///
        /// lastKey is the key that played most recently (null if nothing has
        /// yet, or if it's no longer in the list) - in which case the
        /// rotation starts from the first key. Keys that aren't playable
        /// (e.g. every sound in the group is switched off in the menu) are
        /// skipped but keep their place in the order, so re-enabling one puts
        /// it back where it was.
        /// </summary>
        public static string NextKey(IList<string> orderedKeys, Func<string, bool> isPlayable, string lastKey)
        {
            int count = orderedKeys.Count;
            if (count == 0)
            {
                return null;
            }

            int start = lastKey == null ? 0 : orderedKeys.IndexOf(lastKey) + 1;

            for (int i = 0; i < count; i++)
            {
                string key = orderedKeys[(start + i) % count];
                if (isPlayable(key))
                {
                    return key;
                }
            }

            return null;
        }
    }
}
