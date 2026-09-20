using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SoundboardMod.Tests
{
    /// <summary>The search in the Add/Edit Sound dropdowns: which entries match what was typed, and in what order.</summary>
    public class DropdownFilterTests
    {
        private static readonly string[] Events =
        {
            "CreatureDeath",
            "PlayerDeath",
            "PlayerDeathByFall",
            "PlayerJump",
            "RedLizardNear",
            "RegionEntered",
            "UnderPlayerDeath",
            "Death",
        };

        private static string[] Search(string[] texts, string query)
        {
            return DropdownFilter.Filter(texts, query).Select(i => texts[i]).ToArray();
        }

        [Fact]
        public void ABlankQueryKeepsEverythingInTheOriginalOrder()
        {
            foreach (string blank in new[] { null, "", "   " })
            {
                Assert.Equal(Enumerable.Range(0, Events.Length), DropdownFilter.Filter(Events, blank));
            }
        }

        [Fact]
        public void MatchesAnywhereInTheNameNotJustAtTheStart()
        {
            Assert.Contains("CreatureDeath", Search(Events, "death"));
            Assert.Contains("UnderPlayerDeath", Search(Events, "death"));
            Assert.Equal(new[] { "RegionEntered" }, Search(Events, "entered"));
        }

        [Fact]
        public void IgnoresCase()
        {
            Assert.Equal(Search(Events, "playerjump"), Search(Events, "PLAYERJUMP"));
            Assert.Equal(new[] { "PlayerJump" }, Search(Events, "pLaYeRjUmP"));
        }

        [Fact]
        public void SpacesBetweenWordsStillFindACamelCaseName()
        {
            Assert.Equal(new[] { "PlayerDeath", "PlayerDeathByFall", "UnderPlayerDeath" }, Search(Events, "player death"));
            Assert.Equal(new[] { "RedLizardNear" }, Search(Events, "red lizard"));
        }

        [Fact]
        public void WordsCanComeInAnyOrder()
        {
            Assert.Equal(Search(Events, "player death"), Search(Events, "death player"));
        }

        [Fact]
        public void PunctuationInTheQueryOrTheNameDoesNotMatter()
        {
            string[] files = { "boom_1.wav", "boom-2.wav", "splash.ogg", "player death.mp3" };

            Assert.Equal(new[] { "boom_1.wav" }, Search(files, "boom1"));
            Assert.Equal(new[] { "boom_1.wav" }, Search(files, "boom_1"));
            Assert.Equal(new[] { "boom-2.wav" }, Search(files, "boom 2"));
            Assert.Equal(new[] { "player death.mp3" }, Search(files, "playerdeath"));
            Assert.Equal(new[] { "boom_1.wav", "boom-2.wav" }, Search(files, "wav"));
        }

        [Fact]
        public void APunctuationOnlyQueryFallsBackToTheTextAsWritten()
        {
            string[] files = { "boom_1.wav", "splash", "a.b" };

            Assert.Equal(new[] { "boom_1.wav", "a.b" }, Search(files, "."));
            Assert.Equal(new[] { "boom_1.wav" }, Search(files, "_"));
        }

        [Fact]
        public void LettersMerelyAppearingInOrderDoNotMatch()
        {
            // Remix's own search would let "pdj" find PlayerJump (p...d...j); this one wants the text itself.
            Assert.Empty(Search(Events, "pdj"));
            Assert.Empty(Search(Events, "zzz"));
        }

        [Fact]
        public void NothingMatchesWhenOneOfTheWordsIsMissing()
        {
            Assert.Empty(Search(Events, "player lizard"));
        }

        [Fact]
        public void APrefixMatchComesBeforeAMatchInTheMiddle()
        {
            // Alphabetically CreatureDeath and Death would come first; the ones that start with "death" should lead.
            string[] result = Search(Events, "death");

            Assert.Equal("Death", result[0]);
        }

        [Fact]
        public void TheStartOfACamelCasePartBeatsTheMiddleOfOne()
        {
            string[] texts = { "Outdoor", "PlayerDoor", "Hardooze" };

            // "do": PlayerDoor has it at a word start, Outdoor and Hardooze only inside a word
            Assert.Equal(new[] { "PlayerDoor", "Outdoor", "Hardooze" }, Search(texts, "do"));
        }

        [Fact]
        public void APrefixMatchBeatsAWordStartMatch()
        {
            string[] texts = { "PlayerJump", "JumpPlayer", "AJump" };

            Assert.Equal(new[] { "JumpPlayer", "PlayerJump", "AJump" }, Search(texts, "jump"));
        }

        [Fact]
        public void AnExactNameBeatsALongerOneThatStartsTheSame()
        {
            string[] texts = { "PlayerDeathByFall", "PlayerDeath" };

            Assert.Equal(new[] { "PlayerDeath", "PlayerDeathByFall" }, Search(texts, "PlayerDeath"));
            Assert.Equal(new[] { "PlayerDeath", "PlayerDeathByFall" }, Search(texts, "player death"));
        }

        [Fact]
        public void EqualMatchesKeepTheListsOwnOrder()
        {
            string[] texts = { "b_kick.wav", "a_kick.wav", "c_kick.wav" };

            Assert.Equal(texts, Search(texts, "kick"));
        }

        [Fact]
        public void AnItemMatchesIfAnyOfItsTextsDoes()
        {
            var filter = new DropdownFilter(new[]
            {
                new[] { "PlayerDeath: Big boom", "PlayerDeath_1" },
                new[] { "PlayerJump: Hop", "PlayerJump_1" },
                new string[0],
                null,
            });

            Assert.Equal(new[] { 0 }, filter.Search("boom"));
            Assert.Equal(new[] { 1 }, filter.Search("jump_1"));
            Assert.Empty(filter.Search("nothing"));
            Assert.Equal(4, filter.Count);
        }

        [Fact]
        public void TheWordsOfAQueryMustAllMatchTheSameText()
        {
            var filter = new DropdownFilter(new[]
            {
                new[] { "Boom", "Jump" },
            });

            Assert.Empty(filter.Search("boom jump"));
        }

        [Fact]
        public void CompactFormAgreesWithNameMatchNormalize()
        {
            // A name found through its Normalize()d form (how the yaml is read) must be found by typing it.
            foreach (string name in Events.Concat(new[] { "boom_1.wav", "Some Sound-2.ogg" }))
            {
                Assert.Contains(name, Search(new[] { name }, NameMatch.Normalize(name)));
            }
        }

        [Fact]
        public void EveryGeneratedEventNameIsFoundByItsNormalizedName()
        {
            var catalog = new EventCatalog(new[] { "RedLizard", "GreenLizard", "Scavenger", "Vulture" });
            string[] names = catalog.All.Select(e => e.Name).ToArray();

            foreach (string name in names)
            {
                Assert.Contains(name, Search(names, NameMatch.Normalize(name)));
            }
        }

        [Fact]
        public void ALongListStaysFast()
        {
            var texts = new List<string>();
            for (int i = 0; i < 5000; i++)
            {
                texts.Add("Creature" + i + "Near_" + (i % 7));
            }

            var filter = new DropdownFilter(texts.Select(t => new[] { t }).ToArray());
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 100; i++)
            {
                filter.Search("creature 12 near");
            }

            // Generous: 100 searches of 5000 entries is a few tens of milliseconds, a keystroke is one.
            Assert.True(watch.ElapsedMilliseconds < 2000, "took " + watch.ElapsedMilliseconds + " ms");
        }
    }
}
