using System.Linq;
using Xunit;

namespace SoundboardMod.Tests
{
    /// <summary>The "&lt;Creature&gt;Near" events: the generated names, their settings, and the in-range/cooldown logic.</summary>
    public class CreatureNearTests
    {
        private static readonly EventCatalog Catalog = new EventCatalog(new[] { "RedLizard", "Scavenger", "Spider", "MirosVulture" });

        private static SoundboardConfig Parse(string yaml) => SoundboardConfigParser.Parse(yaml, Catalog);

        // --- the generated events ------------------------------------------------------

        [Fact]
        public void EveryCreatureTypeGetsANearEvent()
        {
            Assert.Equal("RedLizardNear", Catalog.Resolve("RedLizardNear"));
            Assert.Equal("MirosVultureNear", Catalog.Resolve("miros vulture near"));
            Assert.Equal("SpiderNear", Catalog.Resolve("SPIDER-NEAR"));
            Assert.Equal("RedLizardNear", EventCatalog.NearKey("RedLizard"));
        }

        [Fact]
        public void NearEventsLiveInTheirOwnSectionAndHaveDescriptions()
        {
            EventInfo info = Catalog.All.Single(e => e.Name == "RedLizardNear");
            Assert.Equal(EventCatalog.SectionNear, info.Section);
            Assert.Contains("RedLizard", info.Description);
            Assert.Contains("creature-near-distance", info.Description);
            Assert.Contains("creature-near-cooldown", info.Description);
            Assert.Equal(Catalog.All.Count, Catalog.All.Select(e => NameMatch.Normalize(e.Name)).Distinct().Count());
        }

        [Fact]
        public void ThereIsOneNearEventPerCreatureTypeAndNoFixedOnes()
        {
            var types = new[] { "RedLizard", "Scavenger", "Spider", "MirosVulture" };
            string[] near = Catalog.All.Where(e => e.Section == EventCatalog.SectionNear).Select(e => e.Name).ToArray();
            Assert.Equal(types.Select(EventCatalog.NearKey).OrderBy(n => n), near.OrderBy(n => n));

            // With no creature list there are no near events at all - the family is generated, not hand-listed.
            Assert.DoesNotContain(new EventCatalog(new string[0]).All, e => e.Section == EventCatalog.SectionNear);
        }

        [Fact]
        public void ANearEventIsNotConfusedWithTheOtherFamilies()
        {
            Assert.Equal("RedLizardDeath", Catalog.Resolve("RedLizardDeath"));
            Assert.Equal("PlayerSpottedByRedLizard", Catalog.Resolve("PlayerSpottedByRedLizard"));
            Assert.Null(Catalog.Resolve("PlayerNearRedLizard"));
        }

        [Fact]
        public void NearEventsAppearInTheEventsFile()
        {
            string text = Catalog.DescribeAll();
            Assert.Contains("== " + EventCatalog.SectionNear + " ==", text);
            Assert.Contains("  RedLizardNear", text);
            Assert.Contains("  MirosVultureNear", text);
        }

        [Fact]
        public void TheSpecificationIsRecoverableFromTheKey()
        {
            Assert.Equal("RedLizard", EventCatalog.CreatureTypeOfNearKey(EventCatalog.NearKey("RedLizard")));
            Assert.Null(EventCatalog.CreatureTypeOfNearKey("PlayerDeath"));
            Assert.Null(EventCatalog.CreatureTypeOfNearKey("Near"));
            Assert.Null(EventCatalog.CreatureTypeOfNearKey(null));
        }

        [Fact]
        public void EveryCatalogNearEventMapsBackToItsCreatureType()
        {
            foreach (EventInfo info in Catalog.All.Where(e => e.Section == EventCatalog.SectionNear))
            {
                string type = EventCatalog.CreatureTypeOfNearKey(info.Name);
                Assert.NotNull(type);
                Assert.Equal(info.Name, EventCatalog.NearKey(type));
            }
        }

        // --- the config ----------------------------------------------------------------

        [Fact]
        public void TheConfigAcceptsANearEventInAnySpelling()
        {
            SoundboardConfig config = Parse("events:\n  red lizard near:\n    - a.wav\n  MirosVultureNear:\n    - b.wav\n");

            Assert.Empty(config.Issues);
            Assert.Equal(new[] { "RedLizardNear", "MirosVultureNear" }, config.Events.Select(e => e.EventName));
        }

        [Fact]
        public void ANearEventForAnUnknownCreatureIsFlaggedWithASuggestion()
        {
            SoundboardConfig config = Parse("events:\n  RedLizrdNear:\n    - a.wav\n");

            ConfigIssue issue = Assert.Single(config.Issues);
            Assert.Contains("isn't an event", issue.Message);
            Assert.Contains("RedLizardNear", issue.Message);
        }

        [Fact]
        public void TheAddSoundTabCanAddASoundToANearEvent()
        {
            YamlEditor.Result result = SoundAdder.Add("events:\n  PlayerDeath:\n    - boom.wav\n", NewSound.Single("red lizard near", "growl.wav"), Catalog);

            Assert.True(result.Ok, result.Error);
            Assert.Contains("RedLizardNear:", result.Text);
            Assert.Equal("growl.wav", Parse(result.Text).Events.Single(e => e.EventName == "RedLizardNear").Choices.Single().Sounds.Single().File);
        }

        [Fact]
        public void NearSettingsHaveDefaultsAndAreParsed()
        {
            SoundboardSettings defaults = Parse(string.Empty).Settings;
            Assert.Equal(10f, defaults.CreatureNearDistance);
            Assert.Equal(10f, defaults.CreatureNearCooldown);

            SoundboardConfig config = Parse("settings:\n  creature-near-distance: 15\n  Creature Near Cooldown: 2.5\n");
            Assert.Empty(config.Issues);
            Assert.Equal(15f, config.Settings.CreatureNearDistance);
            Assert.Equal(2.5f, config.Settings.CreatureNearCooldown);
        }

        [Fact]
        public void NearSettingsAreKeptInRange()
        {
            SoundboardConfig config = Parse("settings:\n  creature-near-distance: 0\n  creature-near-cooldown: -1\n");
            Assert.Equal(2, config.Issues.Count);
            Assert.Equal(1f, config.Settings.CreatureNearDistance);
            Assert.Equal(0f, config.Settings.CreatureNearCooldown);

            Assert.Equal(200f, Parse("settings:\n  creature-near-distance: 5000\n").Settings.CreatureNearDistance);
        }

        // --- the tracker -----------------------------------------------------------------

        private sealed class Clock
        {
            public float Now;
        }

        private sealed class Creature
        {
            public readonly string Name;

            public Creature(string name)
            {
                Name = name;
            }
        }

        private static NearTracker NewTracker(Clock clock, float cooldownSeconds = 10f) =>
            new NearTracker(new Cooldown(cooldownSeconds, () => clock.Now));

        /// <summary>One scan in which each creature is observed at the given distance (range 10, holding nothing).</summary>
        private static bool[] Scan(NearTracker tracker, params (Creature creature, float distance)[] seen)
        {
            tracker.BeginScan();
            bool[] fired = seen.Select(s => tracker.Observe(s.creature, s.distance, 10f, allowFire: true)).ToArray();
            tracker.EndScan();
            return fired;
        }

        [Fact]
        public void FiresWhenACreatureComesIntoRange()
        {
            var tracker = NewTracker(new Clock());
            var lizard = new Creature("lizard");

            Assert.False(Scan(tracker, (lizard, 30f))[0]);
            Assert.False(Scan(tracker, (lizard, 12f))[0]);
            Assert.True(Scan(tracker, (lizard, 9f))[0]);
        }

        [Fact]
        public void TheEdgeCountsAsInsideAndEveryoneElseStaysOutside()
        {
            var tracker = NewTracker(new Clock());
            var exactly = new Creature("exactly at the range");
            var justOutside = new Creature("just outside");

            bool[] fired = Scan(tracker, (exactly, 10f), (justOutside, 10.01f));

            Assert.True(fired[0]);
            Assert.False(fired[1]);
        }

        [Fact]
        public void DoesNotRepeatWhileTheCreatureStaysInRange()
        {
            var clock = new Clock();
            var tracker = NewTracker(clock, cooldownSeconds: 0f); // no cooldown: only the edge logic holds it back
            var lizard = new Creature("lizard");

            Assert.True(Scan(tracker, (lizard, 8f))[0]);
            for (int i = 0; i < 20; i++)
            {
                clock.Now += 1f;
                Assert.False(Scan(tracker, (lizard, 3f + i % 4))[0]);
            }
        }

        [Fact]
        public void FiresAgainAfterLeavingAndReturningOnceTheCooldownIsOver()
        {
            var clock = new Clock();
            var tracker = NewTracker(clock, cooldownSeconds: 10f);
            var lizard = new Creature("lizard");

            Assert.True(Scan(tracker, (lizard, 5f))[0]);
            Assert.False(Scan(tracker, (lizard, 40f))[0]); // walked away, well past the range

            clock.Now += 5f;
            Assert.False(Scan(tracker, (lizard, 5f))[0]); // back after 5s: still cooling down

            Assert.False(Scan(tracker, (lizard, 40f))[0]);
            clock.Now += 5f;
            Assert.True(Scan(tracker, (lizard, 5f))[0]); // 10s since it fired
        }

        [Fact]
        public void ACreatureHoveringAtTheEdgeDoesNotRepeat()
        {
            var clock = new Clock();
            var tracker = NewTracker(clock, cooldownSeconds: 0f); // even with no cooldown
            var lizard = new Creature("lizard");

            Assert.True(Scan(tracker, (lizard, 9.9f))[0]);

            // Jitters back and forth across the range line, but never gets a quarter further than it.
            int fires = 0;
            for (int i = 0; i < 40; i++)
            {
                clock.Now += 0.25f;
                float distance = i % 2 == 0 ? 10.5f : 9.5f;
                if (Scan(tracker, (lizard, distance))[0])
                {
                    fires++;
                }
            }

            Assert.Equal(0, fires);
        }

        [Fact]
        public void HysteresisEndsOnceItIsWellOutside()
        {
            var tracker = NewTracker(new Clock(), cooldownSeconds: 0f);
            var lizard = new Creature("lizard");

            Assert.True(Scan(tracker, (lizard, 9f))[0]);
            Assert.False(Scan(tracker, (lizard, 12.4f))[0]); // 10 * 1.25 = 12.5: still inside
            Assert.Equal(1, tracker.InsideCount);
            Assert.False(Scan(tracker, (lizard, 12.6f))[0]);
            Assert.Equal(0, tracker.InsideCount);
            Assert.True(Scan(tracker, (lizard, 9f))[0]);
        }

        [Fact]
        public void EachCreatureHasItsOwnCooldownAndEdge()
        {
            var tracker = NewTracker(new Clock());
            var a = new Creature("a");
            var b = new Creature("b");

            Assert.Equal(new[] { true, false }, Scan(tracker, (a, 5f), (b, 50f)));
            Assert.Equal(new[] { false, true }, Scan(tracker, (a, 5f), (b, 5f)));
        }

        [Fact]
        public void ACreatureNoLongerObservedIsForgottenSoItFiresAgainWhenItComesBack()
        {
            var clock = new Clock();
            var tracker = NewTracker(clock, cooldownSeconds: 1f);
            var lizard = new Creature("lizard");

            Assert.True(Scan(tracker, (lizard, 5f))[0]);
            Assert.Equal(1, tracker.InsideCount);

            Scan(tracker); // it left the room (or died): not seen in this scan
            Assert.Equal(0, tracker.InsideCount);

            clock.Now += 2f;
            Assert.True(Scan(tracker, (lizard, 5f))[0]);
        }

        [Fact]
        public void ACreatureThatLeavesAndReturnsBeforeTheCooldownIsOverStaysQuiet()
        {
            var clock = new Clock();
            var tracker = NewTracker(clock, cooldownSeconds: 10f);
            var lizard = new Creature("lizard");

            Assert.True(Scan(tracker, (lizard, 5f))[0]);
            Scan(tracker);
            clock.Now += 3f;
            Assert.False(Scan(tracker, (lizard, 5f))[0]);
        }

        [Fact]
        public void ACreatureBroughtInsideWithoutFiringDoesNotFireWhenPutDown()
        {
            var clock = new Clock();
            var tracker = NewTracker(clock, cooldownSeconds: 0f);
            var carried = new Creature("carried");

            // Held by the player: right next to them, but its arrival doesn't count.
            tracker.BeginScan();
            Assert.False(tracker.Observe(carried, 1f, 10f, allowFire: false));
            tracker.EndScan();

            // Put down beside the player: it was already "inside", so no event.
            Assert.False(Scan(tracker, (carried, 1f))[0]);
        }

        [Fact]
        public void ACarriedCreatureFiresNormallyOnceItIsCarriedAwayAndBack()
        {
            var tracker = NewTracker(new Clock(), cooldownSeconds: 0f);
            var carried = new Creature("carried");

            tracker.BeginScan();
            tracker.Observe(carried, 1f, 10f, allowFire: false);
            tracker.EndScan();

            Assert.False(Scan(tracker, (carried, 60f))[0]); // dropped far away: leaves the range
            Assert.True(Scan(tracker, (carried, 4f))[0]);   // and walks up to the player
        }

        [Fact]
        public void TwoPlayersShareOneCooldownSoASharedCreatureFiresOnce()
        {
            var clock = new Clock();
            var shared = new Cooldown(10f, () => clock.Now);
            var playerOne = new NearTracker(shared);
            var playerTwo = new NearTracker(shared);
            var lizard = new Creature("lizard");

            Assert.True(Scan(playerOne, (lizard, 5f))[0]);
            Assert.False(Scan(playerTwo, (lizard, 5f))[0]);
        }

        [Fact]
        public void ResetForgetsWhoWasInside()
        {
            var tracker = NewTracker(new Clock(), cooldownSeconds: 0f);
            var lizard = new Creature("lizard");

            Assert.True(Scan(tracker, (lizard, 5f))[0]);
            tracker.Reset();
            Assert.Equal(0, tracker.InsideCount);
            Assert.True(Scan(tracker, (lizard, 5f))[0]); // e.g. the player left and came back to the room
        }

        [Fact]
        public void ScansHappenEveryScanIntervalTicks()
        {
            var tracker = NewTracker(new Clock());
            int scans = 0;
            for (int tick = 1; tick <= NearTracker.ScanIntervalTicks * 5; tick++)
            {
                bool due = tracker.Tick();
                if (due)
                {
                    scans++;
                    Assert.Equal(0, tick % NearTracker.ScanIntervalTicks);
                }
            }

            Assert.Equal(5, scans);
        }

        [Fact]
        public void ManyCreaturesInAScanAreAllTrackedAndPruned()
        {
            var tracker = NewTracker(new Clock(), cooldownSeconds: 0f);
            Creature[] crowd = Enumerable.Range(0, 50).Select(i => new Creature("c" + i)).ToArray();

            tracker.BeginScan();
            int fired = crowd.Count(c => tracker.Observe(c, 5f, 10f, allowFire: true));
            tracker.EndScan();
            Assert.Equal(50, fired);
            Assert.Equal(50, tracker.InsideCount);

            Scan(tracker, (crowd[0], 5f)); // only one is left in the room
            Assert.Equal(1, tracker.InsideCount);
        }
    }
}
