namespace SoundboardMod
{
    /// <summary>
    /// Detects "falling at terminal velocity" for one creature: fires once,
    /// the first time its downward speed reaches the threshold during a fall,
    /// and won't fire again until the fall is over. Rain World has no real
    /// speed cap (gravity keeps adding), so "terminal" is whatever speed the
    /// player configures. Free of game types so it can be tested on its own.
    /// </summary>
    public sealed class FallTracker
    {
        // A fall ends when the creature touches something, or slows to under
        // this fraction of the threshold (grabbed a pole, hit water, ...).
        // Without the second rule a catch-and-drop in one long shaft would
        // never re-arm.
        private const float RearmFraction = 0.5f;

        private bool armed = true;

        /// <summary>Fastest downward speed seen since the creature last touched ground.</summary>
        public float Peak { get; private set; }

        /// <param name="downSpeed">Speed towards the ground (positive when falling).</param>
        /// <param name="grounded">True while touching floor, walls or anything solid.</param>
        /// <returns>True on the single update where the fall first reaches the threshold.</returns>
        public bool Update(float downSpeed, bool grounded, float threshold)
        {
            if (grounded)
            {
                armed = true;
                Peak = 0f;
                return false;
            }

            if (downSpeed > Peak)
            {
                Peak = downSpeed;
            }

            if (!armed)
            {
                if (downSpeed < threshold * RearmFraction)
                {
                    armed = true;
                }

                return false;
            }

            if (downSpeed >= threshold)
            {
                armed = false;
                return true;
            }

            return false;
        }
    }
}
