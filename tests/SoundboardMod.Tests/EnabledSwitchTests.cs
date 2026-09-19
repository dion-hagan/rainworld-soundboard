using System;
using System.IO;
using System.Linq;
using Xunit;

namespace SoundboardMod.Tests
{
    public class EnabledSwitchTests
    {
        private static readonly EventCatalog Catalog = new EventCatalog(new[] { "Scavenger" });

        private static SoundboardConfig Parse(string yaml) => SoundboardConfigParser.Parse(yaml, Catalog);

        private static SoundChoice Choice(SoundboardConfig config, string id) =>
            config.Events.SelectMany(e => e.Choices).Single(c => c.Id == id);

        /// <summary>What the game does when a checkbox is ticked: re-read the file, find the entry, edit its lines.</summary>
        private static string Toggle(string yaml, string id, bool on)
        {
            SoundboardConfig fresh = Parse(yaml);
            Assert.False(fresh.Failed);
            YamlEditor.Result result = YamlEditor.SetEnabled(yaml, Choice(fresh, id).Source, on);
            Assert.True(result.Ok, result.Error);
            return result.Text;
        }

        private static bool[] States(string yaml) =>
            Parse(yaml).Events.SelectMany(e => e.Choices).Select(c => c.Enabled).ToArray();

        // --- reading: enabled / disabled ---------------------------------------

        [Theory]
        [InlineData("enabled: false", false)]
        [InlineData("enabled: no", false)]
        [InlineData("enabled: true", true)]
        [InlineData("disabled: true", false)]
        [InlineData("disabled: yes", false)]
        [InlineData("disabled: false", true)]
        public void EnabledAndDisabledBothWork(string line, bool expected)
        {
            SoundboardConfig config = Parse("events:\n  PlayerDeath:\n    - file: a.wav\n      " + line + "\n");
            Assert.Empty(config.Issues);
            Assert.Equal(expected, config.Events[0].Choices[0].Enabled);
        }

        [Fact]
        public void OnByDefault()
        {
            Assert.True(Parse("events:\n  PlayerDeath:\n    - a.wav\n").Events[0].Choices[0].Enabled);
        }

        [Fact]
        public void DisabledWorksInFlowFormAndOnGroups()
        {
            SoundboardConfig config = Parse(
                "events:\n  PlayerDeath:\n" +
                "    - { file: a.wav, disabled: true }\n" +
                "    - together:\n        - b.wav\n        - c.wav\n      disabled: true\n");
            Assert.Empty(config.Issues);
            Assert.All(config.Events[0].Choices, c => Assert.False(c.Enabled));
        }

        [Fact]
        public void BothKeysWarnAndEnabledWins()
        {
            SoundboardConfig config = Parse("events:\n  PlayerDeath:\n    - file: a.wav\n      enabled: true\n      disabled: true\n");
            Assert.Contains("both 'enabled' and 'disabled'", config.Issues.Single().Message);
            Assert.True(config.Events[0].Choices[0].Enabled);
        }

        [Fact]
        public void NonBooleanSwitchWarnsAndKeepsTheEntryOn()
        {
            SoundboardConfig config = Parse("events:\n  PlayerDeath:\n    - file: a.wav\n      disabled: maybe\n");
            Assert.Contains("true or false", config.Issues.Single().Message);
            Assert.True(config.Events[0].Choices[0].Enabled);
        }

        // --- writing ------------------------------------------------------------

        [Fact]
        public void SwitchingOffAddsAnEnabledLineInLineWithTheOtherKeys()
        {
            const string yaml =
                "events:\n" +
                "  PlayerDeath:\n" +
                "    - file: a.wav\n" +
                "      volume: 0.2\n" +
                "    - file: b.wav\n" +
                "      volume: 0.3\n";

            string edited = Toggle(yaml, "PlayerDeath_a", false);

            Assert.Equal(
                "events:\n  PlayerDeath:\n    - file: a.wav\n      volume: 0.2\n      enabled: false\n    - file: b.wav\n      volume: 0.3\n",
                edited);
            Assert.Equal(new[] { false, true }, States(edited));
        }

        [Fact]
        public void SwitchingOnAnEntryWithNoSwitchChangesNothing()
        {
            const string yaml = "events:\n  PlayerDeath:\n    - a.wav\n";
            Assert.Equal(yaml, Toggle(yaml, "PlayerDeath_a", true));
        }

        [Fact]
        public void ExistingSwitchIsFlippedInPlaceKeepingItsComment()
        {
            const string yaml =
                "events:\n  PlayerDeath:\n    - file: a.wav\n      enabled: false   # muted for now\n      volume: 0.2\n";

            string edited = Toggle(yaml, "PlayerDeath_a", true);

            Assert.Equal(
                "events:\n  PlayerDeath:\n    - file: a.wav\n      enabled: true   # muted for now\n      volume: 0.2\n",
                edited);
            Assert.Equal(new[] { true }, States(edited));
        }

        [Fact]
        public void ADisabledKeyIsKeptAndFlippedTheRightWay()
        {
            const string yaml = "events:\n  PlayerDeath:\n    - file: a.wav\n      disabled: true\n";

            string enabledAgain = Toggle(yaml, "PlayerDeath_a", true);
            Assert.Contains("disabled: false", enabledAgain);
            Assert.DoesNotContain("enabled", enabledAgain);
            Assert.Equal(new[] { true }, States(enabledAgain));

            string off = Toggle(enabledAgain, "PlayerDeath_a", false);
            Assert.Contains("disabled: true", off);
            Assert.Equal(new[] { false }, States(off));
        }

        [Fact]
        public void TheSwitchMayBeTheFirstKeyOnTheDashLine()
        {
            const string yaml = "events:\n  PlayerDeath:\n    - enabled: false\n      file: a.wav\n";
            string edited = Toggle(yaml, "PlayerDeath_a", true);
            Assert.Equal("events:\n  PlayerDeath:\n    - enabled: true\n      file: a.wav\n", edited);
        }

        [Fact]
        public void AListItemThatIsJustAFileNameIsExpanded()
        {
            const string yaml = "events:\n  PlayerDeath:\n    - a.wav   # the classic\n    - b.wav\n";

            string edited = Toggle(yaml, "PlayerDeath_a", false);

            Assert.Equal(
                "events:\n  PlayerDeath:\n    - file: a.wav   # the classic\n      enabled: false\n    - b.wav\n",
                edited);
            Assert.Equal(new[] { false, true }, States(edited));
            Assert.Equal("a.wav", Parse(edited).Events[0].Choices[0].Sounds[0].File);
        }

        [Fact]
        public void QuotedFileNamesSurviveExpansion()
        {
            const string yaml = "events:\n  PlayerDeath:\n    - \"my sound.wav\"\n";
            string edited = Toggle(yaml, "PlayerDeath_my_sound", false);
            Assert.Equal("my sound.wav", Parse(edited).Events[0].Choices[0].Sounds[0].File);
            Assert.False(States(edited)[0]);
        }

        [Fact]
        public void InlineEntriesGetTheSwitchInsideTheBraces()
        {
            const string yaml = "events:\n  PlayerDeath:\n    - { file: a.wav, volume: 0.5 }   # loud\n    - b.wav\n";

            string off = Toggle(yaml, "PlayerDeath_a", false);
            Assert.Equal("events:\n  PlayerDeath:\n    - { file: a.wav, volume: 0.5, enabled: false }   # loud\n    - b.wav\n", off);
            Assert.Equal(new[] { false, true }, States(off));

            string on = Toggle(off, "PlayerDeath_a", true);
            Assert.Contains("enabled: true }", on);
            Assert.Equal(new[] { true, true }, States(on));
        }

        [Fact]
        public void GroupsGetTheSwitchAfterTheirLastNestedLine()
        {
            const string yaml =
                "events:\n" +
                "  PlayerSpottedByScavenger:\n" +
                "    - together:\n" +
                "        - one.wav\n" +
                "        - file: two.wav\n" +
                "          volume: 0.2\n" +
                "      name: Pair\n" +
                "    - solo.wav\n";

            string edited = Toggle(yaml, "PlayerSpottedByScavenger_Pair", false);

            Assert.Contains("      name: Pair\n      enabled: false\n    - solo.wav", edited);
            Assert.Equal(new[] { false, true }, States(edited));
            Assert.Equal(2, Parse(edited).Events[0].Choices[0].Sounds.Count);
        }

        [Fact]
        public void AGroupWhoseLastLineIsNested()
        {
            // The group ends inside its nested list, so the new key must land at the group's own indent, not the list's.
            const string yaml =
                "events:\n  PlayerDeath:\n    - together:\n        - one.wav\n        - two.wav\n    - solo.wav\n";

            string edited = Toggle(yaml, "PlayerDeath_one_two", false);

            Assert.Equal(
                "events:\n  PlayerDeath:\n    - together:\n        - one.wav\n        - two.wav\n      enabled: false\n    - solo.wav\n",
                edited);
            Assert.Equal(new[] { false, true }, States(edited));
        }

        [Fact]
        public void WindowsLineEndingsAreKept()
        {
            const string yaml = "events:\r\n  PlayerDeath:\r\n    - file: a.wav\r\n      volume: 1\r\n    - b.wav\r\n";

            string edited = Toggle(yaml, "PlayerDeath_a", false);

            Assert.Equal("events:\r\n  PlayerDeath:\r\n    - file: a.wav\r\n      volume: 1\r\n      enabled: false\r\n    - b.wav\r\n", edited);
            Assert.DoesNotContain("\n\n", edited.Replace("\r\n", "|"));
            Assert.Equal(new[] { false, true }, States(edited));
        }

        [Fact]
        public void TheLastEntryInAFileWithNoTrailingNewline()
        {
            const string yaml = "events:\n  PlayerDeath:\n    - file: a.wav\n      volume: 1";
            string edited = Toggle(yaml, "PlayerDeath_a", false);
            Assert.Equal("events:\n  PlayerDeath:\n    - file: a.wav\n      volume: 1\n      enabled: false", edited);
        }

        [Fact]
        public void CommentsAndBlankLinesElsewhereAreUntouched()
        {
            const string yaml =
                "# my config\n" +
                "settings:\n  debug: true   # keep\n\n" +
                "events:\n\n" +
                "  # deaths\n" +
                "  PlayerDeath:\n" +
                "    - file: a.wav   # first\n" +
                "      volume: 0.2\n" +
                "\n" +
                "    # second one\n" +
                "    - file: b.wav\n";

            string edited = Toggle(yaml, "PlayerDeath_a", false);

            Assert.Equal(yaml.Replace("      volume: 0.2\n", "      volume: 0.2\n      enabled: false\n"), edited);
        }

        [Fact]
        public void SeveralEditsInARowStayConsistent()
        {
            string yaml =
                "events:\n  PlayerDeath:\n    - a.wav\n    - file: b.wav\n    - { file: c.wav }\n" +
                "  PlayerJump:\n    - together:\n        - d.wav\n        - e.wav\n    - f.wav\n";

            string[] ids = Parse(yaml).Events.SelectMany(e => e.Choices).Select(c => c.Id).ToArray();
            Assert.Equal(5, ids.Length); // a, b, c, the d+e group, f

            // Switch everything off, one at a time, re-reading the file each time like the game does...
            foreach (string id in ids)
            {
                yaml = Toggle(yaml, id, false);
            }

            Assert.Equal(new[] { false, false, false, false, false }, States(yaml));
            Assert.Empty(Parse(yaml).Issues);
            Assert.Equal(ids, Parse(yaml).Events.SelectMany(e => e.Choices).Select(c => c.Id).ToArray());

            // ...then everything back on.
            foreach (string id in ids)
            {
                yaml = Toggle(yaml, id, true);
            }

            Assert.Equal(new[] { true, true, true, true, true }, States(yaml));
            Assert.Empty(Parse(yaml).Issues);
            Assert.Equal(6, Parse(yaml).SoundCount); // a b c d e f
        }

        [Fact]
        public void EditingOneEntryDoesNotChangeAnyOtherEntry()
        {
            string yaml =
                "events:\n  PlayerDeath:\n    - file: a.wav\n      volume: 0.2\n      delay: 1\n    - file: b.wav\n      volume: 0.7\n";

            SoundboardConfig before = Parse(yaml);
            SoundboardConfig after = Parse(Toggle(yaml, "PlayerDeath_a", false));

            SoundRef b1 = before.Events[0].Choices[1].Sounds[0];
            SoundRef b2 = after.Events[0].Choices[1].Sounds[0];
            Assert.Equal(b1.File, b2.File);
            Assert.Equal(b1.Volume, b2.Volume);
            Assert.True(after.Events[0].Choices[1].Enabled);
            Assert.Equal(0.2f, after.Events[0].Choices[0].Sounds[0].Volume);
            Assert.Equal(1f, after.Events[0].Choices[0].Sounds[0].Delay);
        }

        // --- things it must refuse rather than corrupt -----------------------------

        [Fact]
        public void MultiLineInlineEntriesAreRefusedWithAnExplanation()
        {
            const string yaml = "events:\n  PlayerDeath:\n    - { file: a.wav,\n        volume: 0.5 }\n";
            SoundboardConfig config = Parse(yaml);
            YamlEditor.Result result = YamlEditor.SetEnabled(yaml, config.Events[0].Choices[0].Source, false);
            Assert.False(result.Ok);
            Assert.Contains("several lines", result.Error);
            Assert.Equal(yaml, result.Text);
        }

        [Fact]
        public void ABareValueUnderTheEventNameIsRefused()
        {
            const string yaml = "events:\n  PlayerDeath: a.wav\n";
            SoundboardConfig config = Parse(yaml);
            YamlEditor.Result result = YamlEditor.SetEnabled(yaml, config.Events[0].Choices[0].Source, false);
            Assert.False(result.Ok);
            Assert.Equal(yaml, result.Text);
        }

        [Fact]
        public void StaleLineNumbersAreDetectedNotApplied()
        {
            const string yaml = "events:\n  PlayerDeath:\n    - a.wav\n";
            var stale = new YamlItemSpan { Kind = YamlKind.Scalar, StartLine = 40, EndLine = 40 };
            YamlEditor.Result result = YamlEditor.SetEnabled(yaml, stale, false);
            Assert.False(result.Ok);
            Assert.Equal(yaml, result.Text);
        }

        // --- the real thing --------------------------------------------------------

        [Fact]
        public void TheShippedConfigCanBeToggledEntryByEntry()
        {
            string dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "SoundboardMod.sln")))
            {
                dir = Path.GetDirectoryName(dir);
            }

            string yaml = File.ReadAllText(Path.Combine(dir, "mod", "soundboard.yaml"));
            var ids = Parse(yaml).Events.SelectMany(e => e.Choices).Select(c => c.Id).ToArray();
            Assert.True(ids.Length > 50);

            string original = yaml;
            foreach (string id in ids)
            {
                yaml = Toggle(yaml, id, false);
            }

            SoundboardConfig off = Parse(yaml);
            Assert.All(off.Events.SelectMany(e => e.Choices), c => Assert.False(c.Enabled));
            Assert.Equal(Parse(original).SoundCount, off.SoundCount);
            Assert.Equal(Parse(original).Issues.Count, off.Issues.Count);
            Assert.Contains("# What plays when.", yaml); // comments survive

            foreach (string id in ids)
            {
                yaml = Toggle(yaml, id, true);
            }

            Assert.All(Parse(yaml).Events.SelectMany(e => e.Choices), c => Assert.True(c.Enabled));
        }
    }

    public class TemplateDetectionTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "soundboard-tpl-" + Guid.NewGuid().ToString("N"));

        public TemplateDetectionTests()
        {
            Directory.CreateDirectory(Path.Combine(root, "mod"));
            Directory.CreateDirectory(Path.Combine(root, "user"));
        }

        public void Dispose()
        {
            Directory.Delete(root, true);
        }

        private ConfigLocations Locations() => new ConfigLocations(Path.Combine(root, "mod"), Path.Combine(root, "user"));

        [Fact]
        public void FlagsATemplateThatIsNewerAndDifferent()
        {
            ConfigLocations locations = Locations();
            File.WriteAllText(locations.UserConfigPath, "mine");
            File.WriteAllText(locations.BundledConfigPath, "new template");
            File.SetLastWriteTimeUtc(locations.UserConfigPath, DateTime.UtcNow.AddMinutes(-10));
            Assert.True(locations.TemplateIsNewerThanUserCopy());
        }

        [Fact]
        public void IgnoresATemplateThatIsOlderOrIdentical()
        {
            ConfigLocations locations = Locations();
            File.WriteAllText(locations.UserConfigPath, "same\r\n");
            File.WriteAllText(locations.BundledConfigPath, "same\n");
            File.SetLastWriteTimeUtc(locations.UserConfigPath, DateTime.UtcNow.AddMinutes(-10));
            Assert.False(locations.TemplateIsNewerThanUserCopy()); // newer but the same apart from line endings

            File.WriteAllText(locations.BundledConfigPath, "different");
            File.SetLastWriteTimeUtc(locations.BundledConfigPath, DateTime.UtcNow.AddMinutes(-20));
            Assert.False(locations.TemplateIsNewerThanUserCopy()); // different but older
        }

        [Fact]
        public void MissingFilesAreNotAProblem()
        {
            Assert.False(Locations().TemplateIsNewerThanUserCopy());
        }
    }
}
