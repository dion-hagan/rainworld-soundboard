using Xunit;

namespace SoundboardMod.Tests
{
    public class RainWarningTests
    {
        private const int Minute = 60 * 40;

        [Fact]
        public void WarningIsExactlyOneMinuteOfGameTicks()
        {
            Assert.Equal(2400, RainWarning.WarningTicks);
        }

        [Fact]
        public void FiresOnceWhenTheTimerFirstReachesOneMinute()
        {
            var warning = new RainWarning();

            Assert.False(warning.Update(Minute + 2));
            Assert.False(warning.Update(Minute + 1));
            Assert.True(warning.Update(Minute));
            Assert.False(warning.Update(Minute - 1));
            Assert.False(warning.Update(1200));
            Assert.False(warning.Update(1));
        }

        [Fact]
        public void FiresWhenTheTimerJumpsPastTheExactTick()
        {
            // The Rivulet and arena rushes to rain add several ticks per update.
            var warning = new RainWarning();

            Assert.False(warning.Update(Minute + 2));
            Assert.True(warning.Update(Minute - 1));
            Assert.False(warning.Update(Minute - 4));
        }

        [Fact]
        public void DoesNotFireOnceTheRainHasArrived()
        {
            var warning = new RainWarning();

            Assert.False(warning.Update(0));
            Assert.False(warning.Update(-5));
        }

        [Fact]
        public void FiresAgainForTheNextCycle()
        {
            var warning = new RainWarning();

            Assert.True(warning.Update(Minute));
            Assert.False(warning.Update(100));

            Assert.False(warning.Update(Minute * 12)); // a new cycle starts with plenty of time left
            Assert.True(warning.Update(Minute));
        }

        [Fact]
        public void FiresStraightAwayWhenFirstShownANumberInsideTheLastMinute()
        {
            // E.g. a cycle that is shorter than a minute to begin with.
            var warning = new RainWarning();

            Assert.True(warning.Update(Minute - 600));
            Assert.False(warning.Update(Minute - 601));
        }
    }
}
