using System;
using System.Runtime.CompilerServices;

namespace SoundboardMod
{
    /// <summary>
    /// Lets each key (e.g. one particular creature) trigger at most once per
    /// cooldown period. Keys are held weakly, so a creature that despawns
    /// doesn't leak. Takes its clock as a delegate so it can be tested
    /// without the game.
    /// </summary>
    public sealed class Cooldown
    {
        private readonly ConditionalWeakTable<object, StrongBox<float>> lastTriggered = new ConditionalWeakTable<object, StrongBox<float>>();
        private readonly Func<float> seconds;
        private readonly Func<float> clock;

        public Cooldown(float seconds, Func<float> clock)
            : this(() => seconds, clock)
        {
        }

        /// <summary>
        /// The cooldown length is read on every check, so it can follow a
        /// setting that changes while the game is running.
        /// </summary>
        public Cooldown(Func<float> seconds, Func<float> clock)
        {
            this.seconds = seconds;
            this.clock = clock;
        }

        /// <summary>
        /// Returns true (and starts the key's cooldown) if it has been at
        /// least the cooldown period since it last returned true; false if
        /// it's still cooling down. A key that has never triggered is always
        /// allowed.
        /// </summary>
        public bool TryTrigger(object key)
        {
            float now = clock();
            StrongBox<float> last = lastTriggered.GetValue(key, _ => new StrongBox<float>(float.NegativeInfinity));
            if (now - last.Value < seconds())
            {
                return false;
            }

            last.Value = now;
            return true;
        }
    }
}
