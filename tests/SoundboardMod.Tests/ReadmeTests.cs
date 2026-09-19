using System;
using System.IO;
using System.Linq;
using Xunit;

namespace SoundboardMod.Tests
{
    /// <summary>The README's event tables are generated from EventCatalog; this catches them drifting apart.</summary>
    public class ReadmeTests
    {
        private static string ReadRepoFile(string relativePath)
        {
            string dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "SoundboardMod.sln")))
            {
                dir = Path.GetDirectoryName(dir);
            }

            Assert.NotNull(dir);
            return File.ReadAllText(Path.Combine(dir, relativePath));
        }

        [Fact]
        public void ReadmeDocumentsEveryBuiltInEventWithItsCurrentDescription()
        {
            string readme = ReadRepoFile("README.md");
            var catalog = new EventCatalog(new string[0]);

            foreach (EventInfo info in catalog.All)
            {
                Assert.Contains("| `" + info.Name + "` | " + info.Description + " |", readme);
            }
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
