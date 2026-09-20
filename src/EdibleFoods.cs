using System;
using System.Collections.Generic;
using System.Linq;

namespace SoundboardMod
{
    /// <summary>One kind of non-creature food: the game's object type, and how the event describes it.</summary>
    public sealed class EdibleFood
    {
        /// <summary>
        /// The value of the food's AbstractPhysicalObject.AbstractObjectType, exactly as the game spells it
        /// (this is NOT always the class name: SwollenWaterNut is the type "WaterNut"). The event is
        /// EventCatalog.EatKey of this.
        /// </summary>
        public string TypeName;

        /// <summary>What the event description says the slugcat eats, e.g. "a Blue Fruit (dangle fruit)".</summary>
        public string Eats;
    }

    /// <summary>
    /// Every food the slugcat can eat that isn't a creature, for the PlayerEat&lt;Food&gt; events.
    ///
    /// This is a fixed, hand-checked list rather than something found at startup: the game gives no way to
    /// ask "which object types are edible" (a type only turns into a class inside the big switch in
    /// AbstractPhysicalObject.Realize), so the alternatives were guessing from class names or loading an
    /// instance of every object type. The list was made by decompiling Rain World v1.11.8 and taking every
    /// class that implements IPlayerEdible but isn't a Creature, then reading its Realize() branch for the
    /// object type. Every one of them calls Player.ObjectEaten from its BitByPlayer. Foods that are creatures
    /// (Batflies, Vulture Grubs, Hazers, ...) are not here: PlayerEatCreature and the per-creature events cover them.
    ///
    /// The Slugcat DLC and Watcher foods are listed even when that DLC isn't switched on. That keeps the
    /// list of valid event names (and so a shared soundboard.yaml) the same for everyone; the event just
    /// never fires. A food added by another mod isn't here, so has no event.
    /// </summary>
    public static class EdibleFoods
    {
        public static readonly IReadOnlyList<EdibleFood> All = new[]
        {
            // Vanilla
            new EdibleFood { TypeName = "DangleFruit", Eats = "a Blue Fruit (dangle fruit)" },
            new EdibleFood { TypeName = "SlimeMold", Eats = "a Slime Mold" },
            new EdibleFood { TypeName = "Mushroom", Eats = "a Mushroom" },
            new EdibleFood { TypeName = "WaterNut", Eats = "a Bubble Fruit (the game calls it a water nut, or swollen water nut)" },
            new EdibleFood { TypeName = "JellyFish", Eats = "a Jellyfish" },
            new EdibleFood { TypeName = "KarmaFlower", Eats = "a Karma Flower" },
            new EdibleFood { TypeName = "EggBugEgg", Eats = "an Eggbug egg" },
            new EdibleFood { TypeName = "SSOracleSwarmer", Eats = "a Neuron Fly (the ordinary kind, from around Five Pebbles)" },
            new EdibleFood { TypeName = "SLOracleSwarmer", Eats = "one of Looks to the Moon's neuron flies (the kind that makes you glow)" },

            // More Slugcats
            new EdibleFood { TypeName = "DandelionPeach", Eats = "a Dandelion Peach" },
            new EdibleFood { TypeName = "FireEgg", Eats = "a Fire Egg" },
            new EdibleFood { TypeName = "GlowWeed", Eats = "a Glow Weed" },
            new EdibleFood { TypeName = "GooieDuck", Eats = "a Gooieduck" },
            new EdibleFood { TypeName = "LillyPuck", Eats = "a Lilypuck" },

            // Watcher
            new EdibleFood { TypeName = "FireSpriteLarva", Eats = "a Box Worm larva (the game calls it a Fire Sprite larva)" },
        };

        private static readonly HashSet<string> Known = new HashSet<string>(All.Select(f => f.TypeName));

        /// <summary>True if typeName (an AbstractObjectType value) is one of the foods above.</summary>
        public static bool IsKnown(string typeName)
        {
            return typeName != null && Known.Contains(typeName);
        }

        /// <summary>
        /// The event to fire when the slugcat finishes eating an object of the given AbstractObjectType value,
        /// or null if it isn't a food we have an event for.
        /// </summary>
        public static string EventFor(string typeName)
        {
            return IsKnown(typeName) ? EventCatalog.EatKey(typeName) : null;
        }
    }
}
