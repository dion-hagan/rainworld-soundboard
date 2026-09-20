using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SoundboardMod
{
    /// <summary>
    /// Game-event hooks that trigger soundboard playback. Every nested
    /// *_Patch class below patches one real game method and calls
    /// Trigger(...)/TriggerAt(...) with an event key. What plays for a key is
    /// decided by soundboard.yaml (see SoundboardRuntime.Fire) - nothing in
    /// here knows about particular sounds.
    ///
    /// To add a new hook: add another nested class patching whatever method
    /// fires at the moment you care about, call Trigger/TriggerAt with a new
    /// event key, and add that key to EventCatalog so it shows up in
    /// events.txt and validates in the config.
    /// </summary>
    public static class EventHooks
    {
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("SoundboardMod");

        private static SoundboardSettings Settings => SoundboardRuntime.Settings;

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

            int deathHooks = PatchCreatureDeaths(harmony);

            int patchedCount = harmony.GetPatchedMethods().Count();
            Log.LogInfo($"Harmony patched {patchedCount} method(s) ({deathHooks} of them creature Die() methods), {failed} hook class(es) failed.");
        }

        // --- Player ---------------------------------------------------

        // PlayerJump fires on every jump; PlayerJumpCooldown is the same moment
        // but at most once per "player-jump-cooldown" seconds for each player,
        // for sounds too long to sit through on every hop.
        [HarmonyPatch(typeof(Player), nameof(Player.Jump))]
        private static class Player_Jump_Patch
        {
            private static readonly Cooldown JumpCooldown = new Cooldown(() => Settings.PlayerJumpCooldown, () => Time.time);

            [HarmonyPostfix]
            private static void Postfix(Player __instance)
            {
                Trigger("PlayerJump", __instance);

                if (JumpCooldown.TryTrigger(__instance))
                {
                    Trigger("PlayerJumpCooldown", __instance);
                }

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
                if (firstContact && Settings.Debug && speed > 10f)
                {
                    Log.LogInfo($"[landing] impact speed {speed:0.0} (hard-landing-speed is {Settings.HardLandingSpeed:0.#})");
                }

                if (firstContact && speed > Settings.HardLandingSpeed)
                {
                    Trigger("PlayerHardLanding", __instance);
                }
            }
        }

        // Player.pyroJumpped is a persistent flag, not a one-frame pulse, so
        // track its last value per-player and only fire on the false->true
        // edge (otherwise this would re-trigger every frame it stays true).
        // Artificer can chain these jumps quickly, so each player also gets a
        // cooldown ("artificer-pyro-jump-cooldown", 10s by default). The edge tracking below always updates, so a jump
        // that's suppressed by the cooldown can't cause a stale edge later.
        [HarmonyPatch(typeof(Player), nameof(Player.ClassMechanicsArtificer))]
        private static class Player_ClassMechanicsArtificer_Patch
        {
            private static readonly ConditionalWeakTable<Player, StrongBox<bool>> LastPyroJumped = new ConditionalWeakTable<Player, StrongBox<bool>>();
            private static readonly Cooldown PyroJumpCooldown = new Cooldown(() => Settings.ArtificerPyroJumpCooldown, () => Time.time);

            [HarmonyPostfix]
            private static void Postfix(Player __instance)
            {
                StrongBox<bool> last = LastPyroJumped.GetOrCreateValue(__instance);
                if (__instance.pyroJumpped && !last.Value && PyroJumpCooldown.TryTrigger(__instance))
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

        // A Snail's "explosion" is Click(): the pop that plays Snail_Pop and
        // sends a stunning shockwave through the room. It runs for a live,
        // "triggered" snail (hit hard, dropped fast, bumped, or jumped on by
        // the player) - Snail.Die() itself does nothing but call the base
        // version, so dying isn't what makes one go off. Click() returns
        // immediately while triggerTicker > 0 without popping, so the prefix
        // records whether this call will really pop.
        [HarmonyPatch(typeof(Snail), nameof(Snail.Click))]
        private static class Snail_Click_Patch
        {
            [HarmonyPrefix]
            private static void Prefix(Snail __instance, out bool __state)
            {
                __state = __instance.triggerTicker <= 0;
            }

            [HarmonyPostfix]
            private static void Postfix(Snail __instance, bool __state)
            {
                if (__state)
                {
                    Trigger("SnailExplosion", __instance);
                }
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

        // LizardJumpModule.Jump() is the launch itself, called once by Lizard
        // when its animation switches to Jumping (it also plays the lizard's
        // own jump sound there). ___lizard reads the module's private field.
        [HarmonyPatch(typeof(LizardJumpModule), nameof(LizardJumpModule.Jump))]
        private static class LizardJumpModule_Jump_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(Lizard ___lizard)
            {
                if (___lizard != null && IsCreatureType(___lizard, CreatureTemplate.Type.CyanLizard))
                {
                    Trigger("CyanLizardJump", ___lizard);
                }
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

        // Scavenger.Throw calls Weapon.Thrown with itself as thrownBy. Spear
        // has no early return, and ExplosiveSpear inherits it while MSC's
        // ElectricSpear overrides Thrown but calls base.Thrown, so this
        // covers every spear type.
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

        // PlayerEnterShelter fires when the camera moves into a shelter room,
        // not when the door shuts. ShelterDoor.DoorClosed (which an earlier
        // version hooked) is the very last tick of the ~8 second closing
        // animation and calls RainWorldGame.Win straight away, so a sound
        // started there is cut off by the sleep screen after a split second.
        //
        // RoomCamera.ChangeRoom is private and runs once when the camera has
        // finished switching rooms (the shortcut handler asks the camera to
        // follow the player out of a pipe, and ChangeRoom runs once the room's
        // texture has loaded); it sets the camera's room to newRoom. Moving
        // between camera screens inside one room never reaches it.
        //
        // Two calls are skipped: the camera's first-ever room (the game placing
        // it in the starting shelter at the start of a cycle, when the camera
        // has no room yet), and a "change" to the room it already showed.
        // Shelters whose door is broken can't be slept in, so they're skipped too.
        [HarmonyPatch(typeof(RoomCamera), "ChangeRoom")]
        private static class RoomCamera_ChangeRoom_Patch
        {
            [HarmonyPrefix]
            private static void Prefix(RoomCamera __instance, out Room __state)
            {
                __state = __instance.room;
            }

            [HarmonyPostfix]
            private static void Postfix(Room newRoom, Room __state)
            {
                if (__state == null || __state == newRoom)
                {
                    return;
                }

                if (newRoom?.shelterDoor == null || newRoom.shelterDoor.Broken)
                {
                    return;
                }

                TriggerNonPositional("PlayerEnterShelter", newRoom);
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
        // RegionGate.Update calls OverWorld.GateRequestsSwitchInitiation exactly
        // once per gate use, at the moment the gate begins its transition (it
        // has just switched to Mode.ClosingAirLock and starts loading the next
        // region). RegionGate.OPENCLOSE, which an earlier version hooked, is
        // only a door-toggle helper the gate's own logic never calls.

        [HarmonyPatch(typeof(OverWorld), nameof(OverWorld.GateRequestsSwitchInitiation))]
        private static class OverWorld_GateRequestsSwitchInitiation_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(RegionGate reportBackToGate)
            {
                TriggerNonPositional("RegionGateTransition", reportBackToGate?.room);
            }
        }

        // --- Predator noticed the player ---------------------------------
        // Tracker.CreatureNoticed is where a creature's AI starts tracking
        // something new - on first sight, or when something asks the tracker
        // for a creature it isn't tracking yet. We only care when that
        // "something" is the player. It returns null (tracking nothing) if the
        // creature isn't realized, is dead, or this AI never tracks it, and
        // callers that ask again each frame would hit that path repeatedly,
        // so only a non-null result counts as a real "noticed". The tracker
        // forgets creatures it hasn't seen for a while, and a creature that
        // keeps losing and re-finding the player would re-fire this
        // constantly, so each creature gets a cooldown (spotted-cooldown).
        //
        // Two kinds of event come out of one sighting:
        //  - PlayerSpottedBy<CreatureType>, for every creature type the game
        //    has (PlayerSpottedByGreenLizard, PlayerSpottedByKingVulture, ...).
        //  - The older grouped events (Predator / MajorThreat / Miros / ...),
        //    kept as they were: at most one of them per sighting, with the more
        //    specific groups taking priority over the generic predator one.

        // Keyed on the creature's AbstractCreature, which outlives the
        // realized object, so a predator that leaves and re-enters the camera
        // range doesn't get a fresh cooldown. Each creature has its own, so
        // several different predators noticing you together can still all fire.
        private static readonly Cooldown SpottedCooldown = new Cooldown(() => Settings.SpottedCooldown, () => Time.time);

        [HarmonyPatch(typeof(Tracker), "CreatureNoticed")]
        private static class Tracker_CreatureNoticed_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(Tracker __instance, AbstractCreature crit, Tracker.CreatureRepresentation __result)
            {
                if (__result == null || !(crit?.realizedObject is Player player))
                {
                    return;
                }

                AbstractCreature observer = __instance.AI?.creature;
                PhysicalObject predator = observer?.realizedObject;
                if (predator == null || predator is Player)
                {
                    return;
                }

                var eventKeys = new List<string>();

                string typeName = CreatureTypeName(predator);
                if (typeName != null)
                {
                    eventKeys.Add(EventCatalog.SpottedKey(typeName));
                }

                string groupKey = null;
                if (predator is Scavenger)
                {
                    groupKey = "PlayerSpottedByScavenger";
                }
                else if (predator is DaddyLongLegs
                    || IsCreatureType(predator, CreatureTemplate.Type.RedLizard)
                    || IsCreatureType(predator, CreatureTemplate.Type.RedCentipede)
                    || IsCreatureType(predator, CreatureTemplate.Type.KingVulture))
                {
                    // Takes priority over the generic predator sound below.
                    groupKey = "PlayerSpottedByMajorThreat";
                }
                else if (predator is MirosBird || (predator is Vulture vulture && vulture.IsMiros))
                {
                    // Miros Vultures are ordinary Vulture objects flagged IsMiros.
                    groupKey = "PlayerSpottedByMiros";
                }
                else if (IsCreatureType(predator, CreatureTemplate.Type.CyanLizard))
                {
                    groupKey = "PlayerSpottedByCyanLizard";
                }
                else if (predator is Lizard || predator is Spider || predator is BigSpider || predator is Vulture)
                {
                    groupKey = "PlayerSpottedByPredator";
                }

                if (groupKey != null)
                {
                    eventKeys.Add(groupKey);
                }

                if (eventKeys.Count > 0 && SpottedCooldown.TryTrigger(observer))
                {
                    TriggerAll(eventKeys, player);
                }
            }
        }

        // --- Creatures dying ------------------------------------------------
        // Creature.Die is virtual and a long list of creature classes override
        // it (some calling base.Die(), some not), so rather than one hand-written
        // patch per class, every Die() declared by a Creature subclass in the
        // game assembly is patched at startup with the same prefix/postfix.
        //
        // A death is reported when the creature's dead flag goes from false to
        // true across the call - Creature.Die's body runs its tail every time
        // it's called, so "Die() was called" alone would repeat for a corpse.
        // Overrides call into base.Die(), so one death is seen by several
        // patches in a single call chain; OnCreatureDied counts it only once.
        //
        // Events: <CreatureType>Death for every creature type (RedLizardDeath,
        // BigSpiderDeath, ...), the older grouped ones (LizardDeath, SpiderDeath,
        // ScavengerDeath - every scavenger variant, CicadaOrLanternMouseDeath),
        // and PlayerDeath for the slugcat.

        private static readonly ConditionalWeakTable<Creature, StrongBox<int>> LastDeathFrame = new ConditionalWeakTable<Creature, StrongBox<int>>();

        private static int PatchCreatureDeaths(Harmony harmony)
        {
            var prefix = new HarmonyMethod(typeof(EventHooks).GetMethod(nameof(CreatureDiePrefix), BindingFlags.NonPublic | BindingFlags.Static));
            var postfix = new HarmonyMethod(typeof(EventHooks).GetMethod(nameof(CreatureDiePostfix), BindingFlags.NonPublic | BindingFlags.Static));

            int patched = 0;
            foreach (Type type in typeof(Creature).Assembly.GetTypes())
            {
                if (!typeof(Creature).IsAssignableFrom(type))
                {
                    continue;
                }

                MethodInfo die = type.GetMethod("Die", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                if (die == null || die.IsAbstract)
                {
                    continue;
                }

                try
                {
                    harmony.Patch(die, prefix, postfix);
                    patched++;
                }
                catch (Exception e)
                {
                    Log.LogError($"Couldn't hook {type.Name}.Die and will miss those deaths: {e.Message}");
                }
            }

            return patched;
        }

        private static void CreatureDiePrefix(Creature __instance, out bool __state)
        {
            __state = __instance.dead;
        }

        private static void CreatureDiePostfix(Creature __instance, bool __state)
        {
            if (!__state && __instance.dead)
            {
                OnCreatureDied(__instance);
            }
        }

        private static void OnCreatureDied(Creature creature)
        {
            StrongBox<int> lastFrame = LastDeathFrame.GetValue(creature, _ => new StrongBox<int>(-1));
            if (lastFrame.Value == Time.frameCount)
            {
                return; // already reported by another Die() further along this same call chain
            }

            lastFrame.Value = Time.frameCount;

            if (creature is Player)
            {
                Trigger("PlayerDeath", creature);
                return;
            }

            var eventKeys = new List<string>();

            string typeName = CreatureTypeName(creature);
            if (typeName != null)
            {
                eventKeys.Add(EventCatalog.DeathKey(typeName));
            }

            if (creature is Scavenger)
            {
                eventKeys.Add("ScavengerDeath");
            }

            if (creature is Lizard)
            {
                eventKeys.Add("LizardDeath");
            }

            if (creature is Spider || creature is BigSpider)
            {
                eventKeys.Add("SpiderDeath");
            }

            if (creature is Cicada || creature is LanternMouse)
            {
                eventKeys.Add("CicadaOrLanternMouseDeath");
            }

            TriggerAll(eventKeys, creature);
        }

        // --- Moving between rooms ---------------------------------------------
        // Creature.NewRoom is called when a creature is placed in a room: on
        // spawning, and again each time it comes out of a shortcut into a
        // different room (Player.NewRoom calls base.NewRoom). The first call for
        // a player is just it being placed in the level, so a transition is only
        // counted when the player already had a room and it's a different one.
        [HarmonyPatch(typeof(Creature), nameof(Creature.NewRoom))]
        private static class Player_RoomTransition_Patch
        {
            private static readonly ConditionalWeakTable<Player, StrongBox<string>> LastRoom = new ConditionalWeakTable<Player, StrongBox<string>>();

            [HarmonyPostfix]
            private static void Postfix(Creature __instance, Room newRoom)
            {
                if (!(__instance is Player player) || player.isNPC || newRoom?.abstractRoom == null)
                {
                    return;
                }

                StrongBox<string> last = LastRoom.GetValue(player, _ => new StrongBox<string>(null));
                string previous = last.Value;
                last.Value = newRoom.abstractRoom.name;

                if (previous != null && previous != last.Value)
                {
                    Trigger("PlayerRoomTransition", player);
                }
            }
        }

        // --- Falling fast --------------------------------------------------------
        // Rain World has no speed cap on falling - gravity just keeps adding -
        // so "terminal velocity" is a speed the player picks (terminal-velocity
        // in the settings). FallTracker fires once when a fall first reaches it
        // and re-arms after landing (or slowing right down, e.g. grabbing a pole).
        [HarmonyPatch(typeof(Player), nameof(Player.Update))]
        private static class Player_Update_FallSpeed_Patch
        {
            private static readonly ConditionalWeakTable<Player, FallTracker> Trackers = new ConditionalWeakTable<Player, FallTracker>();

            [HarmonyPostfix]
            private static void Postfix(Player __instance)
            {
                if (__instance.room == null || __instance.dead || __instance.bodyChunks == null || __instance.bodyChunks.Length == 0)
                {
                    return;
                }

                bool touching = false;
                foreach (BodyChunk chunk in __instance.bodyChunks)
                {
                    if (chunk.ContactPoint.x != 0 || chunk.ContactPoint.y != 0)
                    {
                        touching = true;
                        break;
                    }
                }

                float downSpeed = -__instance.mainBodyChunk.vel.y;
                FallTracker tracker = Trackers.GetValue(__instance, _ => new FallTracker());
                if (tracker.Update(downSpeed, touching, Settings.TerminalVelocity))
                {
                    if (Settings.Debug)
                    {
                        Log.LogInfo($"[fall] reached terminal-velocity: falling at {downSpeed:0.0} (setting is {Settings.TerminalVelocity:0.#})");
                    }

                    Trigger("PlayerTerminalVelocity", __instance);
                }
            }
        }

        // --- Shared playback logic --------------------------------------

        /// <summary>
        /// Something happened to/at source: plays its event, positioned at it
        /// (the source's own room and position, so it pans and fades naturally).
        /// Events about the player are heard even if the camera hasn't caught
        /// up to the room yet; events about other creatures only if that room
        /// is on screen.
        /// </summary>
        private static void Trigger(string eventKey, PhysicalObject source)
        {
            if (source?.room == null || source.bodyChunks == null || source.bodyChunks.Length == 0)
            {
                return;
            }

            SoundScope scope = source is Player ? SoundScope.Player : SoundScope.World;
            SoundboardRuntime.Fire(eventKey, source.room, source.bodyChunks[0].pos, scope);
        }

        /// <summary>Several events for one thing happening (e.g. a Red Lizard dying is both RedLizardDeath and LizardDeath); each fires once.</summary>
        private static void TriggerAll(IEnumerable<string> eventKeys, PhysicalObject source)
        {
            foreach (string key in eventKeys.Distinct())
            {
                Trigger(key, source);
            }
        }

        /// <summary>The creature's type name as the game spells it ("RedLizard", "KingVulture", ...), or null.</summary>
        private static string CreatureTypeName(PhysicalObject obj)
        {
            return (obj as Creature)?.abstractCreature?.creatureTemplate?.type?.value;
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

        /// <summary>Plays an event at a spot in a room, for things that aren't a PhysicalObject.</summary>
        private static void TriggerAt(string eventKey, Room room, Vector2 pos)
        {
            SoundboardRuntime.Fire(eventKey, room, pos, SoundScope.World);
        }

        /// <summary>Plays an event with no position (centred), for things that are about the player rather than a spot in the world.</summary>
        private static void TriggerNonPositional(string eventKey, Room room)
        {
            SoundboardRuntime.Fire(eventKey, room, null, SoundScope.Player);
        }
    }
}
