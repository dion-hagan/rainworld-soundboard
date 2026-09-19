using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SoundboardMod.Tests
{
    /// <summary>An entry's "cooldown:" - it can't play again for N seconds after it plays.</summary>
    public class EntryCooldownTests
    {
        private static readonly EventCatalog Catalog = new EventCatalog(new string[0]);

        private static SoundboardConfig Parse(string yaml) => SoundboardConfigParser.Parse(yaml, Catalog);

        private static void TickSeconds(EntryCooldowns cooldowns, float seconds)
        {
            int ticks = (int)System.Math.Round(seconds * DelayQueue.TicksPerSecond);
            for (int i = 0; i < ticks; i++)
            {
                cooldowns.Tick();
            }
        }

        // --- the tracker ------------------------------------------------------------

        [Fact]
        public void AnEntryThatNeverPlayedIsNeverCoolingDown()
        {
            Assert.False(new EntryCooldowns().IsCoolingDown("a"));
        }

        [Fact]
        public void AnEntryCoolsDownForExactlyItsCooldown()
        {
            var cooldowns = new EntryCooldowns();
            cooldowns.Start("a", 2f);

            Assert.True(cooldowns.IsCoolingDown("a"));
            TickSeconds(cooldowns, 1.9f);
            Assert.True(cooldowns.IsCoolingDown("a"));
            TickSeconds(cooldowns, 0.1f);
            Assert.False(cooldowns.IsCoolingDown("a"));
        }

        [Fact]
        public void ACooldownOfZeroDoesNothingAndEntriesAreIndependent()
        {
            var cooldowns = new EntryCooldowns();
            cooldowns.Start("a", 0f);
            cooldowns.Start("b", 1f);

            Assert.False(cooldowns.IsCoolingDown("a"));
            Assert.True(cooldowns.IsCoolingDown("b"));
            Assert.False(cooldowns.IsCoolingDown("c"));
        }

        [Fact]
        public void StartingAgainRestartsTheCooldownAndClearForgetsThem()
        {
            var cooldowns = new EntryCooldowns();
            cooldowns.Start("a", 1f);
            TickSeconds(cooldowns, 1f);
            Assert.False(cooldowns.IsCoolingDown("a"));

            cooldowns.Start("a", 1f);
            Assert.True(cooldowns.IsCoolingDown("a"));

            cooldowns.Clear();
            Assert.False(cooldowns.IsCoolingDown("a"));
        }

        [Fact]
        public void NothingCountsDownUntilTheGameTicks()
        {
            var cooldowns = new EntryCooldowns();
            cooldowns.Start("a", 1f);

            // No Tick() calls - a paused game, or the menu - so the cooldown doesn't move.
            Assert.True(cooldowns.IsCoolingDown("a"));
            Assert.Equal(0, cooldowns.Now);
        }

        // --- with the rotation ---------------------------------------------------------

        /// <summary>What SoundboardRuntime.Fire does, on plain keys.</summary>
        private static string Fire(List<string> order, Dictionary<string, float> cooldownSeconds, EntryCooldowns cooldowns, ref string last)
        {
            string next = SoundRotation.NextKey(order, id => !cooldowns.IsCoolingDown(id), last);
            if (next != null)
            {
                last = next;
                cooldowns.Start(next, cooldownSeconds[next]);
            }

            return next;
        }

        [Fact]
        public void AnEventWithOneCooldownEntryStaysQuietUntilItIsReady()
        {
            var cooldowns = new EntryCooldowns();
            var order = new List<string> { "boom" };
            var seconds = new Dictionary<string, float> { ["boom"] = 5f };
            string last = null;

            Assert.Equal("boom", Fire(order, seconds, cooldowns, ref last));
            Assert.Null(Fire(order, seconds, cooldowns, ref last));

            TickSeconds(cooldowns, 4.9f);
            Assert.Null(Fire(order, seconds, cooldowns, ref last));

            TickSeconds(cooldowns, 0.1f);
            Assert.Equal("boom", Fire(order, seconds, cooldowns, ref last));
        }

        [Fact]
        public void TheRotationSkipsACoolingEntryAndComesBackToItAfterwards()
        {
            var cooldowns = new EntryCooldowns();
            var order = new List<string> { "a", "b", "c" };
            var seconds = new Dictionary<string, float> { ["a"] = 0f, ["b"] = 10f, ["c"] = 0f };
            string last = null;

            var played = new List<string>();
            for (int i = 0; i < 6; i++)
            {
                played.Add(Fire(order, seconds, cooldowns, ref last));
                TickSeconds(cooldowns, 1f);
            }

            // b plays once (turn 2), then is cooling for the next 10 seconds, so the rotation is just a, c.
            Assert.Equal(new[] { "a", "b", "c", "a", "c", "a" }, played.ToArray());

            TickSeconds(cooldowns, 10f);
            Assert.Equal("b", Fire(order, seconds, cooldowns, ref last)); // ready again; it was next in line after "c"
        }

        [Fact]
        public void WhenEveryEntryIsCoolingNothingPlays()
        {
            var cooldowns = new EntryCooldowns();
            var order = new List<string> { "a", "b" };
            var seconds = new Dictionary<string, float> { ["a"] = 30f, ["b"] = 30f };
            string last = null;

            Assert.Equal("a", Fire(order, seconds, cooldowns, ref last));
            Assert.Equal("b", Fire(order, seconds, cooldowns, ref last));
            Assert.Null(Fire(order, seconds, cooldowns, ref last));
        }

        // --- reading it from the file --------------------------------------------------

        [Fact]
        public void CooldownIsReadFromAnEntryAndDefaultsToNone()
        {
            SoundboardConfig config = Parse(
                "events:\n  PlayerDeath:\n" +
                "    - a.wav\n" +
                "    - file: b.wav\n" +
                "    - file: c.wav\n      cooldown: 12.5\n" +
                "    - { file: d.wav, cooldown: 3 }\n");

            Assert.Empty(config.Issues);
            Assert.Equal(new[] { 0f, 0f, 12.5f, 3f }, config.Events[0].Choices.Select(c => c.Cooldown).ToArray());
        }

        [Fact]
        public void ATogetherGroupHasOneCooldownForTheWholeGroup()
        {
            SoundboardConfig config = Parse(
                "events:\n  PlayerDeath:\n" +
                "    - together:\n        - one.wav\n        - two.wav\n      cooldown: 20\n");

            Assert.Empty(config.Issues);
            SoundChoice group = config.Events[0].Choices.Single();
            Assert.Equal(20f, group.Cooldown);
            Assert.Equal(2, group.Sounds.Count);
        }

        [Fact]
        public void ACooldownOnAGroupMemberIsFlaggedNotSilentlyIgnored()
        {
            SoundboardConfig config = Parse(
                "events:\n  PlayerDeath:\n" +
                "    - together:\n        - file: one.wav\n          cooldown: 5\n        - two.wav\n");

            Assert.Contains("cooldown", config.Issues.Single().Message);
            Assert.Equal(0f, config.Events[0].Choices.Single().Cooldown);
        }

        [Theory]
        [InlineData("cooldown: -1", 0f, "between 0 and 3600")]
        [InlineData("cooldown: 99999", 3600f, "between 0 and 3600")]
        [InlineData("cooldown: soon", 0f, "isn't a number")]
        [InlineData("cooldown: 1,5", 0f, "comma")]
        public void ABadCooldownWarnsAndFallsBackToASafeValue(string line, float expected, string message)
        {
            SoundboardConfig config = Parse("events:\n  PlayerDeath:\n    - file: a.wav\n      " + line + "\n");

            Assert.Contains(message, config.Issues.Single().Message);
            Assert.Equal(IssueSeverity.Warning, config.Issues.Single().Severity);
            Assert.Equal(expected, config.Events[0].Choices.Single().Cooldown);
        }

        [Fact]
        public void CooldownDoesNotDisturbTheOtherKeys()
        {
            SoundboardConfig config = Parse("events:\n  PlayerDeath:\n    - file: a.wav\n      volume: 0.5\n      delay: 1\n      name: x\n      enabled: true\n      cooldown: 2\n");
            Assert.Empty(config.Issues);
        }

        // --- adding one from the options screen -------------------------------------------

        private static string Add(string yaml, NewSound sound)
        {
            YamlEditor.Result result = SoundAdder.Add(yaml, sound, Catalog);
            Assert.True(result.Ok, result.Error);
            return result.Text;
        }

        [Fact]
        public void ASingleSoundWithACooldownIsWrittenAfterItsOtherOptions()
        {
            NewSound sound = NewSound.Single("PlayerDeath", "new.wav", 0.5f, 1.5f);
            sound.Cooldown = 30f;

            string edited = Add("events:\n  PlayerDeath:\n    - a.wav\n", sound);

            Assert.Equal("events:\n  PlayerDeath:\n    - a.wav\n    - file: new.wav\n      volume: 0.5\n      delay: 1.5\n      cooldown: 30\n", edited);
            Assert.Equal(30f, SoundboardConfigParser.Parse(edited, Catalog).Events[0].Choices.Last().Cooldown);
        }

        [Fact]
        public void ACooldownAloneStillNeedsTheLongForm()
        {
            NewSound sound = NewSound.Single("PlayerDeath", "new.wav");
            sound.Cooldown = 2.5f;

            Assert.Equal("events:\n  PlayerDeath:\n    - file: new.wav\n      cooldown: 2.5\n", Add("events:\n  PlayerDeath:\n", sound));
        }

        [Fact]
        public void AGroupsCooldownSitsBesideTogetherNotInsideAMember()
        {
            var sound = new NewSound { EventName = "PlayerDeath", Together = true, Cooldown = 45f };
            sound.Parts.Add(new NewSoundPart { File = "one.wav", Volume = 0.5f });
            sound.Parts.Add(new NewSoundPart { File = "two.wav" });

            string edited = Add("events:\n  PlayerDeath:\n    - a.wav\n", sound);

            Assert.Equal(
                "events:\n  PlayerDeath:\n    - a.wav\n    - together:\n        - file: one.wav\n          volume: 0.5\n        - two.wav\n      cooldown: 45\n",
                edited);
            SoundChoice group = SoundboardConfigParser.Parse(edited, Catalog).Events[0].Choices.Last();
            Assert.Equal(45f, group.Cooldown);
            Assert.Equal(2, group.Sounds.Count);
        }

        [Fact]
        public void CooldownsAreWrittenInInlineListsToo()
        {
            NewSound single = NewSound.Single("PlayerDeath", "n.wav");
            single.Cooldown = 3f;
            Assert.Equal("events:\n  PlayerDeath: [a.wav, { file: n.wav, cooldown: 3 }]\n", Add("events:\n  PlayerDeath: [a.wav]\n", single));

            var group = new NewSound { EventName = "PlayerDeath", Together = true, Cooldown = 3f };
            group.Parts.Add(new NewSoundPart { File = "one.wav" });
            group.Parts.Add(new NewSoundPart { File = "two.wav" });
            string edited = Add("events:\n  PlayerDeath: [a.wav]\n", group);
            Assert.Equal("events:\n  PlayerDeath: [a.wav, { together: [one.wav, two.wav], cooldown: 3 }]\n", edited);
            Assert.Equal(3f, SoundboardConfigParser.Parse(edited, Catalog).Events[0].Choices.Last().Cooldown);
        }

        [Theory]
        [InlineData(-1f)]
        [InlineData(3601f)]
        [InlineData(float.NaN)]
        public void AnOutOfRangeCooldownIsRefused(float cooldown)
        {
            NewSound sound = NewSound.Single("PlayerDeath", "n.wav");
            sound.Cooldown = cooldown;
            YamlEditor.Result result = SoundAdder.Add("events:\n  PlayerDeath:\n    - a.wav\n", sound, Catalog);

            Assert.False(result.Ok);
            Assert.Contains("cooldown", result.Error);
        }
    }
}
