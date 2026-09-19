using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
    /// multiple sounds can share a key, and they take turns in meta.json
    /// order (skipping any that are switched off in the menu).
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
        private const float HardLandingSpeedThreshold = 30f;

        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("SoundboardMod");

        public static void Apply(Harmony harmony)
        {
            // Patch each hook class on its own instead of PatchAll, so a hook
            // that fails to resolve (e.g. a renamed game method) is logged
            // and skipped rather than aborting every hook after it.
            int failed = 0;
            foreach (Type type in typeof(EventHooks).Assembly.GetTypes())
            {
                if (!type.IsDefined(typeof(HarmonyPatch), false))
                {
                    continue;
                }

                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                }
                catch (Exception e)
                {
                    failed++;
                    Log.LogError($"Hook {type.Name} failed to apply and was skipped: {e.Message}");
                }
            }

            int patchedCount = harmony.GetPatchedMethods().Count();
            Log.LogInfo($"Harmony patched {patchedCount} method(s), {failed} hook class(es) failed.");
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

                if (IsHoldingCicada(__instance))
                {
                    Trigger("PlayerJumpWithCicada", __instance);
                }
            }

            private static bool IsHoldingCicada(Player player)
            {
                Creature.Grasp[] grasps = player.grasps;
                if (grasps == null)
                {
                    return false;
                }

                foreach (Creature.Grasp grasp in grasps)
                {
                    if (grasp?.grabbed is Cicada)
                    {
                        return true;
                    }
                }

                return false;
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

        // Player.pyroJumpped is a persistent flag, not a one-frame pulse, so
        // track its last value per-player and only fire on the false->true
        // edge (otherwise this would re-trigger every frame it stays true).
        [HarmonyPatch(typeof(Player), nameof(Player.ClassMechanicsArtificer))]
        private static class Player_ClassMechanicsArtificer_Patch
        {
            private static readonly ConditionalWeakTable<Player, StrongBox<bool>> LastPyroJumped = new ConditionalWeakTable<Player, StrongBox<bool>>();

            [HarmonyPostfix]
            private static void Postfix(Player __instance)
            {
                StrongBox<bool> last = LastPyroJumped.GetOrCreateValue(__instance);
                if (__instance.pyroJumpped && !last.Value)
                {
                    Trigger("PlayerArtificerPyroJump", __instance);
                }

                last.Value = __instance.pyroJumpped;
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
                else if (obj is MoreSlugcats.Yeek)
                {
                    Trigger("PlayerGrabYeek", player);
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

        [HarmonyPatch(typeof(Snail), nameof(Snail.Die))]
        private static class Snail_Die_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(Snail __instance)
            {
                Trigger("SnailExplosion", __instance);
            }
        }

        [HarmonyPatch(typeof(Cicada), nameof(Cicada.Die))]
        private static class Cicada_Die_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(Cicada __instance)
            {
                Trigger("CicadaOrLanternMouseDeath", __instance);
            }
        }

        [HarmonyPatch(typeof(LanternMouse), nameof(LanternMouse.Die))]
        private static class LanternMouse_Die_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(LanternMouse __instance)
            {
                Trigger("CicadaOrLanternMouseDeath", __instance);
            }
        }

        // VultureGrub.InitiateSignal is the moment it starts actively
        // emitting its call (after being thrown by the player), which is
        // what actually summons nearby vultures.
        [HarmonyPatch(typeof(VultureGrub), "InitiateSignal")]
        private static class VultureGrub_InitiateSignal_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(VultureGrub __instance)
            {
                Trigger("VultureGrubSignal", __instance);
            }
        }

        // --- Lizards -------------------------------------------------------

        // LizardJumpModule.Jump() is the launch itself (InitiateJump is the
        // wind-up decision). Not confirmed strictly one-shot per jump (no
        // decompiler available), so there's a short per-lizard cooldown to
        // keep a single leap from stacking sounds.
        [HarmonyPatch(typeof(LizardJumpModule), nameof(LizardJumpModule.Jump))]
        private static class LizardJumpModule_Jump_Patch
        {
            private const float CooldownSeconds = 1f;
            private static readonly ConditionalWeakTable<Lizard, StrongBox<float>> LastJumpTime = new ConditionalWeakTable<Lizard, StrongBox<float>>();

            [HarmonyPostfix]
            private static void Postfix(Lizard ___lizard)
            {
                Lizard lizard = ___lizard;
                if (lizard == null || !IsCreatureType(lizard, CreatureTemplate.Type.CyanLizard))
                {
                    return;
                }

                StrongBox<float> last = LastJumpTime.GetValue(lizard, _ => new StrongBox<float>(float.NegativeInfinity));
                if (Time.time - last.Value < CooldownSeconds)
                {
                    return;
                }

                last.Value = Time.time;
                Trigger("CyanLizardJump", lizard);
            }
        }

        // Lizard.Bite(chunk) is the bite that actually lands on a body chunk.
        [HarmonyPatch(typeof(Lizard), "Bite")]
        private static class Lizard_Bite_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(BodyChunk chunk)
            {
                if (chunk?.owner is Player player)
                {
                    Trigger("PlayerBitByLizard", player);
                }
            }
        }

        // --- Thrown weapons --------------------------------------------------

        // Weapon.Thrown is virtual and most weapon types override it, but
        // Spear (and ExplosiveSpear, which inherits it) is covered here.
        // MSC's ElectricSpear overrides Thrown separately and isn't hooked,
        // so a Scavenger throwing one of those won't trigger this.
        [HarmonyPatch(typeof(Spear), nameof(Spear.Thrown))]
        private static class Spear_Thrown_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(Creature thrownBy)
            {
                if (thrownBy is Scavenger)
                {
                    Trigger("ScavengerThrowSpear", thrownBy);
                }
            }
        }

        // Fires for any thrower (player or otherwise), positioned at the bomb.
        [HarmonyPatch(typeof(FlareBomb), nameof(FlareBomb.Thrown))]
        private static class FlareBomb_Thrown_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(FlareBomb __instance)
            {
                Trigger("FlareBombThrown", __instance);
            }
        }

        // --- Spitter spider ----------------------------------------------------
        // The spit projectile is a DartMaggot. Rather than trust that
        // ChangeMode runs after stuckInChunk is assigned, watch each maggot's
        // state every update and fire once, the first time it's seen stuck in
        // a Player. ___stuckInChunk is Harmony's way of reading the private
        // field of the same name.

        [HarmonyPatch(typeof(DartMaggot), nameof(DartMaggot.Update))]
        private static class DartMaggot_Update_Patch
        {
            private static readonly ConditionalWeakTable<DartMaggot, object> AlreadyReported = new ConditionalWeakTable<DartMaggot, object>();

            [HarmonyPostfix]
            private static void Postfix(DartMaggot __instance, BodyChunk ___stuckInChunk)
            {
                if (__instance.mode != DartMaggot.Mode.StuckInChunk || !(___stuckInChunk?.owner is Player player))
                {
                    return;
                }

                if (AlreadyReported.TryGetValue(__instance, out _))
                {
                    return;
                }

                AlreadyReported.Add(__instance, null);
                Trigger("PlayerHitByDartMaggot", player);
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

        // Fires whenever ANY creature moves into a shelter room that
        // already has a player physically present in it (excluding the
        // entering creature itself, so a player's own first entry doesn't
        // trigger it). NOTE: some creature types override NewRoom() instead
        // of using Creature's - if a particular creature type never
        // triggers this, that's why (same issue as Die() needing per-type
        // patches for Spider/BigSpider/etc above).
        [HarmonyPatch(typeof(Creature), nameof(Creature.NewRoom))]
        private static class Creature_NewRoom_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(Creature __instance, Room newRoom)
            {
                if (newRoom?.shelterDoor == null || __instance.bodyChunks == null || __instance.bodyChunks.Length == 0)
                {
                    return;
                }

                if (!AnotherPlayerAlreadyThere(newRoom, __instance))
                {
                    return;
                }

                TriggerAt("CreatureEnteredOccupiedShelter", newRoom, __instance.bodyChunks[0].pos);
            }

            private static bool AnotherPlayerAlreadyThere(Room room, Creature entering)
            {
                if (room.physicalObjects == null)
                {
                    return false;
                }

                foreach (List<PhysicalObject> layer in room.physicalObjects)
                {
                    if (layer == null)
                    {
                        continue;
                    }

                    foreach (PhysicalObject obj in layer)
                    {
                        if (obj is Player player && !ReferenceEquals(player, entering))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }

        // --- Region gate ---------------------------------------------------
        // OPENCLOSE is the method that kicks off a gate's door-opening
        // sequence, i.e. the transition. It's not confirmed 100% one-shot
        // (no decompiler was available to check the method body), so if this
        // ends up firing more than once per transition, that's the place to
        // look.

        [HarmonyPatch(typeof(RegionGate), "OPENCLOSE")]
        private static class RegionGate_OPENCLOSE_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(RegionGate __instance)
            {
                TriggerNonPositional("RegionGateTransition", __instance.room);
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
                if (predator is Scavenger)
                {
                    Trigger("PlayerSpottedByScavenger", player);
                }
                else if (predator is DaddyLongLegs
                    || IsCreatureType(predator, CreatureTemplate.Type.RedLizard)
                    || IsCreatureType(predator, CreatureTemplate.Type.RedCentipede)
                    || IsCreatureType(predator, CreatureTemplate.Type.KingVulture))
                {
                    // Takes priority over the generic predator sound below.
                    Trigger("PlayerSpottedByMajorThreat", player);
                }
                else if (predator is MirosBird || (predator is Vulture vulture && vulture.IsMiros))
                {
                    // Miros Vultures are ordinary Vulture objects flagged IsMiros.
                    Trigger("PlayerSpottedByMiros", player);
                }
                else if (IsCreatureType(predator, CreatureTemplate.Type.CyanLizard))
                {
                    Trigger("PlayerSpottedByCyanLizard", player);
                }
                else if (predator is Lizard || predator is Spider || predator is BigSpider || predator is Vulture)
                {
                    Trigger("PlayerSpottedByPredator", player);
                }
            }
        }

        // --- Shared playback logic --------------------------------------

        private static void Trigger(string eventKey, PhysicalObject source)
        {
            if (source?.room == null || source.bodyChunks == null || source.bodyChunks.Length == 0)
            {
                return;
            }

            TriggerAt(eventKey, source.room, source.bodyChunks[0].pos);
        }

        /// <summary>
        /// True if obj is a creature of the given template type - needed to
        /// tell apart variants that share a class (Red/Cyan/etc. Lizard are
        /// all just "Lizard", King Vulture is a "Vulture", ...).
        /// </summary>
        private static bool IsCreatureType(PhysicalObject obj, CreatureTemplate.Type type)
        {
            return obj is Creature creature && creature.abstractCreature?.creatureTemplate?.type == type;
        }

        /// <summary>
        /// Plays every sound in the next group in line for the given event
        /// key (if any), positioned in the world so it pans/attenuates
        /// naturally. Sounds with no "group" in meta.json are their own group
        /// of one; sounds sharing a group take their turn as a single unit
        /// and all play together.
        /// </summary>
        private static void TriggerAt(string eventKey, Room room, Vector2 pos)
        {
            if (room == null)
            {
                return;
            }

            foreach (SoundEntry entry in ChooseGroup(eventKey))
            {
                room.PlaySound(entry.soundId, pos, 1f, 1f);
            }
        }

        /// <summary>Same as TriggerAt, but for events with no natural world position.</summary>
        private static void TriggerNonPositional(string eventKey, Room room)
        {
            if (room == null)
            {
                return;
            }

            foreach (SoundEntry entry in ChooseGroup(eventKey))
            {
                room.PlaySound(entry.soundId);
            }
        }

        // Which group played last for each event, so the next trigger moves on
        // to the following one. In-memory only: every launch starts each
        // event's rotation from its first sound again.
        private static readonly Dictionary<string, string> LastPlayedGroup = new Dictionary<string, string>();

        private static bool IsEnabled(SoundEntry entry)
        {
            return Options.Instance == null || Options.Instance.IsEnabled(entry.id);
        }

        /// <summary>
        /// Picks the next group for eventKey and returns the enabled sounds in
        /// it. Groups are visited in meta.json order and wrap around after the
        /// last one (see SoundRotation), so a sound isn't repeated until every
        /// other enabled group for that event has had a turn. A sound with no
        /// "group" is its own group of one.
        /// </summary>
        private static List<SoundEntry> ChooseGroup(string eventKey)
        {
            // Every sound for this event, in meta.json order, bucketed by group.
            // Disabled sounds stay in the list so they keep their place.
            var order = new List<string>();
            var groups = new Dictionary<string, List<SoundEntry>>();

            foreach (SoundEntry entry in SoundboardData.Sounds)
            {
                if (entry.@event != eventKey || entry.soundId == null)
                {
                    continue;
                }

                string groupKey = string.IsNullOrEmpty(entry.group) ? entry.id : entry.group;
                if (!groups.TryGetValue(groupKey, out List<SoundEntry> members))
                {
                    members = new List<SoundEntry>();
                    groups[groupKey] = members;
                    order.Add(groupKey);
                }

                members.Add(entry);
            }

            LastPlayedGroup.TryGetValue(eventKey, out string lastKey);
            string next = SoundRotation.NextKey(order, key => groups[key].Any(IsEnabled), lastKey);
            if (next == null)
            {
                return new List<SoundEntry>();
            }

            LastPlayedGroup[eventKey] = next;
            return groups[next].Where(IsEnabled).ToList();
        }
    }
}
