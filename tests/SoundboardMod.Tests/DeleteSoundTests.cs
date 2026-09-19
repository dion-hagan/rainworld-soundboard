using System;
using System.IO;
using System.Linq;
using Xunit;

namespace SoundboardMod.Tests
{
    /// <summary>What the options screen's Delete boxes do to soundboard.yaml.</summary>
    public class DeleteSoundTests
    {
        private static readonly EventCatalog Catalog = new EventCatalog(new[] { "Scavenger", "GreenLizard" });

        private static SoundboardConfig Parse(string yaml) => SoundboardConfigParser.Parse(yaml, Catalog);

        private static SoundChoice Entry(string yaml, string id) =>
            Parse(yaml).Events.SelectMany(e => e.Choices).Single(c => c.Id == id);

        private static EntryDelete Whole(string id) => new EntryDelete { ChoiceId = id, WholeEntry = true };

        /// <summary>A request to remove the given members (by position in the entry's together list).</summary>
        private static EntryDelete Members(string yaml, string id, params int[] positions)
        {
            SoundChoice entry = Entry(yaml, id);
            var request = new EntryDelete { ChoiceId = id };
            foreach (int p in positions)
            {
                request.Sounds.Add(new SoundTweak { Member = entry.Sounds[p].Member, File = entry.Sounds[p].File });
            }

            return request;
        }

        private static string Delete(string yaml, EntryDelete request)
        {
            YamlEditor.Result result = SoundTweaker.Delete(yaml, request, Catalog);
            Assert.True(result.Ok, result.Error);
            return result.Text;
        }

        private static string Fail(string yaml, EntryDelete request)
        {
            YamlEditor.Result result = SoundTweaker.Delete(yaml, request, Catalog);
            Assert.False(result.Ok);
            Assert.Equal(yaml, result.Text); // a refusal never hands back changed text
            return result.Error;
        }

        // --- whole entries ---------------------------------------------------------------------

        [Fact]
        public void ASingleEntryIsRemovedWithAllItsLines()
        {
            const string yaml = "events:\n  PlayerDeath:\n    - a.wav\n    - file: b.wav\n      volume: 0.5\n      name: Bee\n    - c.wav\n";

            string edited = Delete(yaml, Whole("PlayerDeath_Bee"));

            Assert.Equal("events:\n  PlayerDeath:\n    - a.wav\n    - c.wav\n", edited);
            Assert.Equal(new[] { "a.wav", "c.wav" }, Parse(edited).Events[0].Choices.Select(c => c.Sounds[0].File).ToArray());
        }

        [Fact]
        public void ACommentOnTheEntrysOwnLinesGoesButCommentsAroundItStay()
        {
            const string yaml =
                "events:\n  PlayerDeath:\n" +
                "    # about a\n" +
                "    - a.wav   # the classic\n" +
                "    # about b\n" +
                "    - file: b.wav   # loud\n" +
                "      # inside b\n" +
                "      volume: 0.5\n" +
                "    - c.wav\n";

            string edited = Delete(yaml, Whole("PlayerDeath_b"));

            Assert.Equal("events:\n  PlayerDeath:\n    # about a\n    - a.wav   # the classic\n    # about b\n    - c.wav\n", edited);
        }

        [Fact]
        public void ARemovedEntryDoesNotDisturbTheRestOfTheFile()
        {
            const string yaml =
                "# my notes\nsettings:\n  debug: true   # keep\n\nevents:\n  PlayerDeath:\n    - a.wav\n    - b.wav\n\n  PlayerJump:\n    - c.wav\n";

            string edited = Delete(yaml, Whole("PlayerDeath_a"));

            Assert.Equal("# my notes\nsettings:\n  debug: true   # keep\n\nevents:\n  PlayerDeath:\n    - b.wav\n\n  PlayerJump:\n    - c.wav\n", edited);
            Assert.True(Parse(edited).Settings.Debug);
        }

        [Fact]
        public void RemovingAnEventsLastEntryRemovesTheEmptyEventToo()
        {
            const string yaml = "events:\n  PlayerDeath:   # only one\n    - a.wav\n\n  PlayerJump:\n    - c.wav\n";

            string edited = Delete(yaml, Whole("PlayerDeath_a"));

            Assert.Equal("events:\n\n  PlayerJump:\n    - c.wav\n", edited);
            SoundboardConfig config = Parse(edited);
            Assert.Empty(config.Issues); // no "has no sounds listed" leftovers
            Assert.Equal(new[] { "PlayerJump" }, config.Events.Select(e => e.EventName).ToArray());
        }

        [Fact]
        public void DashesLevelWithTheEventNameWorkToo()
        {
            const string yaml = "events:\n  PlayerDeath:\n  - a.wav\n  - b.wav\n  PlayerJump:\n  - c.wav\n";
            Assert.Equal("events:\n  PlayerDeath:\n  - b.wav\n  PlayerJump:\n  - c.wav\n", Delete(yaml, Whole("PlayerDeath_a")));
            Assert.Equal("events:\n  PlayerDeath:\n  - a.wav\n  - b.wav\n", Delete(yaml, Whole("PlayerJump_c")));
        }

        [Fact]
        public void AnInlineEntryOnItsOwnLineIsRemoved()
        {
            const string yaml = "events:\n  PlayerDeath:\n    - { file: a.wav, volume: 0.5 }   # loud\n    - b.wav\n";
            Assert.Equal("events:\n  PlayerDeath:\n    - b.wav\n", Delete(yaml, Whole("PlayerDeath_a")));
        }

        [Fact]
        public void ASoundWrittenStraightUnderTheEventNameTakesTheNameWithIt()
        {
            Assert.Equal("events:\n  PlayerJump:\n    - c.wav\n", Delete("events:\n  PlayerDeath: a.wav\n  PlayerJump:\n    - c.wav\n", Whole("PlayerDeath_a")));

            const string block = "events:\n  PlayerDeath:\n    file: a.wav\n    volume: 0.4\n  PlayerJump:\n    - c.wav\n";
            Assert.Equal("events:\n  PlayerJump:\n    - c.wav\n", Delete(block, Whole("PlayerDeath_a")));
        }

        [Fact]
        public void AWholeGroupIsRemovedWithItsNestedLines()
        {
            const string yaml =
                "events:\n  PlayerDeath:\n    - solo.wav\n    - together:\n        - one.wav\n        - file: two.wav\n          volume: 0.3\n      name: Pair\n      cooldown: 5\n    - last.wav\n";

            string edited = Delete(yaml, Whole("PlayerDeath_Pair"));

            Assert.Equal("events:\n  PlayerDeath:\n    - solo.wav\n    - last.wav\n", edited);
            Assert.Empty(Parse(edited).Issues);
        }

        [Fact]
        public void AnEntryInsideAnInlineListIsRefusedWithAReason()
        {
            const string yaml = "events:\n  PlayerDeath: [a.wav, b.wav]\n";
            Assert.Contains("by hand", Fail(yaml, Whole("PlayerDeath_a")));
        }

        [Fact]
        public void AnEntryWrittenAcrossLinesInBracesIsRemovedWholeAndAnInlineGroupToo()
        {
            const string multi = "events:\n  PlayerDeath:\n    - { file: a.wav,\n        volume: 0.5 }\n    - b.wav\n";
            Assert.Equal("events:\n  PlayerDeath:\n    - b.wav\n", Delete(multi, Whole("PlayerDeath_a")));

            const string group = "events:\n  PlayerDeath:\n    - { together: [one.wav, two.wav], name: G }\n    - b.wav\n";
            Assert.Equal("events:\n  PlayerDeath:\n    - b.wav\n", Delete(group, Whole("PlayerDeath_G")));
        }

        [Fact]
        public void EntriesWithTheSameNameKeepTheRightOneWhenAnEarlierOneGoes()
        {
            // The second "a" is "PlayerDeath_a_2" until the first is deleted.
            const string yaml = "events:\n  PlayerDeath:\n    - file: a.wav\n      volume: 0.1\n    - file: a.wav\n      volume: 0.9\n";

            string edited = Delete(yaml, Whole("PlayerDeath_a_2"));

            Assert.Equal("events:\n  PlayerDeath:\n    - file: a.wav\n      volume: 0.1\n", edited);
        }

        // --- sounds inside a group ---------------------------------------------------------------

        private const string GroupYaml =
            "events:\n" +
            "  PlayerDeath:\n" +
            "    - together:\n" +
            "        - one.wav\n" +
            "        - file: two.wav\n" +
            "          volume: 0.3\n" +
            "        - three.wav   # last\n" +
            "      name: Trio\n" +
            "    - solo.wav\n";

        [Theory]
        [InlineData(0, "events:\n  PlayerDeath:\n    - together:\n        - file: two.wav\n          volume: 0.3\n        - three.wav   # last\n      name: Trio\n    - solo.wav\n")]
        [InlineData(1, "events:\n  PlayerDeath:\n    - together:\n        - one.wav\n        - three.wav   # last\n      name: Trio\n    - solo.wav\n")]
        [InlineData(2, "events:\n  PlayerDeath:\n    - together:\n        - one.wav\n        - file: two.wav\n          volume: 0.3\n      name: Trio\n    - solo.wav\n")]
        public void OneSoundOfAGroupCanBeRemoved(int position, string expected)
        {
            string edited = Delete(GroupYaml, Members(GroupYaml, "PlayerDeath_Trio", position));

            Assert.Equal(expected, edited);
            SoundChoice group = Entry(edited, "PlayerDeath_Trio");
            Assert.Equal(2, group.Sounds.Count);
            Assert.Equal("Trio", group.Label);
            Assert.Empty(Parse(edited).Issues);
        }

        [Fact]
        public void SeveralSoundsOfAGroupCanBeRemovedTogether()
        {
            string edited = Delete(GroupYaml, Members(GroupYaml, "PlayerDeath_Trio", 0, 2));
            Assert.Equal("events:\n  PlayerDeath:\n    - together:\n        - file: two.wav\n          volume: 0.3\n      name: Trio\n    - solo.wav\n", edited);
            Assert.Equal(new[] { "two.wav" }, Entry(edited, "PlayerDeath_Trio").Sounds.Select(s => s.File).ToArray());
        }

        [Fact]
        public void RemovingEverySoundOfAGroupOrOneFromASingleEntryIsRefused()
        {
            Assert.Contains("every sound", Fail(GroupYaml, Members(GroupYaml, "PlayerDeath_Trio", 0, 1, 2)));

            const string single = "events:\n  PlayerDeath:\n    - a.wav\n";
            Assert.Contains("whole entry", Fail(single, Members(single, "PlayerDeath_a", 0)));
        }

        [Fact]
        public void SoundsInAnInlineGroupListAreRefused()
        {
            const string inline = "events:\n  PlayerDeath:\n    - together: [one.wav, two.wav, three.wav]\n";
            Assert.Contains("by hand", Fail(inline, Members(inline, "PlayerDeath_one_two_three", 1)));

            const string oneLine = "events:\n  PlayerDeath:\n    - { together: [one.wav, two.wav], name: G }\n";
            Assert.Contains("by hand", Fail(oneLine, Members(oneLine, "PlayerDeath_G", 0)));
        }

        // --- the file no longer matches what the screen showed ----------------------------------------

        [Fact]
        public void AnEntryThatIsGoneOrASoundThatChangedIsRefused()
        {
            Assert.Contains("no longer in", Fail(GroupYaml, Whole("PlayerDeath_missing")));

            EntryDelete stale = Members(GroupYaml, "PlayerDeath_Trio", 1);
            stale.Sounds[0].File = "something-else.wav";
            Assert.Contains("changed since", Fail(GroupYaml, stale));
        }

        [Fact]
        public void SoundsAreFoundByPlaceNotByLineWhenLinesHaveMoved()
        {
            const string shown = "events:\n  PlayerDeath:\n    - together:\n        - one.wav\n        - two.wav\n        - three.wav\n";
            const string now = "events:\n  PlayerDeath:\n    - file: filler.wav\n      volume: 0.5\n      enabled: false\n    - together:\n        - one.wav\n        - two.wav\n        - three.wav\n";

            EntryDelete fromShown = Members(shown, "PlayerDeath_one_two_three", 1);
            string edited = Delete(now, fromShown);

            // An unnamed group's id is made from its files, so it changes with them.
            Assert.Equal(new[] { "one.wav", "three.wav" }, Entry(edited, "PlayerDeath_one_three").Sounds.Select(s => s.File).ToArray());
        }

        [Fact]
        public void AFileWithASyntaxErrorIsNeverTouched()
        {
            Assert.Contains("fix that first", Fail("events:\n  PlayerDeath:\n    - a.wav\n   - b.wav\n", Whole("PlayerDeath_a")));
        }

        [Fact]
        public void NothingAskedMeansNothingChanged()
        {
            const string yaml = "events:\n  PlayerDeath:\n    - together:\n        - one.wav\n        - two.wav\n";
            Assert.Equal(yaml, Delete(yaml, new EntryDelete { ChoiceId = "PlayerDeath_one_two" }));
        }

        [Fact]
        public void WindowsLineEndingsAreKept()
        {
            const string yaml = "events:\r\n  PlayerDeath:\r\n    - a.wav\r\n    - file: b.wav\r\n      volume: 0.5\r\n    - c.wav\r\n";
            Assert.Equal("events:\r\n  PlayerDeath:\r\n    - a.wav\r\n    - c.wav\r\n", Delete(yaml, Whole("PlayerDeath_b")));
        }

        [Fact]
        public void SeveralDeletionsOneAfterAnotherEachLandOnTheRightEntry()
        {
            string yaml = "events:\n  PlayerDeath:\n    - a.wav\n    - b.wav\n    - c.wav\n  PlayerJump:\n    - together:\n        - x.wav\n        - y.wav\n        - z.wav\n";

            yaml = Delete(yaml, Whole("PlayerDeath_b"));
            yaml = Delete(yaml, Members(yaml, "PlayerJump_x_y_z", 1));
            yaml = Delete(yaml, Whole("PlayerDeath_a"));

            SoundboardConfig config = Parse(yaml);
            Assert.Empty(config.Issues);
            Assert.Equal(new[] { "c.wav" }, config.Events.Single(e => e.EventName == "PlayerDeath").Choices.Select(c => c.Sounds[0].File).ToArray());
            Assert.Equal(new[] { "x.wav", "z.wav" }, config.Events.Single(e => e.EventName == "PlayerJump").Choices[0].Sounds.Select(s => s.File).ToArray());
        }

        // --- the shipped file ------------------------------------------------------------------------

        [Fact]
        public void EveryEntryOfTheShippedFileCanBeDeletedOnItsOwn()
        {
            string yaml = File.ReadAllText(Path.Combine(FindRepoRoot(), "mod", "soundboard.yaml"));
            SoundboardConfig baseline = Parse(yaml);
            int entries = baseline.Events.Sum(e => e.Choices.Count);

            foreach (SoundChoice choice in baseline.Events.SelectMany(e => e.Choices))
            {
                string edited = Delete(yaml, Whole(choice.Id));
                SoundboardConfig after = Parse(edited);

                Assert.Equal(entries - 1, after.Events.Sum(e => e.Choices.Count));
                Assert.True(after.Issues.Count <= baseline.Issues.Count, "new problems after deleting " + choice.Id);
                Assert.DoesNotContain(after.Events.SelectMany(e => e.Choices), c => c.Id == choice.Id && c.Sounds.Select(s => s.File).SequenceEqual(choice.Sounds.Select(s => s.File)));
            }
        }

        [Fact]
        public void EverySoundOfEveryShippedGroupCanBeRemovedOnItsOwn()
        {
            string yaml = File.ReadAllText(Path.Combine(FindRepoRoot(), "mod", "soundboard.yaml"));
            SoundboardConfig baseline = Parse(yaml);
            var groups = baseline.Events.SelectMany(e => e.Choices).Where(c => c.IsGroup).ToList();
            Assert.NotEmpty(groups);

            foreach (SoundChoice group in groups)
            {
                int position = baseline.Events.SelectMany(e => e.Choices).ToList().IndexOf(group);
                for (int i = 0; i < group.Sounds.Count; i++)
                {
                    string edited = Delete(yaml, Members(yaml, group.Id, i));
                    SoundChoice after = Parse(edited).Events.SelectMany(e => e.Choices).ElementAt(position); // same place in the file
                    Assert.Equal(group.Sounds.Count - 1, after.Sounds.Count);
                    Assert.DoesNotContain(after.Sounds, s => s.File == group.Sounds[i].File);
                }
            }
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
}
