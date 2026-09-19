using System.Linq;
using Xunit;

namespace SoundboardMod.Tests
{
    public class ConfigParserTests
    {
        private static readonly EventCatalog Catalog = new EventCatalog(new[] { "RedLizard", "GreenLizard", "Scavenger", "Spider", "CyanLizard" });

        private static SoundboardConfig Parse(string yaml) => SoundboardConfigParser.Parse(yaml, Catalog);

        private static EventBinding Event(SoundboardConfig config, string name) =>
            config.Events.Single(e => e.EventName == name);

        [Fact]
        public void ParsesTheSimplestPossibleConfig()
        {
            SoundboardConfig config = Parse("events:\n  PlayerDeath:\n    - boom.wav\n");

            Assert.Empty(config.Issues);
            SoundChoice choice = Event(config, "PlayerDeath").Choices.Single();
            SoundRef sound = choice.Sounds.Single();
            Assert.Equal("boom.wav", sound.File);
            Assert.Equal(1f, sound.Volume);
            Assert.Equal(0f, sound.Delay);
            Assert.Equal("Boom", choice.Label);
            Assert.True(choice.DefaultEnabled);
        }

        [Fact]
        public void ParsesVolumeDelayNameDescriptionAndEnabled()
        {
            SoundboardConfig config = Parse(
                "events:\n" +
                "  PlayerJump:\n" +
                "    - file: hop.wav\n" +
                "      volume: 0.4\n" +
                "      delay: 1.5\n" +
                "      name: Bunny hop\n" +
                "      description: Plays on every jump\n" +
                "      enabled: no\n");

            Assert.Empty(config.Issues);
            SoundChoice choice = Event(config, "PlayerJump").Choices.Single();
            Assert.Equal(0.4f, choice.Sounds[0].Volume);
            Assert.Equal(1.5f, choice.Sounds[0].Delay);
            Assert.Equal("Bunny hop", choice.Label);
            Assert.Equal("Plays on every jump", choice.Description);
            Assert.False(choice.DefaultEnabled);
        }

        [Fact]
        public void InlineFormWorks()
        {
            SoundboardConfig config = Parse("events:\n  PlayerDeath:\n    - { file: a.wav, volume: 0.25, delay: 2 }\n");
            Assert.Empty(config.Issues);
            SoundRef sound = Event(config, "PlayerDeath").Choices.Single().Sounds.Single();
            Assert.Equal(0.25f, sound.Volume);
            Assert.Equal(2f, sound.Delay);
        }

        [Fact]
        public void SeveralEntriesBecomeARotationInOrder()
        {
            SoundboardConfig config = Parse("events:\n  PlayerDeath:\n    - a.wav\n    - b.wav\n    - c.wav\n");
            Assert.Equal(new[] { "a.wav", "b.wav", "c.wav" }, Event(config, "PlayerDeath").Choices.Select(c => c.Sounds[0].File));
        }

        [Fact]
        public void TogetherGroupsPlayAsOneChoice()
        {
            SoundboardConfig config = Parse(
                "events:\n" +
                "  PlayerSpottedByScavenger:\n" +
                "    - solo.wav\n" +
                "    - together:\n" +
                "        - one.wav\n" +
                "        - file: two.wav\n" +
                "          volume: 0.5\n");

            Assert.Empty(config.Issues);
            EventBinding binding = Event(config, "PlayerSpottedByScavenger");
            Assert.Equal(2, binding.Choices.Count);
            SoundChoice group = binding.Choices[1];
            Assert.Equal(2, group.Sounds.Count);
            Assert.Equal("One + Two", group.Label);
            Assert.Equal(0.5f, group.Sounds[1].Volume);
        }

        [Fact]
        public void GroupVolumeMultipliesAndGroupDelayAdds()
        {
            SoundboardConfig config = Parse(
                "events:\n" +
                "  PlayerDeath:\n" +
                "    - together:\n" +
                "        - file: a.wav\n" +
                "          volume: 0.5\n" +
                "          delay: 1\n" +
                "        - b.wav\n" +
                "      volume: 0.5\n" +
                "      delay: 2\n" +
                "      name: Combo\n");

            Assert.Empty(config.Issues);
            SoundChoice group = Event(config, "PlayerDeath").Choices.Single();
            Assert.Equal("Combo", group.Label);
            Assert.Equal(0.25f, group.Sounds[0].Volume);
            Assert.Equal(3f, group.Sounds[0].Delay);
            Assert.Equal(0.5f, group.Sounds[1].Volume);
            Assert.Equal(2f, group.Sounds[1].Delay);
        }

        [Fact]
        public void EventNamesAreForgiving()
        {
            SoundboardConfig config = Parse("events:\n  player death:\n    - a.wav\n  red-lizard_death:\n    - b.wav\n");
            Assert.Empty(config.Issues);
            Assert.Equal("PlayerDeath", config.Events[0].EventName);
            Assert.Equal("RedLizardDeath", config.Events[1].EventName);
        }

        [Fact]
        public void EveryCreatureTypeGetsSpottedAndDeathEvents()
        {
            SoundboardConfig config = Parse(
                "events:\n" +
                "  PlayerSpottedByRedLizard:\n    - a.wav\n" +
                "  GreenLizardDeath:\n    - b.wav\n" +
                "  ScavengerDeath:\n    - c.wav\n");
            Assert.Empty(config.Issues);
            Assert.Equal(3, config.Events.Count);
        }

        [Fact]
        public void UnknownEventGetsASuggestionButIsKept()
        {
            SoundboardConfig config = Parse("events:\n  PlayerDeth:\n    - a.wav\n");
            ConfigIssue issue = config.Issues.Single();
            Assert.Equal(IssueSeverity.Warning, issue.Severity);
            Assert.Equal(2, issue.Line);
            Assert.Contains("Did you mean 'PlayerDeath'?", issue.Message);
            Assert.Single(config.Events); // kept: a modded creature's event may just not be registered yet
        }

        [Fact]
        public void SameEventWrittenTwiceIsMergedWithAWarning()
        {
            SoundboardConfig config = Parse("events:\n  PlayerDeath:\n    - a.wav\n  player death:\n    - b.wav\n");
            Assert.Single(config.Issues);
            Assert.Equal(new[] { "a.wav", "b.wav" }, Event(config, "PlayerDeath").Choices.Select(c => c.Sounds[0].File));
        }

        [Fact]
        public void UnknownOptionGetsASuggestion()
        {
            SoundboardConfig config = Parse("events:\n  PlayerDeath:\n    - file: a.wav\n      volum: 0.5\n");
            ConfigIssue issue = config.Issues.Single();
            Assert.Equal(4, issue.Line);
            Assert.Contains("Did you mean 'volume'?", issue.Message);
            Assert.Equal(1f, Event(config, "PlayerDeath").Choices[0].Sounds[0].Volume); // typo'd option is ignored
        }

        [Fact]
        public void VolumeAndDelayAreRangeChecked()
        {
            SoundboardConfig config = Parse("events:\n  PlayerDeath:\n    - file: a.wav\n      volume: 500\n      delay: -3\n");
            Assert.Equal(2, config.Issues.Count);
            SoundRef sound = Event(config, "PlayerDeath").Choices[0].Sounds[0];
            Assert.Equal(10f, sound.Volume);
            Assert.Equal(0f, sound.Delay);
        }

        [Fact]
        public void CommaDecimalsGetAHelpfulMessage()
        {
            SoundboardConfig config = Parse("events:\n  PlayerDeath:\n    - file: a.wav\n      volume: 0,5\n");
            Assert.Contains("dot", config.Issues.Single().Message);
        }

        [Fact]
        public void NonNumbersAreReported()
        {
            SoundboardConfig config = Parse("events:\n  PlayerDeath:\n    - file: a.wav\n      volume: loud\n");
            Assert.Contains("isn't a number", config.Issues.Single().Message);
        }

        [Fact]
        public void FileIsRequired()
        {
            SoundboardConfig config = Parse("events:\n  PlayerDeath:\n    - volume: 0.5\n");
            Assert.Equal(IssueSeverity.Error, config.Issues.Single().Severity);
            Assert.Contains("missing 'file:'", config.Issues.Single().Message);
            Assert.Empty(config.Events);
        }

        [Fact]
        public void OtherItemsSurviveABrokenOne()
        {
            SoundboardConfig config = Parse("events:\n  PlayerDeath:\n    - volume: 1\n    - good.wav\n");
            Assert.Single(Event(config, "PlayerDeath").Choices);
        }

        [Fact]
        public void PathsMustStayInsideTheSoundsFolder()
        {
            SoundboardConfig config = Parse("events:\n  PlayerDeath:\n    - ../evil.wav\n    - 'C:\\x\\y.wav'\n    - sub/ok.wav\n");
            Assert.Equal(2, config.Issues.Count);
            Assert.Equal("sub/ok.wav", Event(config, "PlayerDeath").Choices.Single().Sounds[0].File);
        }

        [Fact]
        public void FileCannotBeMixedWithTogether()
        {
            SoundboardConfig config = Parse("events:\n  PlayerDeath:\n    - file: a.wav\n      together:\n        - b.wav\n");
            Assert.Contains("next to 'together'", config.Issues.Single().Message);
        }

        [Fact]
        public void ScalarInsteadOfListIsAccepted()
        {
            SoundboardConfig config = Parse("events:\n  PlayerDeath: boom.wav\n");
            Assert.Empty(config.Issues);
            Assert.Equal("boom.wav", Event(config, "PlayerDeath").Choices.Single().Sounds[0].File);
        }

        [Fact]
        public void EventWithNoSoundsWarns()
        {
            SoundboardConfig config = Parse("events:\n  PlayerDeath:\n  PlayerJump:\n    - a.wav\n");
            Assert.Contains("no sounds", config.Issues.Single().Message);
            Assert.Single(config.Events);
        }

        // --- ids ------------------------------------------------------------

        [Fact]
        public void ChoiceIdsAreValidAndUnique()
        {
            SoundboardConfig config = Parse(
                "events:\n" +
                "  PlayerDeath:\n" +
                "    - a b.wav\n" +
                "    - a b.wav\n" +
                "    - together:\n      - x.wav\n      - y.wav\n" +
                "  PlayerJump:\n" +
                "    - a b.wav\n");

            string[] ids = config.Events.SelectMany(e => e.Choices).Select(c => c.Id).ToArray();
            Assert.Equal(ids.Length, ids.Distinct().Count());
            Assert.All(ids, id => Assert.All(id, ch => Assert.True(char.IsLetterOrDigit(ch) || ch == '_')));
            Assert.Equal("PlayerDeath_a_b", ids[0]);
            Assert.Equal("PlayerDeath_a_b_2", ids[1]);
        }

        [Fact]
        public void ReorderingDoesNotChangeIds()
        {
            SoundboardConfig first = Parse("events:\n  PlayerDeath:\n    - a.wav\n    - b.wav\n");
            SoundboardConfig second = Parse("events:\n  PlayerDeath:\n    - b.wav\n    - a.wav\n");
            Assert.Equal(
                first.Events[0].Choices.ToDictionary(c => c.Sounds[0].File, c => c.Id),
                second.Events[0].Choices.ToDictionary(c => c.Sounds[0].File, c => c.Id));
        }

        // --- settings ---------------------------------------------------------

        [Fact]
        public void SettingsDefaultsAreSane()
        {
            SoundboardSettings s = Parse(string.Empty).Settings;
            Assert.Equal(30f, s.HardLandingSpeed);
            Assert.Equal(40f, s.TerminalVelocity);
            Assert.Equal(2f, s.PlayerJumpCooldown);
            Assert.Equal(10f, s.ArtificerPyroJumpCooldown);
            Assert.Equal(10f, s.SpottedCooldown);
            Assert.False(s.Debug);
        }

        [Fact]
        public void ParsesSettingsInAnyNamingStyle()
        {
            SoundboardConfig config = Parse(
                "settings:\n" +
                "  hard-landing-speed: 25\n" +
                "  Terminal_Velocity: 55.5\n" +
                "  playerJumpCooldown: 0.5\n" +
                "  debug: yes\n");

            Assert.Empty(config.Issues);
            Assert.Equal(25f, config.Settings.HardLandingSpeed);
            Assert.Equal(55.5f, config.Settings.TerminalVelocity);
            Assert.Equal(0.5f, config.Settings.PlayerJumpCooldown);
            Assert.True(config.Settings.Debug);
        }

        [Fact]
        public void UnknownSettingSuggestsTheRightOne()
        {
            SoundboardConfig config = Parse("settings:\n  terminal-velocty: 50\n");
            Assert.Contains("Did you mean 'terminal-velocity'?", config.Issues.Single().Message);
        }

        [Fact]
        public void OutOfRangeSettingIsClamped()
        {
            SoundboardConfig config = Parse("settings:\n  hard-landing-speed: 0\n");
            Assert.Single(config.Issues);
            Assert.Equal(1f, config.Settings.HardLandingSpeed);
        }

        // --- whole-file problems ------------------------------------------------

        [Fact]
        public void SyntaxErrorFailsTheWholeFileWithTheLineNumber()
        {
            SoundboardConfig config = Parse("events:\n  PlayerDeath:\n\t- a.wav\n");
            Assert.True(config.Failed);
            Assert.True(config.HasErrors);
            Assert.Equal(3, config.Issues.Single().Line);
        }

        [Fact]
        public void EventNameOutsideEventsSectionGetsAPointer()
        {
            SoundboardConfig config = Parse("PlayerDeath:\n  - a.wav\n");
            Assert.Contains("inside the 'events:' section", config.Issues.Single().Message);
        }

        [Fact]
        public void MisspelledSectionGetsASuggestion()
        {
            SoundboardConfig config = Parse("evnts:\n  PlayerDeath:\n    - a.wav\n");
            Assert.Contains("Did you mean 'events'?", config.Issues.Single().Message);
        }

        [Fact]
        public void ListAtTopLevelFails()
        {
            Assert.True(Parse("- a.wav\n- b.wav\n").Failed);
        }
    }
}
