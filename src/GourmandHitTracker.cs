using System;

namespace SoundboardMod
{
    /// <summary>The three ways the Gourmand's body damages a creature in Player.Collide.</summary>
    public enum GourmandHitKind
    {
        /// <summary>Rolling into it (stun 120, damage 1).</summary>
        Roll,

        /// <summary>Belly-sliding (or rocket-jumping out of a slide) into it.</summary>
        Slide,

        /// <summary>Falling or being flung onto it - the "slam" that isn't a slide or a roll.</summary>
        Drop,
    }

    /// <summary>
    /// Decides which event a Gourmand body hit is, and lets each victim be
    /// reported only once per short window. Player.Collide can run several
    /// times for one impact (once per pair of touching body chunks, and the
    /// slam branch has no lockout of its own the way the roll branch does),
    /// so without this one hit could play its sound twice. No game types, so
    /// it can be tested without the game.
    /// </summary>
    public sealed class GourmandHitTracker
    {
        /// <summary>The roll branch locks itself for 20 ticks (gourmandAttackNegateTime = 20), about half a second.</summary>
        public const float DebounceSeconds = 0.5f;

        public const string SlideEvent = "GourmandSlideHit";
        public const string DropEvent = "GourmandDropHit";
        public const string RollEvent = "GourmandRollHit";

        private readonly Cooldown perVictim;

        public GourmandHitTracker(Func<float> clock)
        {
            perVictim = new Cooldown(DebounceSeconds, clock);
        }

        /// <summary>
        /// Player.Collide checks the roll first, then treats a belly slide or
        /// rocket jump as a slide, and everything else that gets through
        /// SlugSlamConditions (a fast fall) as a drop.
        /// </summary>
        public static GourmandHitKind Classify(bool rolling, bool sliding)
        {
            if (rolling)
            {
                return GourmandHitKind.Roll;
            }

            return sliding ? GourmandHitKind.Slide : GourmandHitKind.Drop;
        }

        public static string EventFor(GourmandHitKind kind)
        {
            switch (kind)
            {
                case GourmandHitKind.Roll:
                    return RollEvent;
                case GourmandHitKind.Slide:
                    return SlideEvent;
                default:
                    return DropEvent;
            }
        }

        /// <summary>
        /// The event to fire for this hit, or null if the same victim was
        /// already reported within the last <see cref="DebounceSeconds"/>
        /// (of any kind - one impact is one sound).
        /// </summary>
        public string TryHit(object victim, bool rolling, bool sliding)
        {
            if (victim == null || !perVictim.TryTrigger(victim))
            {
                return null;
            }

            return EventFor(Classify(rolling, sliding));
        }
    }
}
