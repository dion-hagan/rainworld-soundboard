using System;
using System.IO;
using System.Linq;
using Xunit;

namespace SoundboardMod.Tests
{
    /// <summary>What the options screen's "Edit Sound" page does to entries already in soundboard.yaml.</summary>
    public class EditSoundTests
    {
        private static readonly EventCatalog Catalog = new EventCatalog(new[] { "Scavenger", "GreenLizard" });

        private static SoundboardConfig Parse(string yaml) => SoundboardConfigParser.Parse(yaml, Catalog);

        private static SoundChoice Entry(string yaml, string id) =>
            Parse(yaml).Events.SelectMany(e => e.Choices).Single(c => c.Id == id);

        /// <summary>Builds the tweak the screen would send: numbers for the entry's sounds by position, plus a cooldown.</summary>
        private static EntryTweak Tweak(string yaml, string id, float? cooldown, params (float? volume, float? delay)[] sounds)
        {
            SoundChoice entry = Entry(yaml, id);
            var tweak = new EntryTweak { ChoiceId = id, Cooldown = cooldown };
            for (int i = 0; i < sounds.Length; i++)
            {
                tweak.Sounds.Add(new SoundTweak { Member = entry.Sounds[i].Member, File = entry.Sounds[i].File, Volume = sounds[i].volume, Delay = sounds[i].delay });
            }

            return tweak;
        }

        private static string Edit(string yaml, EntryTweak tweak)
        {
            YamlEditor.Result result = SoundTweaker.Apply(yaml, tweak, Catalog);
            Assert.True(result.Ok, result.Error);
            return result.Text;
        }

        private static string Fail(string yaml, EntryTweak tweak)
        {
            YamlEditor.Result result = SoundTweaker.Apply(yaml, tweak, Catalog);
            Assert.False(result.Ok);
            Assert.Equal(yaml, result.Text); // a refusal never hands back changed text
            return result.Error;
        }

        // --- single entries in block form ------------------------------------------------

        [Fact]
        public void AnExistingValueIsReplacedInPlaceKeepingItsComment()
        {
            const string yaml = "events:\n  PlayerDeath:\n    - file: a.wav\n      volume: 0.2   # quiet\n      name: Boom\n    - b.wav\n";

            string edited = Edit(yaml, Tweak(yaml, "PlayerDeath_Boom", null, (0.5f, null)));

            Assert.Equal("events:\n  PlayerDeath:\n    - file: a.wav\n      volume: 0.5   # quiet\n      name: Boom\n    - b.wav\n", edited);
            Assert.Equal(0.5f, Entry(edited, "PlayerDeath_Boom").Sounds[0].Volume);
        }

        [Fact]
        public void MissingOptionsAreAddedAfterTheEntryInLineWithItsKeys()
        {
            const string yaml = "events:\n  PlayerDeath:\n    - file: a.wav\n      name: Boom\n    - b.wav\n";

            string edited = Edit(yaml, Tweak(yaml, "PlayerDeath_Boom", 30f, (0.35f, 1.5f)));

            Assert.Equal("events:\n  PlayerDeath:\n    - file: a.wav\n      name: Boom\n      volume: 0.35\n      delay: 1.5\n      cooldown: 30\n    - b.wav\n", edited);
            SoundChoice entry = Entry(edited, "PlayerDeath_Boom");
            Assert.Equal(0.35f, entry.Sounds[0].OwnVolume);
            Assert.Equal(1.5f, entry.Sounds[0].OwnDelay);
            Assert.Equal(30f, entry.Cooldown);
            Assert.Empty(Parse(edited).Issues);
        }

        [Fact]
        public void ABareFileNameIsExpandedToMakeRoom()
        {
            const string yaml = "events:\n  PlayerDeath:\n    - a.wav   # the classic\n    - b.wav\n";

            string edited = Edit(yaml, Tweak(yaml, "PlayerDeath_a", 5f, (0.4f, null)));

            Assert.Equal("events:\n  PlayerDeath:\n    - file: a.wav   # the classic\n      volume: 0.4\n      cooldown: 5\n    - b.wav\n", edited);
            Assert.Equal("a.wav", Entry(edited, "PlayerDeath_a").Sounds[0].File);
        }

        [Fact]
        public void QuotedFileNamesSurviveExpansion()
        {
            const string yaml = "events:\n  PlayerDeath:\n    - \"my sound.wav\"\n";
            string edited = Edit(yaml, Tweak(yaml, "PlayerDeath_my_sound", null, (0.4f, null)));
            Assert.Equal("events:\n  PlayerDeath:\n    - file: \"my sound.wav\"\n      volume: 0.4\n", edited);
            Assert.Equal("my sound.wav", Entry(edited, "PlayerDeath_my_sound").Sounds[0].File);
        }

        [Fact]
        public void TheFirstKeyOnTheDashLineCanBeTheOneChanged()
        {
            const string yaml = "events:\n  PlayerDeath:\n    - volume: 0.9\n      file: a.wav\n";
            string edited = Edit(yaml, Tweak(yaml, "PlayerDeath_a", null, (0.1f, null)));
            Assert.Equal("events:\n  PlayerDeath:\n    - volume: 0.1\n      file: a.wav\n", edited);
        }

        [Fact]
        public void AnExistingCooldownAndAnEmptyValueAreHandled()
        {
            const string yaml = "events:\n  PlayerDeath:\n    - file: a.wav\n      cooldown: 10\n      delay:\n";

            string edited = Edit(yaml, Tweak(yaml, "PlayerDeath_a", 0f, (null, 2f)));

            Assert.Equal("events:\n  PlayerDeath:\n    - file: a.wav\n      cooldown: 0\n      delay: 2\n", edited);
            Assert.Equal(0f, Entry(edited, "PlayerDeath_a").Cooldown);
            Assert.Equal(2f, Entry(edited, "PlayerDeath_a").Sounds[0].Delay);
        }

        [Fact]
        public void NumbersThatDontChangeAreNotTouched()
        {
            const string yaml = "events:\n  PlayerDeath:\n    - a.wav\n    - file: b.wav\n      volume: 0.5\n";

            // The screen sends every box's value; the ones equal to what's already there must not rewrite anything.
            Assert.Equal(yaml, Edit(yaml, Tweak(yaml, "PlayerDeath_a", 0f, (1f, 0f))));
            Assert.Equal(yaml, Edit(yaml, Tweak(yaml, "PlayerDeath_b", 0f, (0.5f, 0f))));
            Assert.Equal(yaml, Edit(yaml, Tweak(yaml, "PlayerDeath_b", null)));
        }

        // --- inline entries ----------------------------------------------------------------

        [Fact]
        public void InlineEntriesGetTheirValuesChangedInsideTheBraces()
        {
            const string yaml = "events:\n  PlayerDeath:\n    - { file: a.wav, volume: 0.5 }   # loud\n    - b.wav\n";

            string edited = Edit(yaml, Tweak(yaml, "PlayerDeath_a", 4f, (0.25f, 1f)));

            Assert.Equal("events:\n  PlayerDeath:\n    - { file: a.wav, volume: 0.25, delay: 1, cooldown: 4 }   # loud\n    - b.wav\n", edited);
            SoundChoice entry = Entry(edited, "PlayerDeath_a");
            Assert.Equal(new[] { 0.25f, 1f, 4f }, new[] { entry.Sounds[0].Volume, entry.Sounds[0].Delay, entry.Cooldown });
        }

        [Fact]
        public void EntriesInsideAnInlineListAreRefusedWithAReason()
        {
            const string yaml = "events:\n  PlayerDeath: [a.wav, { file: b.wav, volume: 0.5 }]\n";
            Assert.Contains("by hand", Fail(yaml, Tweak(yaml, "PlayerDeath_b", null, (0.2f, null))));
        }

        [Fact]
        public void ASingleBareSoundUnderAnEventNameIsRefused()
        {
            const string yaml = "events:\n  PlayerDeath: a.wav\n";
            Assert.Contains("by hand", Fail(yaml, Tweak(yaml, "PlayerDeath_a", null, (0.2f, null))));
        }

        [Fact]
        public void AnEntryWrittenAcrossSeveralLinesInBracesIsRefused()
        {
            const string yaml = "events:\n  PlayerDeath:\n    - { file: a.wav,\n        volume: 0.5 }\n";
            Assert.Contains("by hand", Fail(yaml, Tweak(yaml, "PlayerDeath_a", null, (0.2f, null))));
        }

        // --- together groups ------------------------------------------------------------------

        private const string GroupYaml =
            "events:\n" +
            "  PlayerDeath:\n" +
            "    - together:\n" +
            "        - one.wav\n" +
            "        - file: two.wav\n" +
            "          volume: 0.3\n" +
            "      name: Pair\n" +
            "    - solo.wav\n";

        [Fact]
        public void EachSoundInAGroupCanBeChangedAndTheGroupHasOneCooldown()
        {
            string edited = Edit(GroupYaml, Tweak(GroupYaml, "PlayerDeath_Pair", 20f, (0.6f, null), (0.45f, 2f)));

            Assert.Equal(
                "events:\n  PlayerDeath:\n    - together:\n" +
                "        - file: one.wav\n" +
                "          volume: 0.6\n" +
                "        - file: two.wav\n" +
                "          volume: 0.45\n" +
                "          delay: 2\n" +
                "      name: Pair\n" +
                "      cooldown: 20\n" +
                "    - solo.wav\n",
                edited);

            SoundChoice group = Entry(edited, "PlayerDeath_Pair");
            Assert.Equal(new[] { "one.wav", "two.wav" }, group.Sounds.Select(s => s.File).ToArray());
            Assert.Equal(new[] { 0.6f, 0.45f }, group.Sounds.Select(s => s.OwnVolume).ToArray());
            Assert.Equal(20f, group.Cooldown);
            Assert.Empty(Parse(edited).Issues);
        }

        [Fact]
        public void AGroupsCooldownGoesAfterTheLastSoundEvenWhenThatSoundWasJustExpanded()
        {
            // The last member is a bare file name: it gets expanded (and its new lines added) while the group's
            // cooldown is added at the same spot - the cooldown must end up after the member's lines, at the group's indent.
            const string yaml = "events:\n  PlayerDeath:\n    - together:\n        - one.wav\n        - two.wav\n    - solo.wav\n";

            string edited = Edit(yaml, Tweak(yaml, "PlayerDeath_one_two", 9f, (null, null), (0.5f, 1f)));

            Assert.Equal(
                "events:\n  PlayerDeath:\n    - together:\n        - one.wav\n" +
                "        - file: two.wav\n" +
                "          volume: 0.5\n" +
                "          delay: 1\n" +
                "      cooldown: 9\n" +
                "    - solo.wav\n",
                edited);
            SoundChoice group = Entry(edited, "PlayerDeath_one_two");
            Assert.Equal(9f, group.Cooldown);
            Assert.Equal(0.5f, group.Sounds[1].OwnVolume);
            Assert.Equal(1f, group.Sounds[0].OwnVolume); // untouched
        }

        [Fact]
        public void AGroupsOwnVolumeAndDelayStayPutWhileMembersChange()
        {
            const string yaml =
                "events:\n  PlayerDeath:\n    - together:\n        - file: one.wav\n          volume: 0.4\n        - two.wav\n      volume: 0.5\n      delay: 1\n      name: G\n";

            SoundChoice before = Entry(yaml, "PlayerDeath_G");
            Assert.Equal(0.4f, before.Sounds[0].OwnVolume);
            Assert.Equal(0.2f, before.Sounds[0].Volume, 3); // 0.4 x the group's 0.5

            string edited = Edit(yaml, Tweak(yaml, "PlayerDeath_G", null, (0.8f, null)));

            Assert.Contains("          volume: 0.8\n", edited);
            Assert.Contains("      volume: 0.5\n      delay: 1\n      name: G\n", edited); // the group's own lines are unchanged
            SoundChoice after = Entry(edited, "PlayerDeath_G");
            Assert.Equal(0.8f, after.Sounds[0].OwnVolume);
            Assert.Equal(0.4f, after.Sounds[0].Volume, 3); // 0.8 x 0.5
        }

        [Fact]
        public void AGroupWrittenOnOneLineIsRefused()
        {
            const string yaml = "events:\n  PlayerDeath:\n    - { together: [one.wav, two.wav], name: G }\n";
            Assert.Contains("by hand", Fail(yaml, Tweak(yaml, "PlayerDeath_G", 5f)));
        }

        // --- the file no longer matches what the screen showed --------------------------------

        [Fact]
        public void AnEntryThatIsGoneOrANewFileNameIsRefused()
        {
            const string yaml = "events:\n  PlayerDeath:\n    - a.wav\n";

            Assert.Contains("no longer in", Fail(yaml, new EntryTweak { ChoiceId = "PlayerDeath_missing", Cooldown = 1f }));

            EntryTweak stale = Tweak(yaml, "PlayerDeath_a", null, (0.5f, null));
            stale.Sounds[0].File = "something-else.wav";
            Assert.Contains("changed since", Fail(yaml, stale));

            EntryTweak otherMember = Tweak(yaml, "PlayerDeath_a", null, (0.5f, null));
            otherMember.Sounds[0].Member += 3;
            Assert.Contains("changed since", Fail(yaml, otherMember));
        }

        [Fact]
        public void ASoundIsIdentifiedByItsPlaceInTheEntryNotByItsLine()
        {
            // The screen showed the file as it was; lines were then added above (e.g. checkbox edits saved first).
            const string shown = "events:\n  PlayerDeath:\n    - together:\n        - one.wav\n        - two.wav\n";
            const string now = "events:\n  PlayerDeath:\n    - file: filler.wav\n      enabled: false\n      volume: 0.5\n    - together:\n        - one.wav\n        - two.wav\n";

            EntryTweak fromShown = Tweak(shown, "PlayerDeath_one_two", null, (null, null), (0.4f, null));
            string edited = Edit(now, fromShown);

            Assert.Equal(0.4f, Entry(edited, "PlayerDeath_one_two").Sounds[1].OwnVolume);
            Assert.Equal("two.wav", Entry(edited, "PlayerDeath_one_two").Sounds[1].File);
        }

        [Fact]
        public void MembersKeepTheirNumberEvenWhenAnEarlierOneCantBeRead()
        {
            // The first member has no file, so the parser drops it (with an error) - the second is still member 1.
            const string yaml = "events:\n  PlayerDeath:\n    - together:\n        - volume: 0.5\n        - two.wav\n        - three.wav\n";
            SoundChoice group = Entry(yaml, "PlayerDeath_two_three");
            Assert.Equal(new[] { 1, 2 }, group.Sounds.Select(s => s.Member).ToArray());

            EntryTweak tweak = Tweak(yaml, "PlayerDeath_two_three", null, (0.7f, null));
            string edited = Edit(yaml, tweak);

            Assert.Equal(0.7f, Entry(edited, "PlayerDeath_two_three").Sounds[0].OwnVolume);
            Assert.Contains("        - file: two.wav\n          volume: 0.7\n        - three.wav", edited);
        }

        [Fact]
        public void EditingAgainstTheFileAsItIsNowWorksEvenIfLinesMoved()
        {
            // The screen was built from a shorter file; lines were added above. A tweak made from the CURRENT parse still lands right.
            const string yaml = "# a new comment\n# and another\nevents:\n  PlayerDeath:\n    - a.wav\n    - file: b.wav\n";
            string edited = Edit(yaml, Tweak(yaml, "PlayerDeath_b", null, (0.5f, null)));
            Assert.Equal("# a new comment\n# and another\nevents:\n  PlayerDeath:\n    - a.wav\n    - file: b.wav\n      volume: 0.5\n", edited);
        }

        [Theory]
        [InlineData(-0.1f, 0f, 0f)]
        [InlineData(10.5f, 0f, 0f)]
        [InlineData(1f, -1f, 0f)]
        [InlineData(1f, 121f, 0f)]
        [InlineData(1f, 0f, -1f)]
        [InlineData(1f, 0f, 3601f)]
        [InlineData(float.NaN, 0f, 0f)]
        public void OutOfRangeNumbersAreRefused(float volume, float delay, float cooldown)
        {
            const string yaml = "events:\n  PlayerDeath:\n    - a.wav\n";
            Fail(yaml, Tweak(yaml, "PlayerDeath_a", cooldown, (volume, delay)));
        }

        [Fact]
        public void TheLimitsThemselvesAreAccepted()
        {
            const string yaml = "events:\n  PlayerDeath:\n    - a.wav\n";
            string edited = Edit(yaml, Tweak(yaml, "PlayerDeath_a", 3600f, (10f, 120f)));
            SoundChoice entry = Entry(edited, "PlayerDeath_a");
            Assert.Equal(new[] { 10f, 120f, 3600f }, new[] { entry.Sounds[0].Volume, entry.Sounds[0].Delay, entry.Cooldown });
        }

        [Fact]
        public void AFileWithASyntaxErrorIsNeverTouched()
        {
            const string yaml = "events:\n  PlayerDeath:\n    - a.wav\n   - b.wav\n";
            Assert.Contains("fix that first", Fail(yaml, new EntryTweak { ChoiceId = "PlayerDeath_a", Cooldown = 1f }));
        }

        // --- layout preserved ---------------------------------------------------------------------

        [Fact]
        public void WindowsLineEndingsAreKept()
        {
            const string yaml = "events:\r\n  PlayerDeath:\r\n    - a.wav\r\n    - file: b.wav\r\n      volume: 0.5\r\n";

            string edited = Edit(yaml, Tweak(yaml, "PlayerDeath_a", 2f, (0.3f, null)));
            Assert.Equal("events:\r\n  PlayerDeath:\r\n    - file: a.wav\r\n      volume: 0.3\r\n      cooldown: 2\r\n    - file: b.wav\r\n      volume: 0.5\r\n", edited);

            string replaced = Edit(yaml, Tweak(yaml, "PlayerDeath_b", null, (0.9f, null)));
            Assert.Equal("events:\r\n  PlayerDeath:\r\n    - a.wav\r\n    - file: b.wav\r\n      volume: 0.9\r\n", replaced);
        }

        [Fact]
        public void EverythingElseInTheFileIsLeftExactlyAsItWas()
        {
            const string yaml =
                "# my notes\n" +
                "settings:\n" +
                "  debug: true      # keep\n" +
                "events:\n" +
                "  PlayerDeath:\n" +
                "    # a comment between\n" +
                "    - \"my sound.wav\"   # quoted\n" +
                "    - { file: b.wav, volume: 0.5 }\n" +
                "    - file: c.wav\n" +
                "      enabled: false\n" +
                "\n" +
                "  PlayerJump:\n" +
                "    - d.wav\n";

            string edited = Edit(yaml, Tweak(yaml, "PlayerDeath_c", 7f, (0.3f, 0.5f)));

            // Only the two new lines' worth of difference, all other lines identical and in order.
            string[] want = yaml.Split('\n');
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
            Assert.Equal(want.Length + 3, have.Length);
            Assert.Empty(Parse(edited).Issues);
            Assert.False(Entry(edited, "PlayerDeath_c").Enabled);
        }

        [Fact]
        public void EveryEntryInTheShippedFileCanBeTweaked()
        {
            string yaml = File.ReadAllText(Path.Combine(FindRepoRoot(), "mod", "soundboard.yaml"));
            SoundboardConfig baseline = Parse(yaml);

            foreach (SoundChoice choice in baseline.Events.SelectMany(e => e.Choices))
            {
                (float?, float?)[] numbers = choice.Sounds.Select(_ => ((float?)0.55f, (float?)1.5f)).ToArray();
                string edited = Edit(yaml, Tweak(yaml, choice.Id, 12f, numbers));

                SoundboardConfig after = Parse(edited);
                Assert.Equal(baseline.Issues.Count, after.Issues.Count);
                SoundChoice changed = after.Events.SelectMany(e => e.Choices).Single(c => c.Id == choice.Id);
                Assert.Equal(12f, changed.Cooldown);
                Assert.All(changed.Sounds, s => Assert.Equal(0.55f, s.OwnVolume));
                Assert.Equal(choice.Enabled, changed.Enabled);
            }
        }

        [Fact]
        public void TweakingSeveralEntriesOneAfterAnotherKeepsEachChange()
        {
            string yaml = "events:\n  PlayerDeath:\n    - a.wav\n    - b.wav\n  PlayerJump:\n    - together:\n        - c.wav\n        - d.wav\n";

            yaml = Edit(yaml, Tweak(yaml, "PlayerDeath_a", 1f, (0.1f, null)));
            yaml = Edit(yaml, Tweak(yaml, "PlayerJump_c_d", 2f, (null, null), (0.2f, null)));
            yaml = Edit(yaml, Tweak(yaml, "PlayerDeath_b", 3f, (0.3f, 3f)));

            SoundboardConfig config = Parse(yaml);
            Assert.Empty(config.Issues);
            Assert.Equal(1f, Entry(yaml, "PlayerDeath_a").Cooldown);
            Assert.Equal(0.1f, Entry(yaml, "PlayerDeath_a").Sounds[0].OwnVolume);
            Assert.Equal(2f, Entry(yaml, "PlayerJump_c_d").Cooldown);
            Assert.Equal(0.2f, Entry(yaml, "PlayerJump_c_d").Sounds[1].OwnVolume);
            Assert.Equal(new[] { 3f, 0.3f, 3f }, new[] { Entry(yaml, "PlayerDeath_b").Cooldown, Entry(yaml, "PlayerDeath_b").Sounds[0].OwnVolume, Entry(yaml, "PlayerDeath_b").Sounds[0].OwnDelay });
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
