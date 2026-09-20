using System.Linq;
using Xunit;

namespace SoundboardMod.Tests
{
    /// <summary>The PlayerEat&lt;Food&gt; events: which object types they cover and how they're named.</summary>
    public class FoodEventTests
    {
        // A creature list with names that could collide with (or be confused for) food events.
        private static readonly EventCatalog Catalog = new EventCatalog(new[] { "Fly", "VultureGrub", "SmallNeedleWorm", "Centipede", "Hazer", "Scavenger", "RedLizard" });

        // The object types found by decompiling v1.11.8: every non-Creature class implementing IPlayerEdible.
        private static readonly string[] ExpectedTypes =
        {
            "DangleFruit", "SlimeMold", "Mushroom", "WaterNut", "JellyFish", "KarmaFlower", "EggBugEgg",
            "SSOracleSwarmer", "SLOracleSwarmer",
            "DandelionPeach", "FireEgg", "GlowWeed", "GooieDuck", "LillyPuck",
            "FireSpriteLarva",
        };

        [Fact]
        public void EatKeyIsPlayerEatPlusTheObjectType()
        {
            Assert.Equal("PlayerEatDangleFruit", EventCatalog.EatKey("DangleFruit"));
            Assert.Equal("PlayerEatWaterNut", EventCatalog.EatKey("WaterNut"));
            Assert.Equal("PlayerEatSlimeMold", EventCatalog.EatKey("SlimeMold"));
        }

        [Fact]
        public void TheFoodListCoversEveryVerifiedNonCreatureEdible()
        {
            Assert.Equal(ExpectedTypes.OrderBy(t => t), EdibleFoods.All.Select(f => f.TypeName).OrderBy(t => t));
        }

        [Fact]
        public void EveryFoodHasItsOwnEventInTheCatalog()
        {
            foreach (string type in ExpectedTypes)
            {
                EventInfo info = Catalog.All.SingleOrDefault(e => e.Name == "PlayerEat" + type);
                Assert.NotNull(info);
                Assert.Equal(EventCatalog.SectionFood, info.Section);
                Assert.StartsWith("The slugcat eats ", info.Description);
                Assert.EndsWith(".", info.Description);
            }
        }

        [Fact]
        public void TheFoodEventsDoNotDependOnTheCreatureList()
        {
            var withoutCreatures = new EventCatalog(new string[0]);
            var foodEvents = withoutCreatures.All.Where(e => e.Section == EventCatalog.SectionFood).Select(e => e.Name).OrderBy(n => n);
            Assert.Equal(ExpectedTypes.Select(EventCatalog.EatKey).OrderBy(n => n), foodEvents);
        }

        [Fact]
        public void FoodEventNamesAreUniqueAndLeaveTheOlderEatEventsAlone()
        {
            var normalized = Catalog.All.Select(e => NameMatch.Normalize(e.Name)).ToList();
            Assert.Equal(normalized.Count, normalized.Distinct().Count());

            Assert.Equal(EventCatalog.SectionPlayer, Catalog.All.Single(e => e.Name == "PlayerEat").Section);
            Assert.Equal(EventCatalog.SectionPlayer, Catalog.All.Single(e => e.Name == "PlayerEatCreature").Section);
            Assert.DoesNotContain(ExpectedTypes, t => EventCatalog.EatKey(t) == "PlayerEat" || EventCatalog.EatKey(t) == "PlayerEatCreature");
        }

        [Fact]
        public void CreaturesAreNotFoodEvents()
        {
            // Creature meat is PlayerEatCreature; these all implement IPlayerEdible but are Creatures.
            foreach (string creature in new[] { "Fly", "VultureGrub", "SmallNeedleWorm", "Centipede", "Hazer", "Frog", "Rat", "Tardigrade", "Barnacle", "SandGrub" })
            {
                Assert.False(EdibleFoods.IsKnown(creature), creature);
                Assert.Null(EdibleFoods.EventFor(creature));
                Assert.Null(Catalog.Resolve("PlayerEat" + creature));
            }
        }

        [Fact]
        public void EventForMapsAKnownTypeToItsEventAndAnythingElseToNull()
        {
            Assert.Equal("PlayerEatMushroom", EdibleFoods.EventFor("Mushroom"));
            Assert.Equal("PlayerEatFireSpriteLarva", EdibleFoods.EventFor("FireSpriteLarva"));
            Assert.Null(EdibleFoods.EventFor("Rock"));
            Assert.Null(EdibleFoods.EventFor("SomeModdedFruit"));
            Assert.Null(EdibleFoods.EventFor(null));
            Assert.Null(EdibleFoods.EventFor(""));
        }

        [Fact]
        public void EveryFoodEventIsFoundByTheForgivingNameMatching()
        {
            foreach (string type in ExpectedTypes)
            {
                string key = EventCatalog.EatKey(type);
                Assert.Equal(key, Catalog.Resolve(key));
                Assert.Equal(key, Catalog.Resolve(key.ToLowerInvariant()));
            }

            Assert.Equal("PlayerEatDangleFruit", Catalog.Resolve("player eat dangle-fruit"));

            // The shorter "PlayerEat" is a prefix of every food event, but a typo should still point at the real one.
            Assert.Equal("PlayerEatGooieDuck", Catalog.Suggest("PlayerEatGooeyDuck"));
            Assert.NotNull(Catalog.Suggest("Player")); // a bare prefix still suggests something
        }

        [Fact]
        public void TheConfigAcceptsEveryFoodEventName()
        {
            string yaml = "events:\n" + string.Concat(ExpectedTypes.Select(t => "  " + EventCatalog.EatKey(t) + ":\n    - munch.wav\n"));
            SoundboardConfig config = SoundboardConfigParser.Parse(yaml, Catalog);

            Assert.Empty(config.Issues);
            Assert.Equal(ExpectedTypes.Length, config.Events.Count);
            Assert.All(config.Events, e => Assert.Single(e.Choices));
        }

        [Fact]
        public void AMistypedFoodEventGetsADidYouMeanSuggestion()
        {
            SoundboardConfig config = SoundboardConfigParser.Parse("events:\n  PlayerEatSlimemould:\n    - munch.wav\n", Catalog);

            Assert.Single(config.Issues);
            Assert.Contains("PlayerEatSlimeMold", config.Issues[0].ToString());
        }

        [Fact]
        public void TheEventListFileHasAFoodSectionWithEveryFood()
        {
            string text = Catalog.DescribeAll();

            Assert.Contains("== " + EventCatalog.SectionFood + " ==", text);
            foreach (string type in ExpectedTypes)
            {
                Assert.Contains("  " + EventCatalog.EatKey(type) + "\n", text.Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void EveryFoodNamesWhatItIsInItsDescription()
        {
            Assert.All(EdibleFoods.All, f => Assert.False(string.IsNullOrWhiteSpace(f.Eats)));
            Assert.Contains("Bubble Fruit", Catalog.All.Single(e => e.Name == "PlayerEatWaterNut").Description);
        }
    }
}
