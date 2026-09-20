using HarmonyLib;
using UnityEngine;

namespace SoundboardMod
{
    public static partial class EventHooks
    {
        // --- The Gourmand's body attacks -----------------------------------------
        // The only place the game hurts a creature with the Gourmand's body is the
        // MSC block of Player.Collide (Room.Update calls it for every pair of
        // overlapping chunks). It has two branches, and both do the same thing at
        // the moment the hit lands: SetKillTag(the player), then
        // Violence(mainBodyChunk, ..., Blunt, damage, stun):
        //  - animation == Roll and gourmandAttackNegateTime <= 0: "SLUGROLLED",
        //    damage 1, stun 120, then a 20 tick lockout.
        //  - SlugSlamConditions(other): "SLUGSMASH". A belly slide or rocket jump
        //    (damage 0.25, stun 50) or a fast fall (damage and stun scale with how
        //    far and how fast it fell). This branch only damages a living creature
        //    (a corpse just gets a thud) and has no lockout of its own.
        // Both skip SlugNPC pups, and another player unless friendly fire is on,
        // so a hit on a fellow slugcat only fires the event when it truly hurts.
        //
        // Violence is overridden by a long list of creature classes, so instead of
        // patching all of those, Collide's prefix notes which Gourmand and victim
        // are involved and SetKillTag's postfix (a plain, non-virtual Creature
        // method that Collide calls right before Violence) reports the hit. Violence
        // itself calls SetKillTag again for a Creature source, so the context is
        // marked done after the first report and the hit is only counted once.
        // The event plays at the Gourmand (Player scope, like the other Player*
        // events); the victim is right next to it anyway.

        private static readonly GourmandHitTracker GourmandHits = new GourmandHitTracker(() => Time.time);

        private static Player gourmandCollidePlayer;
        private static Creature gourmandCollideVictim;
        private static bool gourmandCollideReported;

        [HarmonyPatch(typeof(Player), nameof(Player.Collide))]
        private static class Player_Collide_Gourmand_Patch
        {
            [HarmonyPrefix]
            private static void Prefix(Player __instance, PhysicalObject otherObject)
            {
                if (__instance.isGourmand && otherObject is Creature victim)
                {
                    gourmandCollidePlayer = __instance;
                    gourmandCollideVictim = victim;
                    gourmandCollideReported = false;
                }
            }

            // A finalizer, not a postfix, so a throwing Collide can't leave the
            // context set. It returns nothing, so an exception still propagates.
            [HarmonyFinalizer]
            private static void Finalizer()
            {
                gourmandCollidePlayer = null;
                gourmandCollideVictim = null;
            }
        }

        [HarmonyPatch(typeof(Creature), nameof(Creature.SetKillTag))]
        private static class Creature_SetKillTag_Gourmand_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(Creature __instance, AbstractCreature killer)
            {
                Player player = gourmandCollidePlayer;
                if (player == null || gourmandCollideReported || !ReferenceEquals(__instance, gourmandCollideVictim) || killer != player.abstractCreature)
                {
                    return;
                }

                // SetKillTag does nothing for a creature that's already dead or out of health.
                if (__instance.killTag != killer)
                {
                    return;
                }

                gourmandCollideReported = true;

                // The animation is still the attacking one here: Collide only resets
                // it to None (after a roll hit) once Violence has returned.
                bool rolling = player.animation == Player.AnimationIndex.Roll;
                bool sliding = player.animation == Player.AnimationIndex.BellySlide || player.animation == Player.AnimationIndex.RocketJump;

                string eventKey = GourmandHits.TryHit(__instance, rolling, sliding);
                if (eventKey != null)
                {
                    if (Settings.Debug)
                    {
                        Log.LogInfo($"[gourmand] {eventKey} on {CreatureTypeName(__instance)}");
                    }

                    Trigger(eventKey, player);
                }
            }
        }
    }
}
