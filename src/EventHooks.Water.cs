using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace SoundboardMod
{
    public static partial class EventHooks
    {
        // --- Water and breathing -------------------------------------------------
        // Player.Update runs LungUpdate() while the player is alive, and that is
        // where the game decides everything below (all read after the update, in
        // this postfix, so the values are the finished ones for the frame):
        //
        //  - Player.submerged is set there: true while the head chunk is more than
        //    90% under water. It is also the only time air drains
        //    (airInLungs -= ..., scaled by the slugcat's lungsFac, so Rivulet's
        //    air lasts far longer - but airInLungs itself always runs from 1 to 0,
        //    which is why there is no per-slugcat air capacity to work out).
        //  - Player.animation == DeepSwim is the game's "swimming below the
        //    surface" animation, as opposed to SurfaceSwim (treading water).
        //    Requiring both means the head really is under, and the slugcat is
        //    actually diving rather than standing on the bottom of a flooded
        //    room or climbing a pole in it.
        //  - The slugcat counts as out of breath when airInLungs falls below
        //    slugcatStats.drownThreshold (1/3 for every slugcat in the base game,
        //    and read from the player in case a mod changes it): the game starts
        //    slowing the slugcat, shaking it about and releasing bubbles at that
        //    point, and marks lungsExhausted when it surfaces. (lungsExhausted
        //    itself is no use here: MSC also sets it for a tired Gourmand out of
        //    the water.)
        //  - At airInLungs 0 the game stuns the slugcat every update and counts
        //    drown up by 1/120; when drown reaches 1 it calls Die() from inside
        //    LungUpdate, so a player that was alive last update and is dead now
        //    with drown >= 1 has drowned (nothing else touches drown, and
        //    LungUpdate never runs for the dead).
        //
        // Slugpups and other NPC slugcats are ignored, like PlayerRoomTransition.
        // BreathTracker does the once-per-event bookkeeping for each player.
        [HarmonyPatch(typeof(Player), nameof(Player.Update))]
        private static class Player_Update_Breath_Patch
        {
            private static readonly ConditionalWeakTable<Player, BreathTracker> Trackers = new ConditionalWeakTable<Player, BreathTracker>();
            private static readonly Cooldown UnderwaterCooldown = new Cooldown(() => Settings.SwimUnderwaterCooldown, () => Time.time);

            [HarmonyPostfix]
            private static void Postfix(Player __instance)
            {
                if (__instance.isNPC || __instance.room == null || __instance.slugcatStats == null)
                {
                    return;
                }

                bool alive = !__instance.dead;
                bool underwater = alive && __instance.submerged && __instance.animation == Player.AnimationIndex.DeepSwim;
                float lowAir = __instance.slugcatStats.drownThreshold;

                BreathTracker tracker = Trackers.GetValue(__instance, _ => new BreathTracker());
                BreathEvent events = tracker.Update(alive, underwater, __instance.submerged, __instance.airInLungs, lowAir, __instance.drown);
                if (events == BreathEvent.None)
                {
                    return;
                }

                // The tracker always advances; the cooldown only decides whether
                // a dive is heard, so a suppressed one can't leave a stale edge.
                if ((events & BreathEvent.DivedUnderwater) != 0 && UnderwaterCooldown.TryTrigger(__instance))
                {
                    LogWater("dived underwater", __instance);
                    Trigger("PlayerSwimUnderwater", __instance);
                }

                if ((events & BreathEvent.StartedDrowning) != 0)
                {
                    LogWater("ran low on air (below " + lowAir.ToString("0.##") + ")", __instance);
                    Trigger("PlayerDrowning", __instance);
                }

                if ((events & BreathEvent.Drowned) != 0)
                {
                    LogWater("drowned", __instance);
                    Trigger("PlayerDrowned", __instance);
                }
            }

            private static void LogWater(string what, Player player)
            {
                if (Settings.Debug)
                {
                    Log.LogInfo($"[water] {what} (air {player.airInLungs:0.00}, drown {player.drown:0.00})");
                }
            }
        }
    }
}
