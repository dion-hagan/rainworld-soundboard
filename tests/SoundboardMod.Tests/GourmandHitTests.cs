using Xunit;

namespace SoundboardMod.Tests
{
    public class GourmandHitTests
    {
        private sealed class Clock
        {
            public float Now;
        }

        private static GourmandHitTracker NewTracker(Clock clock)
        {
            return new GourmandHitTracker(() => clock.Now);
        }

        [Fact]
        public void RollingTakesPriorityEvenIfTheAnimationLooksLikeASlide()
        {
            Assert.Equal(GourmandHitKind.Roll, GourmandHitTracker.Classify(rolling: true, sliding: false));
            Assert.Equal(GourmandHitKind.Roll, GourmandHitTracker.Classify(rolling: true, sliding: true));
        }

        [Fact]
        public void SlidingIsASlideAndAnythingElseIsADrop()
        {
            Assert.Equal(GourmandHitKind.Slide, GourmandHitTracker.Classify(rolling: false, sliding: true));
            Assert.Equal(GourmandHitKind.Drop, GourmandHitTracker.Classify(rolling: false, sliding: false));
        }

        [Fact]
        public void EachKindHasItsOwnEventName()
        {
            Assert.Equal("GourmandSlideHit", GourmandHitTracker.EventFor(GourmandHitKind.Slide));
            Assert.Equal("GourmandDropHit", GourmandHitTracker.EventFor(GourmandHitKind.Drop));
            Assert.Equal("GourmandRollHit", GourmandHitTracker.EventFor(GourmandHitKind.Roll));
        }

        [Fact]
        public void EveryEventTheTrackerCanReturnIsInTheCatalog()
        {
            var catalog = new EventCatalog(new string[0]);
            foreach (GourmandHitKind kind in new[] { GourmandHitKind.Roll, GourmandHitKind.Slide, GourmandHitKind.Drop })
            {
                string name = GourmandHitTracker.EventFor(kind);
                Assert.Equal(name, catalog.Resolve(name));
                Assert.Equal(EventCatalog.SectionGourmand, Assert.Single(catalog.All, e => e.Name == name).Section);
            }
        }

        [Fact]
        public void FirstHitOnAVictimFires()
        {
            var clock = new Clock();
            GourmandHitTracker tracker = NewTracker(clock);

            Assert.Equal("GourmandSlideHit", tracker.TryHit(new object(), rolling: false, sliding: true));
        }

        [Fact]
        public void RepeatHitsOnTheSameVictimWithinTheWindowAreIgnored()
        {
            var clock = new Clock();
            GourmandHitTracker tracker = NewTracker(clock);
            object lizard = new object();

            Assert.Equal("GourmandDropHit", tracker.TryHit(lizard, rolling: false, sliding: false));

            // Same impact reported again by another chunk pair, in the same tick or a few later.
            Assert.Null(tracker.TryHit(lizard, rolling: false, sliding: false));
            clock.Now = GourmandHitTracker.DebounceSeconds - 0.01f;
            Assert.Null(tracker.TryHit(lizard, rolling: false, sliding: false));
        }

        [Fact]
        public void TheDebounceIsPerVictim()
        {
            var clock = new Clock();
            GourmandHitTracker tracker = NewTracker(clock);
            object first = new object();
            object second = new object();

            Assert.NotNull(tracker.TryHit(first, rolling: false, sliding: true));
            Assert.NotNull(tracker.TryHit(second, rolling: false, sliding: true));
        }

        [Fact]
        public void TheSameVictimCanBeHitAgainAfterTheWindow()
        {
            var clock = new Clock();
            GourmandHitTracker tracker = NewTracker(clock);
            object lizard = new object();

            Assert.NotNull(tracker.TryHit(lizard, rolling: false, sliding: true));
            clock.Now = GourmandHitTracker.DebounceSeconds + 0.01f;
            Assert.Equal("GourmandRollHit", tracker.TryHit(lizard, rolling: true, sliding: false));
        }

        [Fact]
        public void ADifferentKindOfHitOnTheSameVictimIsStillOneImpact()
        {
            var clock = new Clock();
            GourmandHitTracker tracker = NewTracker(clock);
            object lizard = new object();

            Assert.Equal("GourmandSlideHit", tracker.TryHit(lizard, rolling: false, sliding: true));
            Assert.Null(tracker.TryHit(lizard, rolling: false, sliding: false));
        }

        [Fact]
        public void NoVictimNoEvent()
        {
            var clock = new Clock();
            Assert.Null(NewTracker(clock).TryHit(null, rolling: false, sliding: true));
        }
    }
}
