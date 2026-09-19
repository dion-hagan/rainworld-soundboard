using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace SoundboardMod.Tests
{
    /// <summary>What the options screen's "Add Sound" page does to soundboard.yaml.</summary>
    public class AddSoundTests
    {
        private static readonly EventCatalog Catalog = new EventCatalog(new[] { "Scavenger", "GreenLizard" });

        private static SoundboardConfig Parse(string yaml) => SoundboardConfigParser.Parse(yaml, Catalog);

        private static NewSound Sound(string ev, string file, float volume = 1f, float delay = 0f) =>
            new NewSound { EventName = ev, File = file, Volume = volume, Delay = delay };

        private static string Add(string yaml, NewSound sound)
        {
            YamlEditor.Result result = SoundAdder.Add(yaml, sound, Catalog);
            Assert.True(result.Ok, result.Error);
            return result.Text;
        }

        private static string Fail(string yaml, NewSound sound)
        {
            YamlEditor.Result result = SoundAdder.Add(yaml, sound, Catalog);
            Assert.False(result.Ok);
            Assert.Equal(yaml, result.Text); // a refusal never hands back changed text
            return result.Error;
        }

        /// <summary>The original's lines must all still be there, in order, untouched: the edit only inserts.</summary>
        private static void AssertOnlyInsertions(string original, string edited)
        {
            string[] want = original.Split('\n');
            string[] have = edited.Split('\n');
            int at = 0;
            foreach (string line in have)
            {
                if (at < want.Length && line == want[at])
                {
                    at++;
                }
            }

            Assert.Equal(want.Length, at);
        }

        // --- adding to an event that exists ---------------------------------------------

        [Fact]
        public void ANewSoundGoesAtTheEndOfTheEventsListInLineWithTheOthers()
        {
            const string yaml =
                "events:\n" +
                "  PlayerDeath:\n" +
                "    - file: a.wav\n" +
                "      volume: 0.2\n" +
                "    - b.wav\n" +
                "\n" +
                "  PlayerJump:\n" +
                "    - c.wav\n";

            string edited = Add(yaml, Sound("PlayerDeath", "new.wav", 0.5f, 1.5f));

            Assert.Equal(
                "events:\n  PlayerDeath:\n    - file: a.wav\n      volume: 0.2\n    - b.wav\n    - file: new.wav\n      volume: 0.5\n      delay: 1.5\n\n  PlayerJump:\n    - c.wav\n",
                edited);
            AssertOnlyInsertions(yaml, edited);

            SoundChoice added = Parse(edited).Events.Single(e => e.EventName == "PlayerDeath").Choices.Last();
            Assert.Equal("new.wav", added.Sounds[0].File);
            Assert.Equal(0.5f, added.Sounds[0].Volume);
            Assert.Equal(1.5f, added.Sounds[0].Delay);
        }

        [Fact]
        public void DefaultVolumeAndDelayAreLeftOutAndTheEntryIsJustAFileName()
        {
            const string yaml = "events:\n  PlayerDeath:\n    - a.wav\n";
            Assert.Equal("events:\n  PlayerDeath:\n    - a.wav\n    - new.wav\n", Add(yaml, Sound("PlayerDeath", "new.wav")));
        }

        [Fact]
        public void OnlyTheNonDefaultOptionIsWritten()
        {
            const string yaml = "events:\n  PlayerDeath:\n    - a.wav\n";
            Assert.Equal("events:\n  PlayerDeath:\n    - a.wav\n    - file: v.wav\n      volume: 0.3\n", Add(yaml, Sound("PlayerDeath", "v.wav", 0.3f)));
            Assert.Equal("events:\n  PlayerDeath:\n    - a.wav\n    - file: d.wav\n      delay: 2\n", Add(yaml, Sound("PlayerDeath", "d.wav", 1f, 2f)));
        }

        [Fact]
        public void TheDashesMayBeAtTheSameIndentAsTheEventName()
        {
            const string yaml = "events:\n  PlayerDeath:\n  - a.wav\n  - file: b.wav\n    volume: 0.4\n  PlayerJump:\n  - c.wav\n";
            string edited = Add(yaml, Sound("PlayerDeath", "new.wav", 0.7f));

            Assert.Equal("events:\n  PlayerDeath:\n  - a.wav\n  - file: b.wav\n    volume: 0.4\n  - file: new.wav\n    volume: 0.7\n  PlayerJump:\n  - c.wav\n", edited);
            Assert.Equal(3, Parse(edited).Events.Single(e => e.EventName == "PlayerDeath").Choices.Count);
        }

        [Fact]
        public void ItLandsAfterAGroupsNestedLinesNotInsideIt()
        {
            const string yaml = "events:\n  PlayerDeath:\n    - together:\n        - one.wav\n        - two.wav\n";
            string edited = Add(yaml, Sound("PlayerDeath", "solo.wav"));

            Assert.Equal("events:\n  PlayerDeath:\n    - together:\n        - one.wav\n        - two.wav\n    - solo.wav\n", edited);
            Assert.Equal(2, Parse(edited).Events[0].Choices.Count);
        }

        [Theory]
        [InlineData("player death")]
        [InlineData("PLAYER-DEATH")]
        [InlineData("playerdeath")]
        public void EventNamesAreMatchedTheWayTheConfigMatchesThem(string spelling)
        {
            const string yaml = "events:\n  player-death:\n    - a.wav\n";
            string edited = Add(yaml, Sound(spelling, "new.wav"));

            Assert.Equal("events:\n  player-death:\n    - a.wav\n    - new.wav\n", edited); // the player's own spelling of the key is kept
        }

        [Fact]
        public void AnEventWrittenTwiceGetsTheSoundInItsLastBlock()
        {
            const string yaml = "events:\n  PlayerDeath:\n    - a.wav\n  player death:\n    - b.wav\n";
            string edited = Add(yaml, Sound("PlayerDeath", "new.wav"));

            Assert.Equal("events:\n  PlayerDeath:\n    - a.wav\n  player death:\n    - b.wav\n    - new.wav\n", edited);
            Assert.Equal("new.wav", Parse(edited).Events[0].Choices.Last().Sounds[0].File);
        }

        [Fact]
        public void AnEventWithAnEmptyListGetsItsFirstSound()
        {
            const string yaml = "events:\n  PlayerDeath:   # nothing yet\n  PlayerJump:\n    - c.wav\n";
            string edited = Add(yaml, Sound("PlayerDeath", "new.wav"));

            Assert.Equal("events:\n  PlayerDeath:   # nothing yet\n    - new.wav\n  PlayerJump:\n    - c.wav\n", edited);
        }

        [Fact]
        public void AnInlineListIsExtendedInsideItsBrackets()
        {
            const string yaml = "events:\n  PlayerDeath: [a.wav, b.wav]   # two\n  PlayerJump:\n    - c.wav\n";
            string edited = Add(yaml, Sound("PlayerDeath", "new.wav", 0.5f));

            Assert.Equal("events:\n  PlayerDeath: [a.wav, b.wav, { file: new.wav, volume: 0.5 }]   # two\n  PlayerJump:\n    - c.wav\n", edited);
            Assert.Equal(3, Parse(edited).Events[0].Choices.Count);
        }

        [Fact]
        public void AnEmptyInlineListTakesTheFirstItem()
        {
            string edited = Add("events:\n  PlayerDeath: []\n", Sound("PlayerDeath", "new.wav"));
            Assert.Equal("events:\n  PlayerDeath: [new.wav]\n", edited);
            Assert.Single(Parse(edited).Events[0].Choices);
        }

        [Fact]
        public void ListsWrittenInWaysThatCantBeEditedSafelyAreRefusedWithAReason()
        {
            string multiLine = Fail("events:\n  PlayerDeath: [a.wav,\n    b.wav]\n", Sound("PlayerDeath", "n.wav"));
            Assert.Contains("by hand", multiLine);

            string single = Fail("events:\n  PlayerDeath: a.wav\n", Sound("PlayerDeath", "n.wav"));
            Assert.Contains("turn it into a list", single);

            string mapping = Fail("events:\n  PlayerDeath:\n    file: a.wav\n    volume: 0.2\n", Sound("PlayerDeath", "n.wav"));
            Assert.Contains("turn it into a list", mapping);
        }

        // --- an event that isn't in the file yet ----------------------------------------

        [Fact]
        public void ANewEventIsAddedAfterTheLastOneSetApartByABlankLine()
        {
            const string yaml = "events:\n  PlayerDeath:\n    - a.wav\n";
            string edited = Add(yaml, Sound("PlayerJump", "new.wav", 0.4f));

            Assert.Equal("events:\n  PlayerDeath:\n    - a.wav\n\n  PlayerJump:\n    - file: new.wav\n      volume: 0.4\n", edited);
            Assert.Equal(new[] { "PlayerDeath", "PlayerJump" }, Parse(edited).Events.Select(e => e.EventName).ToArray());
        }

        [Fact]
        public void ANewEventUsesTheIndentOfTheOtherEvents()
        {
            const string yaml = "events:\n    PlayerDeath:\n        - a.wav\n";
            string edited = Add(yaml, Sound("PlayerJump", "new.wav"));
            Assert.Equal("events:\n    PlayerDeath:\n        - a.wav\n\n    PlayerJump:\n        - new.wav\n", edited);
            Assert.Equal(2, Parse(edited).Events.Count);
        }

        [Fact]
        public void ANewEventCopiesHowFarThePlayerIndentsTheirLists()
        {
            // Dashes level with the event name: the new event's dashes are level too.
            Assert.Equal("events:\n  PlayerDeath:\n  - a.wav\n\n  PlayerJump:\n  - new.wav\n", Add("events:\n  PlayerDeath:\n  - a.wav\n", Sound("PlayerJump", "new.wav")));

            // An event with a comment where its list was: the list follows the others' indent too.
            Assert.Equal(
                "events:\n  PlayerDeath:\n        - a.wav\n  PlayerJump:\n        - new.wav\n",
                Add("events:\n  PlayerDeath:\n        - a.wav\n  PlayerJump:\n", Sound("PlayerJump", "new.wav")));
        }

        [Fact]
        public void ANewEventGoesBeforeASectionThatComesAfterEvents()
        {
            const string yaml = "events:\n  PlayerDeath:\n    - a.wav\n\nsettings:\n  debug: true\n";
            string edited = Add(yaml, Sound("PlayerJump", "new.wav"));

            Assert.Equal("events:\n  PlayerDeath:\n    - a.wav\n\n  PlayerJump:\n    - new.wav\n\nsettings:\n  debug: true\n", edited);
            Assert.True(Parse(edited).Settings.Debug);
        }

        [Fact]
        public void ACreatureEventIsWrittenUnderItsCanonicalName()
        {
            string edited = Add("events:\n  PlayerDeath:\n    - a.wav\n", Sound("greenlizard death", "pop.wav"));
            Assert.Contains("  GreenLizardDeath:\n    - pop.wav", edited);
        }

        [Fact]
        public void AnEmptyEventsSectionGetsItsFirstEvent()
        {
            string edited = Add("settings:\n  debug: false\n\nevents:\n", Sound("PlayerDeath", "new.wav"));
            Assert.Equal("settings:\n  debug: false\n\nevents:\n  PlayerDeath:\n    - new.wav\n", edited);
        }

        [Fact]
        public void AFileWithNoEventsSectionGetsOne()
        {
            string edited = Add("settings:\n  debug: false\n", Sound("PlayerDeath", "new.wav"));
            Assert.Equal("settings:\n  debug: false\n\nevents:\n  PlayerDeath:\n    - new.wav\n", edited);
            Assert.False(Parse(edited).Settings.Debug);
        }

        [Fact]
        public void AnEmptyFileBecomesAValidOne()
        {
            string edited = Add(string.Empty, Sound("PlayerDeath", "new.wav", 0.5f));
            Assert.Equal("events:\n  PlayerDeath:\n    - file: new.wav\n      volume: 0.5\n", edited);
            Assert.Single(Parse(edited).Events);
        }

        [Fact]
        public void ANewEventIsInsertedBeforeTheTrailingCommentedExamplesNotAfterThem()
        {
            const string yaml =
                "events:\n" +
                "  PlayerDeath:\n" +
                "    - a.wav\n" +
                "\n" +
                "  # ------\n" +
                "  # PlayerJump:\n" +
                "  #   - boing.wav\n";

            string edited = Add(yaml, Sound("PlayerJump", "new.wav"));

            Assert.Equal("events:\n  PlayerDeath:\n    - a.wav\n\n  PlayerJump:\n    - new.wav\n\n  # ------\n  # PlayerJump:\n  #   - boing.wav\n", edited);
            AssertOnlyInsertions(yaml, edited);
        }

        // --- the text around the edit is left alone ---------------------------------------

        [Fact]
        public void WindowsLineEndingsAreKept()
        {
            const string yaml = "events:\r\n  PlayerDeath:\r\n    - a.wav\r\n  PlayerJump:\r\n    - c.wav\r\n";

            string toExisting = Add(yaml, Sound("PlayerDeath", "n.wav", 0.5f));
            Assert.Equal("events:\r\n  PlayerDeath:\r\n    - a.wav\r\n    - file: n.wav\r\n      volume: 0.5\r\n  PlayerJump:\r\n    - c.wav\r\n", toExisting);

            string toNew = Add(yaml, Sound("PlayerEat", "n.wav"));
            Assert.DoesNotContain("\n" + "  PlayerEat:\n", toNew);
            Assert.Equal(toNew.Count(c => c == '\n'), toNew.Count(c => c == '\r'));
        }

        [Fact]
        public void CommentsQuotingAndOddLayoutElsewhereSurvive()
        {
            const string yaml =
                "# my notes\n" +
                "settings:\n" +
                "  debug: true      # keep\n" +
                "events:\n" +
                "  PlayerDeath:\n" +
                "    - \"my sound.wav\"   # quoted\n" +
                "    - { file: b.wav, volume: 0.5 }\n" +
                "    # a comment between\n" +
                "    - file: c.wav\n" +
                "      enabled: false\n";

            string edited = Add(yaml, Sound("PlayerDeath", "new.wav"));

            AssertOnlyInsertions(yaml, edited);
            SoundboardConfig config = Parse(edited);
            Assert.Empty(config.Issues);
            Assert.Equal(new[] { true, true, false, true }, config.Events[0].Choices.Select(c => c.Enabled).ToArray());
        }

        [Fact]
        public void ATrailingCommentOnTheLastLineDoesNotBreakTheInsert()
        {
            const string yaml = "events:\n  PlayerDeath:\n    - a.wav   # last one, no newline after";
            string edited = Add(yaml, Sound("PlayerDeath", "new.wav"));
            Assert.Equal("events:\n  PlayerDeath:\n    - a.wav   # last one, no newline after\n    - new.wav", edited);
        }

        // --- file names --------------------------------------------------------------------

        [Theory]
        [InlineData("vine-boom.wav")]
        [InlineData("my sound (2).wav")]
        [InlineData("memes/vine boom.ogg")]
        [InlineData("it's loud.wav")]
        [InlineData("track #1.wav")]
        [InlineData("what: why.mp3")]
        [InlineData("a, b.wav")]
        [InlineData("[draft].wav")]
        [InlineData("- dash first.wav")]
        [InlineData("música-ñ.wav")]
        [InlineData("say \"hi\".wav")]
        public void UnusualFileNamesComeBackExactlyAsPicked(string file)
        {
            string edited = Add("events:\n  PlayerDeath:\n    - a.wav\n", Sound("PlayerDeath", file, 0.5f));

            SoundboardConfig config = Parse(edited);
            Assert.Empty(config.Issues);
            Assert.Equal(file, config.Events[0].Choices.Last().Sounds[0].File);

            // ...and the same inside an inline list.
            string inline = Add("events:\n  PlayerDeath: [a.wav]\n", Sound("PlayerDeath", file, 0.5f));
            Assert.Equal(file, Parse(inline).Events[0].Choices.Last().Sounds[0].File);
        }

        [Fact]
        public void PlainNamesAreWrittenWithoutQuotes()
        {
            Assert.Equal("a-b_c (2).wav", YamlEditor.Scalar("a-b_c (2).wav"));
            Assert.Equal("'it''s.wav'", YamlEditor.Scalar("it's.wav"));
            Assert.Equal("'#hash.wav'", YamlEditor.Scalar("#hash.wav"));
        }

        [Fact]
        public void BackslashesInThePathBecomeForwardSlashes()
        {
            string edited = Add("events:\n  PlayerDeath:\n    - a.wav\n", Sound("PlayerDeath", "memes\\boom.wav"));
            Assert.Contains("    - memes/boom.wav", edited);
        }

        // --- refusals ------------------------------------------------------------------------

        [Fact]
        public void UnknownEventsAreRefused()
        {
            Assert.Contains("isn't an event", Fail("events:\n  PlayerDeath:\n    - a.wav\n", Sound("NoSuchThing", "n.wav")));
            Assert.Contains("isn't an event", Fail("events:\n  PlayerDeath:\n    - a.wav\n", Sound(null, "n.wav")));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("../secret.wav")]
        [InlineData("a/../../b.wav")]
        [InlineData("/etc/x.wav")]
        [InlineData("C:\\sounds\\x.wav")]
        [InlineData(null)]
        public void UnusableFileNamesAreRefused(string file)
        {
            Assert.Contains("usable sound file name", Fail("events:\n  PlayerDeath:\n    - a.wav\n", Sound("PlayerDeath", file)));
        }

        [Theory]
        [InlineData(-0.1f, 0f)]
        [InlineData(10.5f, 0f)]
        [InlineData(1f, -1f)]
        [InlineData(1f, 121f)]
        [InlineData(float.NaN, 0f)]
        public void OutOfRangeNumbersAreRefused(float volume, float delay)
        {
            Fail("events:\n  PlayerDeath:\n    - a.wav\n", Sound("PlayerDeath", "n.wav", volume, delay));
        }

        [Fact]
        public void TheLimitsThemselvesAreAccepted()
        {
            string edited = Add("events:\n  PlayerDeath:\n    - a.wav\n", Sound("PlayerDeath", "n.wav", 10f, 120f));
            SoundRef added = Parse(edited).Events[0].Choices.Last().Sounds[0];
            Assert.Equal(10f, added.Volume);
            Assert.Equal(120f, added.Delay);

            string silent = Add("events:\n  PlayerDeath:\n    - a.wav\n", Sound("PlayerDeath", "z.wav", 0f));
            Assert.Equal(0f, Parse(silent).Events[0].Choices.Last().Sounds[0].Volume);
        }

        [Fact]
        public void AFileWithASyntaxErrorIsNeverTouched()
        {
            string error = Fail("events:\n  PlayerDeath:\n    - a.wav\n   - b.wav\n", Sound("PlayerDeath", "n.wav"));
            Assert.Contains("fix that first", error);
        }

        [Fact]
        public void AnEventsSectionThatCantBeEditedIsRefusedWithAReason()
        {
            Assert.Contains("by hand", Fail("events: {}\n", Sound("PlayerDeath", "n.wav")));
            Assert.Contains("by hand", Fail("events: ~\n", Sound("PlayerDeath", "n.wav")));
        }

        [Fact]
        public void EveryEventInTheCatalogCanBeAddedToTheShippedFile()
        {
            string yaml = File.ReadAllText(Path.Combine(FindRepoRoot(), "mod", "soundboard.yaml"));
            SoundboardConfig baseline = Parse(yaml);

            foreach (EventInfo info in Catalog.All)
            {
                string edited = Add(yaml, Sound(info.Name, "new.wav", 0.5f, 1f));
                AssertOnlyInsertions(yaml, edited);
                SoundboardConfig after = Parse(edited);
                Assert.Equal(baseline.Issues.Count, after.Issues.Count);
                Assert.Equal(baseline.SoundCount + 1, after.SoundCount);
                Assert.Equal("new.wav", after.Events.Single(e => e.EventName == info.Name).Choices.Last().Sounds[0].File);
            }
        }

        [Fact]
        public void AddingSeveralSoundsOneAfterAnotherKeepsTheirOrder()
        {
            string yaml = "events:\n  PlayerDeath:\n    - a.wav\n";
            yaml = Add(yaml, Sound("PlayerDeath", "b.wav"));
            yaml = Add(yaml, Sound("PlayerJump", "j1.wav"));
            yaml = Add(yaml, Sound("PlayerDeath", "c.wav"));
            yaml = Add(yaml, Sound("PlayerJump", "j2.wav"));

            SoundboardConfig config = Parse(yaml);
            Assert.Empty(config.Issues);
            Assert.Equal(new[] { "a.wav", "b.wav", "c.wav" }, config.Events.Single(e => e.EventName == "PlayerDeath").Choices.Select(c => c.Sounds[0].File).ToArray());
            Assert.Equal(new[] { "j1.wav", "j2.wav" }, config.Events.Single(e => e.EventName == "PlayerJump").Choices.Select(c => c.Sounds[0].File).ToArray());
        }

        [Fact]
        public void ABlockListRecordsTheColumnOfItsDashes()
        {
            YamlNode root = MiniYaml.Parse("events:\n  PlayerDeath:\n    - a.wav\n  PlayerJump:\n  - b.wav\n");
            YamlNode events = root.Find("events").Value;
            Assert.Equal(4, events.Find("PlayerDeath").Value.Indent);
            Assert.Equal(2, events.Find("PlayerJump").Value.Indent);
        }

        private static string FindRepoRoot()
        {
            string dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "SoundboardMod.sln")))
            {
                dir = Path.GetDirectoryName(dir);
            }

            Assert.NotNull(dir);
            return dir;
        }
    }

    public class SoundLibraryTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "soundlib-" + Guid.NewGuid().ToString("N"));

        public SoundLibraryTests()
        {
            Directory.CreateDirectory(root);
        }

        public void Dispose()
        {
            try { Directory.Delete(root, true); } catch (IOException) { }
        }

        private string Touch(string relative)
        {
            string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, new byte[] { 1 });
            return path;
        }

        [Fact]
        public void ListsPlayableFilesSortedWithForwardSlashesAndIgnoresTheRest()
        {
            Touch("user/b.wav");
            Touch("user/A.ogg");
            Touch("user/c.MP3");
            Touch("user/notes.txt");
            Touch("user/cover.png");
            Touch("user/memes/deep/zed.wav");

            List<string> files = SoundLibrary.List(new[] { Path.Combine(root, "user") });

            Assert.Equal(new[] { "A.ogg", "b.wav", "c.MP3", "memes/deep/zed.wav" }, files.ToArray());
        }

        [Fact]
        public void ANameInBothFoldersIsListedOnceAndAllOthersAreMerged()
        {
            Touch("user/boom.wav");
            Touch("user/mine.wav");
            Touch("bundled/BOOM.wav");
            Touch("bundled/other.ogg");

            List<string> files = SoundLibrary.List(new[] { Path.Combine(root, "user"), Path.Combine(root, "bundled") });

            Assert.Equal(new[] { "boom.wav", "mine.wav", "other.ogg" }, files.ToArray()); // the user's spelling wins
        }

        [Fact]
        public void MissingFoldersAndTrailingSlashesAreFine()
        {
            Touch("user/a.wav");
            Assert.Empty(SoundLibrary.List(new[] { Path.Combine(root, "nope") }));
            Assert.Equal(new[] { "a.wav" }, SoundLibrary.List(new[] { Path.Combine(root, "nope"), Path.Combine(root, "user") + Path.DirectorySeparatorChar }).ToArray());
            Assert.Empty(SoundLibrary.List(new string[0]));
        }

        [Fact]
        public void EveryListedNameIsFoundAgainByTheResolver()
        {
            Touch("user/a.wav");
            Touch("user/sub dir/b c.ogg");
            Touch("bundled/d.mp3");
            string[] folders = { Path.Combine(root, "user"), Path.Combine(root, "bundled") };

            foreach (string name in SoundLibrary.List(folders))
            {
                Assert.NotNull(SoundFileResolver.Find(name, folders, out string problem));
                Assert.Null(problem);
            }
        }
    }
}
