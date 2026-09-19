using System.Collections.Generic;

namespace SoundboardMod
{
    /// <summary>
    /// Tracks which soundboard entries are cooling down - how an entry's "cooldown:" works.
    /// Once an entry plays, it can't play again until its cooldown has run out; while it
    /// waits it counts as unavailable, so an event's rotation moves on to the next entry
    /// (and an event with only that entry stays quiet). Time is counted in game ticks,
    /// driven by the game's own update loop like DelayQueue, so a paused game doesn't
    /// burn through a cooldown. Takes no game types so it can be tested on its own.
    /// </summary>
    public sealed class EntryCooldowns
    {
        private readonly Dictionary<string, long> readyAt = new Dictionary<string, long>();

        /// <summary>Game ticks counted so far.</summary>
        public long Now { get; private set; }

        /// <summary>Advances one game tick.</summary>
        public void Tick()
        {
            Now++;
        }

        public bool IsCoolingDown(string key)
        {
            return readyAt.TryGetValue(key, out long ready) && Now < ready;
        }

        /// <summary>Starts the key's cooldown from now. A length of 0 (or less) means no cooldown.</summary>
        public void Start(string key, float seconds)
        {
            int ticks = DelayQueue.SecondsToTicks(seconds);
            if (ticks > 0)
            {
                readyAt[key] = Now + ticks;
            }
        }

        /// <summary>Forgets every cooldown (a new game session, or a reloaded config).</summary>
        public void Clear()
        {
            readyAt.Clear();
        }
    }
}
