namespace SoundboardMod
{
    /// <summary>
    /// Decides when to warn that the fatal rain is a minute away: fires once, the
    /// first time the rain timer is at or below the warning time, and stays quiet
    /// until the timer is back above it (a new cycle). Free of game types so it can
    /// be tested on its own.
    /// </summary>
    public sealed class RainWarning
    {
        /// <summary>The game runs 40 ticks a second; RainCycle sizes the cycle as minutes * 40 * 60.</summary>
        public const int TicksPerSecond = 40;

        /// <summary>One minute of rain-timer ticks, the same point the game itself treats as the last stretch (RainCycle.RainApproaching).</summary>
        public const int WarningTicks = 60 * TicksPerSecond;

        private bool warned;

        /// <param name="ticksUntilRain">RainCycle.TimeUntilRain: ticks left until the rain arrives.</param>
        /// <returns>True on the single update where the timer first reaches the warning time.</returns>
        public bool Update(int ticksUntilRain)
        {
            if (ticksUntilRain > WarningTicks)
            {
                warned = false; // a fresh cycle
                return false;
            }

            // Once the rain is here there's nothing left to warn about (and a game
            // that jumps the timer past the warning, like the arena's rush to rain, still warns above).
            if (warned || ticksUntilRain <= 0)
            {
                return false;
            }

            warned = true;
            return true;
        }
    }
}
