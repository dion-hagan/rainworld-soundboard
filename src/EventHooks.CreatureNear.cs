using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace SoundboardMod
{
    public static partial class EventHooks
    {
        // --- Creatures coming near the player ------------------------------------
        // <CreatureType>Near (RedLizardNear, ScavengerEliteNear, ...) fires when a creature of
        // that type comes within 'creature-near-distance' tiles of the player. The game has no
        // event for this, so it's a poll on Player.Update; to keep it cheap:
        //  - it only runs every NearTracker.ScanIntervalTicks ticks (a quarter of a second),
        //  - it does nothing at all unless soundboard.yaml has a sound for some ...Near event,
        //  - it only walks the creatures physically in the player's own room
        //    (Room.physicalObjects, plain index loops, nothing allocated), and only looks
        //    closely at creatures of a type that actually has a sound.
        //
        // Same room only: a creature in the next room, or inside a shortcut/den (both are taken
        // out of physicalObjects: Creature.SuckedIntoShortCut calls Room.RemoveObject), isn't
        // near. Distance is straight-line, in tiles (20 pixels), from the player's main body
        // chunk to the closest body chunk of the creature - so a long creature (Daddy Long Legs,
        // Deer, a centipede) counts when any part of it is close. Walls don't block it.
        //
        // Skipped: dead creatures, other slugcats (Player objects - slugpups included), and a
        // creature the player is holding: that one is tracked but never fires, so it doesn't
        // announce itself when put down next to you. NearTracker (which is unit tested) does the
        // rest: edge triggering, hysteresis, and forgetting creatures that left.

        private const float PixelsPerTile = 20f; // a tile is 20 pixels (Room.MiddleOfTile: 10 + x * 20)

        // Keyed on the creature's AbstractCreature, which outlives the realized object. One
        // instance shared by every player, so a creature between two players fires once.
        private static readonly Cooldown NearCooldown = new Cooldown(() => Settings.CreatureNearCooldown, () => Time.time);

        private sealed class NearState
        {
            public NearTracker Tracker = new NearTracker(NearCooldown);
            public Room LastRoom;
        }

        // The creature types that have a ...Near event with a sound in the current config,
        // rebuilt whenever the config is reloaded (each load is a new SoundboardConfig object).
        private static SoundboardConfig nearTypesFor;
        private static readonly HashSet<string> NearTypes = new HashSet<string>();

        private static bool AnyNearEventConfigured()
        {
            SoundboardConfig config = SoundboardRuntime.Config;
            if (!ReferenceEquals(config, nearTypesFor))
            {
                NearTypes.Clear();
                foreach (EventBinding binding in config.Events)
                {
                    string type = EventCatalog.CreatureTypeOfNearKey(binding.EventName);
                    if (type != null)
                    {
                        NearTypes.Add(type);
                    }
                }

                nearTypesFor = config;
            }

            return NearTypes.Count > 0;
        }

        [HarmonyPatch(typeof(Player), nameof(Player.Update))]
        private static class Player_Update_CreatureNear_Patch
        {
            private static readonly ConditionalWeakTable<Player, NearState> States = new ConditionalWeakTable<Player, NearState>();

            [HarmonyPostfix]
            private static void Postfix(Player __instance)
            {
                if (__instance.dead || __instance.room == null || __instance.bodyChunks == null || __instance.bodyChunks.Length == 0)
                {
                    return;
                }

                // isNPC (slugpups) walks the creature template's ancestry, so it's only
                // checked on the ticks that will actually scan.
                NearState state = States.GetValue(__instance, _ => new NearState());
                if (!state.Tracker.Tick() || __instance.isNPC || !AnyNearEventConfigured())
                {
                    return;
                }

                Room room = __instance.room;
                if (!ReferenceEquals(room, state.LastRoom))
                {
                    // Left and came back, or just arrived: everything already near counts as arriving.
                    state.Tracker.Reset();
                    state.LastRoom = room;
                }

                Scan(__instance, room, state.Tracker);
            }

            private static void Scan(Player player, Room room, NearTracker tracker)
            {
                List<PhysicalObject>[] layers = room.physicalObjects;
                if (layers == null)
                {
                    return;
                }

                Vector2 origin = player.mainBodyChunk.pos;
                float near = Settings.CreatureNearDistance * PixelsPerTile;

                tracker.BeginScan();
                for (int layer = 0; layer < layers.Length; layer++)
                {
                    List<PhysicalObject> objects = layers[layer];
                    if (objects == null)
                    {
                        continue;
                    }

                    for (int i = 0; i < objects.Count; i++)
                    {
                        if (!(objects[i] is Creature creature) || creature is Player || creature.dead || creature.slatedForDeletetion)
                        {
                            continue;
                        }

                        AbstractCreature abstractCreature = creature.abstractCreature;
                        string typeName = abstractCreature?.creatureTemplate?.type?.value;
                        if (typeName == null || !NearTypes.Contains(typeName))
                        {
                            continue;
                        }

                        float distance = DistanceToClosestChunk(creature, origin);
                        if (tracker.Observe(abstractCreature, distance, near, allowFire: !IsHeldBy(player, creature)))
                        {
                            if (Settings.Debug)
                            {
                                Log.LogInfo($"[near] {typeName} is {distance / PixelsPerTile:0.0} tiles away (creature-near-distance is {Settings.CreatureNearDistance:0.#})");
                            }

                            Trigger(EventCatalog.NearKey(typeName), creature);
                        }
                    }
                }

                tracker.EndScan();
            }

            private static float DistanceToClosestChunk(Creature creature, Vector2 from)
            {
                BodyChunk[] chunks = creature.bodyChunks;
                if (chunks == null || chunks.Length == 0)
                {
                    return float.MaxValue;
                }

                float best = float.MaxValue;
                for (int i = 0; i < chunks.Length; i++)
                {
                    float squared = (chunks[i].pos - from).sqrMagnitude;
                    if (squared < best)
                    {
                        best = squared;
                    }
                }

                return Mathf.Sqrt(best);
            }

            private static bool IsHeldBy(Player player, Creature creature)
            {
                Creature.Grasp[] grasps = player.grasps;
                if (grasps == null)
                {
                    return false;
                }

                for (int i = 0; i < grasps.Length; i++)
                {
                    if (grasps[i]?.grabbed == creature)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
