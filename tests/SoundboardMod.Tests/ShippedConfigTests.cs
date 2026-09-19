using System;
using System.IO;
using System.Linq;
using Xunit;

namespace SoundboardMod.Tests
{
    /// <summary>The soundboard.yaml and sounds/ that ship in mod/ must load cleanly - this is what every new player starts with.</summary>
    public class ShippedConfigTests
    {
        private static string ModFolder()
        {
            string dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "SoundboardMod.sln")))
            {
                dir = Path.GetDirectoryName(dir);
            }

            Assert.NotNull(dir);
            return Path.Combine(dir, "mod");
        }

        private static SoundboardConfig LoadShipped(string modFolder)
        {
            // The real game supplies every creature type; the ones the shipped file names are enough here.
            var catalog = new EventCatalog(new[] { "Scavenger", "CyanLizard" });
            string yaml = File.ReadAllText(Path.Combine(modFolder, "soundboard.yaml"));
            SoundboardConfig config = SoundboardConfigParser.Parse(yaml, catalog);
            SoundFileResolver.Resolve(config, new[] { Path.Combine(modFolder, "sounds") });
            return config;
        }

        [Fact]
        public void LoadsWithoutAnyProblems()
        {
            SoundboardConfig config = LoadShipped(ModFolder());
            Assert.False(config.Failed);
            Assert.Empty(config.Issues.Select(i => i.ToString()));
        }

        [Fact]
        public void ContainsTheOriginalSoundSet()
        {
            SoundboardConfig config = LoadShipped(ModFolder());
            // The 0.1.0 set was 29 events / 53 entries / 54 sounds; it may only grow from there.
            Assert.True(config.Events.Count >= 29, "events: " + config.Events.Count);
            Assert.True(config.Events.Sum(e => e.Choices.Count) >= 53);
            Assert.True(config.SoundCount >= 54);
        }

        [Fact]
        public void EveryChoiceHasAUniqueSettingKey()
        {
            SoundboardConfig config = LoadShipped(ModFolder());
            string[] ids = config.Events.SelectMany(e => e.Choices).Select(c => c.Id).ToArray();
            Assert.Equal(ids.Length, ids.Distinct().Count());
        }
    }
}
