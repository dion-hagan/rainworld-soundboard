using HarmonyLib;
using MoreSlugcats;

namespace SoundboardMod
{
    /// <summary>
    /// Movement-tech events (slide, pounces, flips, wall jump, super jump) and
    /// their Rivulet-only twins. Rain World has no "tech happened" callback, so
    /// each is worked out from the player's state around Player.Jump() /
    /// Player.WallJump(); see MovementTech for the decision table and how
    /// certain each one is.
    /// </summary>
    public static partial class EventHooks
    {
        /// <summary>
        /// True while the player is in a belly slide. Kept internal and tiny so
        /// other hooks can share the same definition of "sliding".
        /// The game starts a slide only in Player.Jump() (a crawling player
        /// pressing jump with down + a direction), and ends it by changing
        /// Player.animation, so the animation is the whole truth.
        /// </summary>
        internal static bool IsBellySliding(Player player)
        {
            return player != null && player.animation == Player.AnimationIndex.BellySlide;
        }

        /// <summary>
        /// True for the Rivulet character, the same test the game uses as the
        /// first half of Player.isRivulet (Expedition's "agility" perk, which
        /// also sets isRivulet for other slugcats, is deliberately NOT counted:
        /// these are the character's events). ModManager.MSC is checked first,
        /// because MoreSlugcatsEnums.SlugcatStatsName.Rivulet is only assigned
        /// while More Slugcats is enabled (it is null otherwise).
        /// </summary>
        internal static bool IsRivulet(Player player)
        {
            return ModManager.MSC
                && player != null
                && player.SlugCatClass != null
                && player.SlugCatClass == MoreSlugcatsEnums.SlugcatStatsName.Rivulet;
        }

        private static MoveAnim ToMoveAnim(Player.AnimationIndex animation)
        {
            if (animation == Player.AnimationIndex.BellySlide)
            {
                return MoveAnim.BellySlide;
            }

            if (animation == Player.AnimationIndex.Roll)
            {
                return MoveAnim.Roll;
            }

            if (animation == Player.AnimationIndex.RocketJump)
            {
                return MoveAnim.RocketJump;
            }

            if (animation == Player.AnimationIndex.Flip)
            {
                return MoveAnim.Flip;
            }

            return MoveAnim.Other;
        }

        private static JumpSnapshot SnapshotForJump(Player player)
        {
            return new JumpSnapshot(ToMoveAnim(player.animation), player.slideCounter, player.standing, player.superLaunchJump);
        }

        // A second patch on Player.Jump next to Player_Jump_Patch (which is
        // left alone): Harmony runs both. The prefix records the state Jump()
        // is about to change; the postfix compares it with the state after.
        // The moves fire once each by construction - Jump() runs once per
        // jump - so no per-player edge state is needed.
        //
        // Note the game starts a belly slide from inside Jump(), so PlayerJump
        // also fires for a slide start; every tech below fires alongside it.
        [HarmonyPatch(typeof(Player), nameof(Player.Jump))]
        private static class Player_Jump_MovementTech_Patch
        {
            [HarmonyPrefix]
            private static void Prefix(Player __instance, out JumpSnapshot __state)
            {
                __state = SnapshotForJump(__instance);
            }

            [HarmonyPostfix]
            private static void Postfix(Player __instance, JumpSnapshot __state)
            {
                MoveTech tech = MovementTech.Classify(__state, SnapshotForJump(__instance));
                bool rivulet = IsRivulet(__instance);

                string eventName = MovementTech.EventName(tech);
                if (eventName != null)
                {
                    Trigger(eventName, __instance);
                }

                if (rivulet)
                {
                    Trigger("RivuletJump", __instance);

                    string rivuletEvent = MovementTech.RivuletEventName(tech);
                    if (rivuletEvent != null)
                    {
                        Trigger(rivuletEvent, __instance);
                    }
                }
            }
        }

        // Player.WallJump is called from Jump() (clinging to a wall, or
        // hanging on a ledge) and straight from Player.Update (airborne next
        // to a wall), so hooking it catches both - and that is why this can
        // fire without PlayerJump. Only the "kick off a wall" branch counts;
        // the floor/water/ledge-top hop in the same method does not (see
        // MovementTech.IsWallKick). The test is evaluated in the prefix
        // because the method moves the body before we could look again.
        [HarmonyPatch(typeof(Player), nameof(Player.WallJump))]
        private static class Player_WallJump_MovementTech_Patch
        {
            [HarmonyPrefix]
            private static void Prefix(Player __instance, out bool __state)
            {
                int inputX = __instance.input[0].x;
                bool ledgeHop = inputX != 0
                    && __instance.bodyChunks[0].ContactPoint.x == inputX
                    && __instance.IsTileSolid(0, inputX, 0)
                    && !__instance.IsTileSolid(0, inputX, 1);
                bool floorBelow = __instance.IsTileSolid(1, 0, -1) || __instance.IsTileSolid(0, 0, -1);
                bool inWater = __instance.bodyChunks[1].submersion > 0.1f;

                __state = MovementTech.IsWallKick(floorBelow, inWater, ledgeHop);
            }

            [HarmonyPostfix]
            private static void Postfix(Player __instance, bool __state)
            {
                if (__state)
                {
                    Trigger("PlayerWallJump", __instance);
                }
            }
        }
    }
}
