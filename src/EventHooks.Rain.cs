using System.Runtime.CompilerServices;
using HarmonyLib;

namespace SoundboardMod
{
    public static partial class EventHooks
    {
        // --- The rain -------------------------------------------------------------
        // RainCycle.Update ticks the cycle timer once per game tick (40 a second) and
        // RainCycle.TimeUntilRain is what's left of the cycle: cycleLength - timer. The
        // game's own "the rain is close" effects (RainApproaching, the screen shake,
        // the controller rumble) all work off that number, and FatalRainImminent fires
        // when it first drops to one minute (2400 ticks), once per cycle.
        //
        // Read after the update, in this postfix, and matched with "at or below" rather
        // than "equals": the timer skips ahead in places (the Rivulet and arena rushes
        // to rain add several ticks at a time). It's tracked per game rather than per
        // RainCycle, because a region gate builds a new RainCycle for the next region
        // and carries the timer over, and passing through one must not warn again.
        //
        // The Rot (RM, More Slugcats) never lets the rain hit - RainCycle skips RainHit
        // there - so a warning would be a lie and is left out. Everywhere else the
        // warning is for the cycle, not for the player: it also plays if the slugcat
        // is already tucked up in a shelter.
        //
        // The sound is played centred, in the room a camera is showing, like the other
        // events that are about the player rather than a spot in the world.
        [HarmonyPatch(typeof(RainCycle), nameof(RainCycle.Update))]
        private static class RainCycle_Update_Patch
        {
            private static readonly ConditionalWeakTable<RainWorldGame, RainWarning> Warnings = new ConditionalWeakTable<RainWorldGame, RainWarning>();

            [HarmonyPostfix]
            private static void Postfix(RainCycle __instance)
            {
                RainWorldGame game = __instance.world?.game;
                if (game?.cameras == null || game.cameras.Length == 0)
                {
                    return;
                }

                // No room to play in yet (between rooms): try again next tick rather than using up the warning.
                Room room = game.cameras[0]?.room;
                if (room == null || RainNeverHits(__instance))
                {
                    return;
                }

                RainWarning warning = Warnings.GetValue(game, _ => new RainWarning());
                if (!warning.Update(__instance.TimeUntilRain))
                {
                    return;
                }

                if (Settings.Debug)
                {
                    Log.LogInfo($"[rain] one minute until the rain (timer {__instance.timer} of {__instance.cycleLength})");
                }

                TriggerNonPositional("FatalRainImminent", room);
            }

            private static bool RainNeverHits(RainCycle cycle)
            {
                // The same test RainCycle.Update makes before calling RainHit.
                return ModManager.MSC && cycle.world.game.IsStorySession && cycle.world.region != null && cycle.world.region.name == "RM";
            }
        }
    }
}
