using System.Linq;
using Xunit;

namespace SoundboardMod.Tests
{
    /// <summary>Diving, running low on air and drowning: each reports once and re-arms (BreathTracker; the game reads live in EventHooks.Water.cs).</summary>
    public class BreathTrackerTests
    {
        private const float Low = 1f / 3f; // SlugcatStats.drownThreshold

        // Head out of the water, plenty of air.
        private static BreathEvent Dry(BreathTracker t, float air = 1f) => t.Update(true, false, false, air, Low, 0f);

        // Head under water and swimming.
        private static BreathEvent Dive(BreathTracker t, float air = 1f) => t.Update(true, true, true, air, Low, 0f);

        // Head under water but not swimming (standing on the floor of a flooded room, on a pole...).
        private static BreathEvent Submerged(BreathTracker t, float air) => t.Update(true, false, true, air, Low, 0f);

        // --- swimming underwater ------------------------------------------------

        [Fact]
        public void DivingFiresOnceWhenTheSwimStarts()
        {
            var t = new BreathTracker();
            Assert.Equal(BreathEvent.None, Dry(t));
            Assert.Equal(BreathEvent.DivedUnderwater, Dive(t));
            Assert.Equal(BreathEvent.None, Dive(t));
            Assert.Equal(BreathEvent.None, Dive(t, 0.9f));
        }

        [Fact]
        public void EachNewDiveAfterSurfacingFiresAgain()
        {
            var t = new BreathTracker();
            Assert.Equal(BreathEvent.DivedUnderwater, Dive(t));
            Assert.Equal(BreathEvent.None, Dry(t));
            Assert.Equal(BreathEvent.DivedUnderwater, Dive(t));
        }

        [Fact]
        public void BeingSubmergedWithoutSwimmingIsNotADive()
        {
            var t = new BreathTracker();
            Assert.Equal(BreathEvent.None, Submerged(t, 1f));
            Assert.Equal(BreathEvent.None, Submerged(t, 0.9f));
            Assert.Equal(BreathEvent.DivedUnderwater, Dive(t, 0.9f)); // ...until they actually start to swim
        }

        [Fact]
        public void TheFirstUpdateInsideWaterCountsAsADive()
        {
            // e.g. the player's first update after spawning underwater
            Assert.Equal(BreathEvent.DivedUnderwater, Dive(new BreathTracker()));
        }

        // --- running low on air -------------------------------------------------

        [Fact]
        public void RunningLowFiresOnceWhenAirDropsBelowTheThreshold()
        {
            var t = new BreathTracker();
            Assert.Equal(BreathEvent.DivedUnderwater, Dive(t));
            Assert.Equal(BreathEvent.None, Dive(t, 0.5f));
            Assert.Equal(BreathEvent.None, Dive(t, Low)); // exactly at it is not below it
            Assert.Equal(BreathEvent.StartedDrowning, Dive(t, Low - 0.01f));
            Assert.Equal(BreathEvent.None, Dive(t, 0.2f));
            Assert.Equal(BreathEvent.None, Dive(t, 0f));
        }

        [Fact]
        public void LowAirCountsWhenSubmergedEvenIfNotSwimming()
        {
            var t = new BreathTracker();
            Assert.Equal(BreathEvent.StartedDrowning, Submerged(t, 0.2f));
        }

        [Fact]
        public void LowAirOutOfTheWaterIsNotDrowning()
        {
            // The game keeps a slugcat "exhausted" after surfacing, and air refills from a low point.
            var t = new BreathTracker();
            Assert.Equal(BreathEvent.None, Dry(t, 0.1f));
            Assert.Equal(BreathEvent.None, Dry(t, 0.2f));
        }

        [Fact]
        public void ItThenSitsOutUntilMostOfTheBreathIsBack()
        {
            var t = new BreathTracker();
            Assert.Equal(BreathEvent.StartedDrowning, Submerged(t, 0.3f));

            // Surfacing and gulping air, then dropping back under before it has recovered: still the same struggle.
            Assert.Equal(BreathEvent.None, Dry(t, 0.5f));
            Assert.Equal(BreathEvent.None, Submerged(t, 0.3f));
            Assert.Equal(BreathEvent.None, Submerged(t, 0.2f));

            // Air comes back up past the recovered level (2/3 for a 1/3 threshold), then it can fire again.
            Assert.Equal(BreathEvent.None, Dry(t, BreathTracker.RecoveredAirLevel(Low)));
            Assert.Equal(BreathEvent.None, Submerged(t, 0.5f));
            Assert.Equal(BreathEvent.StartedDrowning, Submerged(t, 0.3f));
        }

        [Fact]
        public void RecoveredLevelIsHalfwayBetweenTheThresholdAndFullLungs()
        {
            Assert.Equal(2f / 3f, BreathTracker.RecoveredAirLevel(1f / 3f), 5);
            Assert.Equal(0.5f, BreathTracker.RecoveredAirLevel(0f), 5);
            Assert.Equal(0.95f, BreathTracker.RecoveredAirLevel(0.9f), 5); // a mod with a different threshold still re-arms below full
        }

        [Fact]
        public void TheThresholdIsWhateverTheSlugcatHas()
        {
            var t = new BreathTracker();
            // A slugcat whose out-of-breath level is 0.5: 0.4 air is low for it, but wouldn't be with the default 1/3.
            Assert.Equal(BreathEvent.StartedDrowning, t.Update(true, false, true, 0.4f, 0.5f, 0f));

            var other = new BreathTracker();
            Assert.Equal(BreathEvent.None, other.Update(true, false, true, 0.4f, Low, 0f));
        }

        [Fact]
        public void BubbleGrassOrAirPocketsRefillingAirRearmsItAtOnce()
        {
            var t = new BreathTracker();
            Assert.Equal(BreathEvent.StartedDrowning, Submerged(t, 0.2f));
            Assert.Equal(BreathEvent.None, Submerged(t, 1f)); // airInLungs set straight to 1 by the game
            Assert.Equal(BreathEvent.StartedDrowning, Submerged(t, 0.2f));
        }

        [Fact]
        public void DivingAndRunningLowCanHappenInTheSameUpdate()
        {
            var t = new BreathTracker();
            Assert.Equal(BreathEvent.DivedUnderwater | BreathEvent.StartedDrowning, Dive(t, 0.1f));
        }

        // --- drowning -----------------------------------------------------------

        [Fact]
        public void DyingWithTheDrowningCounterFullIsADrowning()
        {
            var t = new BreathTracker();
            Assert.Equal(BreathEvent.DivedUnderwater | BreathEvent.StartedDrowning, Dive(t, 0.1f));
            Assert.Equal(BreathEvent.None, t.Update(true, true, true, 0f, Low, 0.5f));
            Assert.Equal(BreathEvent.Drowned, t.Update(false, false, true, 0f, Low, 1f));
            Assert.Equal(BreathEvent.None, t.Update(false, false, true, 0f, Low, 1f)); // and only once
        }

        [Fact]
        public void DyingOfSomethingElseUnderwaterIsNotADrowning()
        {
            var t = new BreathTracker();
            Dive(t, 0.9f);
            Assert.Equal(BreathEvent.None, t.Update(false, false, true, 0.9f, Low, 0f));

            var stunned = new BreathTracker();
            Dive(stunned, 0.1f);
            Assert.Equal(BreathEvent.None, stunned.Update(false, false, true, 0f, Low, 0.4f)); // eaten while already drowning: the counter never filled
        }

        [Fact]
        public void ACorpseSeenForTheFirstTimeIsNotReportedAsDrowned()
        {
            var t = new BreathTracker();
            Assert.Equal(BreathEvent.None, t.Update(false, false, false, 0f, Low, 1f));
        }

        [Fact]
        public void ADeadPlayerFiresNothingElse()
        {
            var t = new BreathTracker();
            Dive(t);
            Assert.Equal(BreathEvent.None, t.Update(false, true, true, 0.1f, Low, 0f));
            Assert.Equal(BreathEvent.None, t.Update(false, true, true, 0.05f, Low, 0f));
        }

        [Fact]
        public void ARevivedPlayerStartsFresh()
        {
            var t = new BreathTracker();
            Assert.Equal(BreathEvent.DivedUnderwater | BreathEvent.StartedDrowning, Dive(t, 0.1f));
            Assert.Equal(BreathEvent.Drowned, t.Update(false, false, true, 0f, Low, 1f));

            Assert.Equal(BreathEvent.DivedUnderwater, Dive(t)); // alive again with a full breath
            Assert.Equal(BreathEvent.StartedDrowning, Submerged(t, 0.1f));
        }

        [Fact]
        public void EachPlayerHasItsOwnTracker()
        {
            var a = new BreathTracker();
            var b = new BreathTracker();
            Assert.Equal(BreathEvent.DivedUnderwater, Dive(a));
            Assert.Equal(BreathEvent.DivedUnderwater, Dive(b));
            Assert.Equal(BreathEvent.None, Dive(a));
            Assert.Equal(BreathEvent.None, Dry(b));
        }
    }

    public class WaterEventCatalogTests
    {
        private static readonly EventCatalog Catalog = new EventCatalog(new string[0]);

        [Fact]
        public void TheWaterEventsAreDeclaredInTheirOwnSection()
        {
            foreach (string name in new[] { "PlayerSwimUnderwater", "PlayerDrowning", "PlayerDrowned" })
            {
                Assert.Equal(name, Catalog.Resolve(name));
                Assert.Equal(EventCatalog.SectionWater, Catalog.All.Single(e => e.Name == name).Section);
            }
        }

        [Fact]
        public void TheCooldownSettingParsesAndIsBounded()
        {
            SoundboardConfig config = SoundboardConfigParser.Parse("settings:\n  swim-underwater-cooldown: 12\n", Catalog);
            Assert.Empty(config.Issues);
            Assert.Equal(12f, config.Settings.SwimUnderwaterCooldown);
            Assert.Equal(5f, new SoundboardSettings().SwimUnderwaterCooldown);

            SoundboardConfig typo = SoundboardConfigParser.Parse("settings:\n  swim-underwater-cooldwn: 1\n", Catalog);
            Assert.Contains("Did you mean 'swim-underwater-cooldown'?", typo.Issues.Single().Message);
        }
    }
}
