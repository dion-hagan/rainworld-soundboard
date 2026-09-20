using System.Collections.Generic;

namespace SoundboardMod
{
    /// <summary>
    /// The decision logic behind the "&lt;Creature&gt;Near" events, for one player: given how far
    /// each creature in the room is at each scan, says which ones just came into range.
    /// Free of game types (creatures are opaque keys) so it can be tested on its own.
    ///
    /// A creature fires when it goes from outside to inside the range ("edge triggered"), so one
    /// that stays close doesn't repeat every scan. Two things stop one that hovers around the
    /// edge from spamming: it only counts as having left once it is a quarter further away than
    /// the range (LeaveFactor), and the shared Cooldown gives each creature a minimum time
    /// between two firings.
    ///
    /// Usage per scan: BeginScan(), Observe() for every creature worth considering, EndScan().
    /// A creature that isn't observed in a scan (left the room, died, ...) is forgotten, so it
    /// fires again when it next comes near.
    /// </summary>
    public sealed class NearTracker
    {
        /// <summary>
        /// How many game ticks (40 per second) pass between scans of the room: 4 scans a second is
        /// plenty for "something walked up to me" and keeps the check off the per-frame path.
        /// </summary>
        public const int ScanIntervalTicks = 10;

        /// <summary>A creature inside the range only counts as having left it beyond range times this.</summary>
        public const float LeaveFactor = 1.25f;

        private readonly Cooldown cooldown;
        private readonly Dictionary<object, int> inside = new Dictionary<object, int>();
        private readonly List<object> forgotten = new List<object>();
        private int scan;
        private int ticks;

        /// <param name="cooldown">
        /// Limits how often one creature can fire. Pass the same instance to every player's tracker
        /// so a creature standing between two players fires once, not once each.
        /// </param>
        public NearTracker(Cooldown cooldown)
        {
            this.cooldown = cooldown;
        }

        /// <summary>How many creatures are currently counted as inside the range.</summary>
        public int InsideCount => inside.Count;

        /// <summary>Call once per game tick; true on the ticks when the room should be scanned.</summary>
        public bool Tick()
        {
            if (++ticks < ScanIntervalTicks)
            {
                return false;
            }

            ticks = 0;
            return true;
        }

        public void BeginScan()
        {
            scan++;
        }

        /// <param name="key">Identifies the creature between scans (it must be the same object each time).</param>
        /// <param name="distance">How far the creature is from the player, in the same units as nearDistance.</param>
        /// <param name="nearDistance">The range: at or inside this is "near".</param>
        /// <param name="allowFire">
        /// False for a creature whose arrival shouldn't count (e.g. one the player is carrying): it is
        /// still tracked as inside, so it doesn't fire the moment it's put down.
        /// </param>
        /// <returns>True if this creature just came into range and should fire its event.</returns>
        public bool Observe(object key, float distance, float nearDistance, bool allowFire)
        {
            if (inside.ContainsKey(key))
            {
                if (distance > nearDistance * LeaveFactor)
                {
                    inside.Remove(key);
                }
                else
                {
                    inside[key] = scan;
                }

                return false;
            }

            if (distance > nearDistance)
            {
                return false;
            }

            inside[key] = scan;
            return allowFire && cooldown.TryTrigger(key);
        }

        /// <summary>Forgets every creature that wasn't observed since BeginScan.</summary>
        public void EndScan()
        {
            if (inside.Count == 0)
            {
                return;
            }

            forgotten.Clear();
            foreach (KeyValuePair<object, int> entry in inside)
            {
                if (entry.Value != scan)
                {
                    forgotten.Add(entry.Key);
                }
            }

            foreach (object key in forgotten)
            {
                inside.Remove(key);
            }

            forgotten.Clear();
        }

        /// <summary>Forgets everything, e.g. when the player is in a different room than at the last scan.</summary>
        public void Reset()
        {
            inside.Clear();
        }
    }
}
