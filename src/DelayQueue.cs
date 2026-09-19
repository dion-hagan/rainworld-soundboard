using System;
using System.Collections.Generic;

namespace SoundboardMod
{
    /// <summary>
    /// Runs actions after a number of game ticks - how a sound's "delay"
    /// works. Driven by the game's own update loop (one Tick per game tick),
    /// so a paused game doesn't burn through delays. Takes no game types so it
    /// can be tested on its own.
    /// </summary>
    public sealed class DelayQueue
    {
        /// <summary>Rain World runs its simulation at 40 ticks per second.</summary>
        public const int TicksPerSecond = 40;

        private sealed class Item
        {
            public int TicksLeft;
            public Action Action;
        }

        private readonly List<Item> items = new List<Item>();
        private readonly Action<Exception> onError;

        public DelayQueue(Action<Exception> onError = null)
        {
            this.onError = onError;
        }

        public int Count => items.Count;

        public static int SecondsToTicks(float seconds)
        {
            return seconds <= 0f ? 0 : Math.Max(1, (int)Math.Round(seconds * TicksPerSecond));
        }

        /// <summary>Schedules action to run after the given number of ticks.</summary>
        public void Add(int ticks, Action action)
        {
            items.Add(new Item { TicksLeft = Math.Max(1, ticks), Action = action });
        }

        /// <summary>Advances one tick and runs everything that has come due, in the order it was added.</summary>
        public void Tick()
        {
            if (items.Count == 0)
            {
                return;
            }

            List<Action> due = null;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                Item item = items[i];
                item.TicksLeft--;
                if (item.TicksLeft <= 0)
                {
                    (due ?? (due = new List<Action>())).Add(item.Action);
                    items.RemoveAt(i);
                }
            }

            if (due == null)
            {
                return;
            }

            // Collected back-to-front above; run front-to-back so ties keep their order.
            for (int i = due.Count - 1; i >= 0; i--)
            {
                try
                {
                    due[i]();
                }
                catch (Exception e)
                {
                    onError?.Invoke(e);
                }
            }
        }

        public void Clear()
        {
            items.Clear();
        }
    }
}
