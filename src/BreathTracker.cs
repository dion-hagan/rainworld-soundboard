using System;

namespace SoundboardMod
{
    /// <summary>What BreathTracker.Update noticed on this update (can be several at once, though in practice it is one).</summary>
    [Flags]
    public enum BreathEvent
    {
        None = 0,

        /// <summary>The player has just gone under and started swimming below the surface.</summary>
        DivedUnderwater = 1,

        /// <summary>The player has just run low on air, the point where the game starts thrashing them about.</summary>
        StartedDrowning = 2,

        /// <summary>The player has just died of drowning.</summary>
        Drowned = 4,
    }

    /// <summary>
    /// Edge detection for the water events of ONE player: diving under,
    /// running out of breath, and drowning. Each reports once, at the moment
    /// it starts, and re-arms once it is over. Free of game types (the hook
    /// reads Player and passes plain values in) so it can be tested on its own.
    /// </summary>
    public sealed class BreathTracker
    {
        private bool wasUnderwater;
        private bool drowningArmed = true;

        // False until the player is seen alive, so a corpse we only meet after
        // the fact (or a player already dead when the tracker was made) can
        // never be reported as "just drowned".
        private bool wasAlive;

        /// <summary>
        /// The air level at which a player who ran low on air counts as having
        /// recovered, so that the next time they run low is a new event. Half
        /// way between the game's out-of-breath level and full lungs: a player
        /// bobbing up and down with their air hovering around the threshold
        /// (air only refills while their head is out of the water) is one
        /// struggle, not a dozen.
        /// </summary>
        public static float RecoveredAirLevel(float lowAirLevel)
        {
            return lowAirLevel + (1f - lowAirLevel) * 0.5f;
        }

        /// <param name="alive">False once the player is dead.</param>
        /// <param name="underwater">The player's head is under water and they are swimming (Player.submerged with the DeepSwim animation).</param>
        /// <param name="submerged">The player's head is under water at all (Player.submerged) - the only time the game drains air.</param>
        /// <param name="airInLungs">1 = full, 0 = none (Player.airInLungs).</param>
        /// <param name="lowAirLevel">Air level below which the game considers this slugcat out of breath (SlugcatStats.drownThreshold).</param>
        /// <param name="drown">The game's drowning counter (Player.drown): 0 while there is air, reaches 1 on the update the player dies of it.</param>
        public BreathEvent Update(bool alive, bool underwater, bool submerged, float airInLungs, float lowAirLevel, float drown)
        {
            if (!alive)
            {
                // The game's LungUpdate kills the player the moment the drowning
                // counter reaches 1 and never runs again for the dead, so a death
                // seen right after being alive with the counter full is a drowning.
                BreathEvent result = wasAlive && drown >= 1f ? BreathEvent.Drowned : BreathEvent.None;
                wasAlive = false;
                wasUnderwater = false;
                drowningArmed = true;
                return result;
            }

            wasAlive = true;
            BreathEvent events = BreathEvent.None;

            if (underwater && !wasUnderwater)
            {
                events |= BreathEvent.DivedUnderwater;
            }

            wasUnderwater = underwater;

            if (drowningArmed)
            {
                if (submerged && airInLungs < lowAirLevel)
                {
                    drowningArmed = false;
                    events |= BreathEvent.StartedDrowning;
                }
            }
            else if (airInLungs >= RecoveredAirLevel(lowAirLevel))
            {
                drowningArmed = true;
            }

            return events;
        }
    }
}
