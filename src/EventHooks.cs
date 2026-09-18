using System.Linq;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SoundboardMod
{
    /// <summary>
    /// Game-event hooks that trigger soundboard playback. Every nested
    /// *_Patch class below patches one real game method and calls
    /// Trigger(...)/TriggerAt(...) with an event key. Sounds in
    /// soundeffects/meta.json reference these keys via their "event" field -
    /// multiple sounds can share a key, and one is chosen at random among
    /// whichever are currently enabled.
    ///
    /// To add a new hook: add another nested class patching whatever method
    /// fires at the moment you care about, then call Trigger/TriggerAt with
    /// a new event key of your choosing.
    /// </summary>
    public static class EventHooks
    {
        // Tune this if "hard landing" fires too often/rarely - it's the
        // fall speed (in the game's internal units) at first ground
        // contact, not a real-world unit. Empirically measured: normal jump
        // landings top out around ~9.4, a drop from a tall pole hit ~22.9.
        // Threshold set between the two with some margin.
        private const float HardLandingSpeedThreshold = 20f;

        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("SoundboardMod");

        public static void Apply(Harmony harmony)
        {
            harmony.PatchAll(typeof(EventHooks).Assembly);
            int patchedCount = harmony.GetPatchedMethods().Count();
            Log.LogInfo($"Harmony patched {patchedCount} method(s).");
        }

        // --- Player ---------------------------------------------------

        [HarmonyPatch(typeof(Player), nameof(Player.Die))]
        private static class Player_Die_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(Player __instance)
            {
                Trigger("PlayerDeath", __instance);
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.Jump))]
        private static class Player_Jump_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(Player __instance)
            {
                Trigger("PlayerJump", __instance);
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.ObjectEaten))]
        private static class Player_ObjectEaten_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(Player __instance, IPlayerEdible edible)
            {
                Trigger("PlayerEat", __instance);

                // Bugs/critters implement IPlayerEdible directly (as opposed
                // to fruit/plants), so this is "ate meat" specifically.
                if (edible is Creature)
                {
                    Trigger("PlayerEatCreature", __instance);
                }
            }
        }

        [HarmonyPatch(typeof(Player), "ThrownSpear")]
        private static class Player_ThrownSpear_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(Player __instance, Spear spear)
            {
                if (spear is ExplosiveSpear)
                {
                    Trigger("PlayerThrowExplosiveSpear", __instance);
                }
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.TerrainImpact))]
        private static class Player_TerrainImpact_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(Player __instance, System.Boolean firstContact, System.Single speed)
            {
                if (firstContact && speed > HardLandingSpeedThreshold)
                {
                    Trigger("PlayerHardLanding", __instance);
                }
            }
        }

        // --- Grabbing (shared by "explosive pickup" and "carry a slugcat") -

        [HarmonyPatch(typeof(Creature), nameof(Creature.Grab))]
        private static class Creature_Grab_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(Creature __instance, PhysicalObject obj, bool __result)
            {
                if (!__result || !(__instance is Player player))
                {
                    return;
                }

                if (obj is ExplosiveSpear || obj is ScavengerBomb)
                {
                    Trigger("PlayerGrabExplosive", player);
                }
                else if (obj is Player)
                {
                    Trigger("PlayerGrabSlugcat", player);
                }
            }
        }

        // --- Other creatures dying -------------------------------------
        // Scavenger and Lizard don't override Die(), so patching the base
        // Creature.Die catches them. Spider/BigSpider DO override Die(),
        // so they each need their own patch.

        [HarmonyPatch(typeof(Creature), nameof(Creature.Die))]
        private static class Creature_Die_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(Creature __instance)
            {
                if (__instance is Scavenger)
                {
                    Trigger("ScavengerDeath", __instance);
                }
                else if (__instance is Lizard)
                {
                    Trigger("LizardDeath", __instance);
                }
            }
        }

        [HarmonyPatch(typeof(Spider), nameof(Spider.Die))]
        private static class Spider_Die_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(Spider __instance)
            {
                Trigger("SpiderDeath", __instance);
            }
        }

        [HarmonyPatch(typeof(BigSpider), nameof(BigSpider.Die))]
        private static class BigSpider_Die_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(BigSpider __instance)
            {
                Trigger("SpiderDeath", __instance);
            }
        }

        // --- Shelter -----------------------------------------------------

        [HarmonyPatch(typeof(ShelterDoor), "DoorClosed")]
        private static class ShelterDoor_DoorClosed_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(ShelterDoor __instance)
            {
                TriggerNonPositional("PlayerEnterShelter", __instance.room);
            }
        }

        // --- Predator noticed the player ---------------------------------
        // Tracker.CreatureNoticed fires on the predator's own Tracker
        // (owned by its ArtificialIntelligence) the moment it first spots
        // something. We only care when that "something" is the player, and
        // the tracker's owner is one of the predators we're after.

        [HarmonyPatch(typeof(Tracker), "CreatureNoticed")]
        private static class Tracker_CreatureNoticed_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(Tracker __instance, AbstractCreature crit)
            {
                if (!(crit?.realizedObject is Player player))
                {
                    return;
                }

                PhysicalObject predator = __instance.AI?.creature?.realizedObject;
                if (predator is Lizard || predator is Spider || predator is BigSpider || predator is Vulture)
                {
                    Trigger("PlayerSpottedByPredator", player);
                }
            }
        }

        // --- Shared playback logic --------------------------------------

        private static void Trigger(string eventKey, Creature creature)
        {
            if (creature?.room == null || creature.bodyChunks == null || creature.bodyChunks.Length == 0)
            {
                return;
            }

            TriggerAt(eventKey, creature.room, creature.bodyChunks[0].pos);
        }

        /// <summary>
        /// Plays one randomly-chosen, currently-enabled sound registered for
        /// the given event key (if any), positioned in the world so it
        /// pans/attenuates naturally.
        /// </summary>
        private static void TriggerAt(string eventKey, Room room, Vector2 pos)
        {
            if (room == null)
            {
                return;
            }

            SoundEntry chosen = ChooseSound(eventKey);
            if (chosen != null)
            {
                room.PlaySound(chosen.soundId, pos, 1f, 1f);
            }
        }

        /// <summary>Same as TriggerAt, but for events with no natural world position.</summary>
        private static void TriggerNonPositional(string eventKey, Room room)
        {
            if (room == null)
            {
                return;
            }

            SoundEntry chosen = ChooseSound(eventKey);
            if (chosen != null)
            {
                room.PlaySound(chosen.soundId);
            }
        }

        private static SoundEntry ChooseSound(string eventKey)
        {
            SoundEntry chosen = null;
            int matchCount = 0;

            foreach (SoundEntry entry in SoundboardData.Sounds)
            {
                if (entry.@event != eventKey || entry.soundId == null)
                {
                    continue;
                }

                bool enabled = Options.Instance == null || Options.Instance.IsEnabled(entry.id);
                if (!enabled)
                {
                    continue;
                }

                matchCount++;
                if (UnityEngine.Random.Range(0, matchCount) == 0)
                {
                    chosen = entry;
                }
            }

            return chosen;
        }
    }
}
