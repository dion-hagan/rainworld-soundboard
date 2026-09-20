using System;
using System.IO;
using System.Linq;
using Xunit;

namespace SoundboardMod.Tests
{
    /// <summary>The docs' event tables are generated from EventCatalog; this catches them drifting apart.</summary>
    public class DocsTests
    {
        private static string RepoRoot()
        {
            string dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "SoundboardMod.sln")))
            {
                dir = Path.GetDirectoryName(dir);
            }

            Assert.NotNull(dir);
            return dir;
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(RepoRoot(), relativePath));
        }

        /// <summary>The event tables are spread over docs/events.md and the docs/events-*.md pages it links to.</summary>
        private static string ReadEventPages()
        {
            string[] pages = Directory.GetFiles(Path.Combine(RepoRoot(), "docs"), "events*.md");
            Assert.NotEmpty(pages);
            return string.Join("\n", pages.Select(File.ReadAllText));
        }

        [Fact]
        public void EventDocsDescribeEveryBuiltInEventWithItsCurrentDescription()
        {
            string docs = ReadEventPages();
            var catalog = new EventCatalog(new string[0]);

            foreach (EventInfo info in catalog.All)
            {
                Assert.Contains("| `" + info.Name + "` | " + info.Description + " |", docs);
            }
        }

        [Fact]
        public void FullEventListPageNamesEveryBuiltInEvent()
        {
            string page = ReadRepoFile(Path.Combine("docs", "events-full-list.md"));
            var catalog = new EventCatalog(new string[0]);

            foreach (EventInfo info in catalog.All)
            {
                Assert.Contains("\n" + info.Name + "\n", page);
            }
        }

        [Fact]
        public void ReadmeLinksToTheFullEventListInsteadOfListingEveryEvent()
        {
            string readme = ReadRepoFile("README.md");
            Assert.Contains("docs/events-full-list.md", readme);
            Assert.DoesNotContain("| `PlayerDeath` |", readme);
        }

        [Fact]
        public void EveryEventUsedInTheShippedConfigIsKnown()
        {
            string yaml = ReadRepoFile(Path.Combine("mod", "soundboard.yaml"));
            var catalog = new EventCatalog(new[] { "Scavenger", "CyanLizard" });
            SoundboardConfig config = SoundboardConfigParser.Parse(yaml, catalog);
            Assert.All(config.Events, e => Assert.NotNull(catalog.Resolve(e.EventName)));
        }
    }
}
