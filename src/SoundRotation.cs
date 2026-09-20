using System;
using System.Collections.Generic;
using System.Linq;

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

        /// <summary>
        /// The shuffled version of NextKey: picks at random from the keys that haven't had a turn
        /// yet this lap, so every key plays once before any plays again, in a different order each
        /// lap and each launch.
        ///
        /// bag is the caller's per-event state, the keys still waiting for their turn this lap; an
        /// empty bag (the very first call) starts a lap. When a lap is used up, or everything left
        /// in the bag is unplayable, a new lap starts with every key - and the key that played last
        /// is left out of the first pick, so a lap boundary never plays the same sound twice in a
        /// row (unless it's the only one available). An unplayable key just stays in the bag until
        /// it is playable and gets drawn, so it doesn't lose its turn.
        /// </summary>
        public static string NextShuffled(IList<string> orderedKeys, Func<string, bool> isPlayable, List<string> bag, string lastKey, Random random)
        {
            // Keys removed from the event since the bag was filled have no turn to take.
            bag.RemoveAll(k => !orderedKeys.Contains(k));

            List<string> candidates = bag.Where(isPlayable).ToList();
            if (candidates.Count == 0)
            {
                bag.Clear();
                bag.AddRange(orderedKeys);
                candidates = bag.Where(isPlayable).ToList();
                if (candidates.Count > 1 && lastKey != null)
                {
                    candidates.Remove(lastKey);
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            string pick = candidates[random.Next(candidates.Count)];
            bag.Remove(pick);
            return pick;
        }
    }
}
