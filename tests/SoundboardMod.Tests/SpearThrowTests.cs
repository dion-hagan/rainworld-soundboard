using System.Linq;
using Xunit;

namespace SoundboardMod.Tests
{
    public class SpearThrowTests
    {
        private static readonly EventCatalog Catalog = new EventCatalog(new string[0]);

        [Fact]
        public void PlayerThrowSpearIsAPlayerEvent()
        {
            Assert.Equal("PlayerThrowSpear", Catalog.Resolve("player throw spear"));
            Assert.Equal(EventCatalog.SectionPlayer, Catalog.All.Single(e => e.Name == "PlayerThrowSpear").Section);
        }

        [Fact]
        public void TheExplosiveSpearEventIsStillItsOwnEvent()
        {
            Assert.Equal("PlayerThrowExplosiveSpear", Catalog.Resolve("PlayerThrowExplosiveSpear"));
            Assert.NotEqual(Catalog.Resolve("PlayerThrowSpear"), Catalog.Resolve("PlayerThrowExplosiveSpear"));
        }

        [Fact]
        public void AConfigCanAttachASoundToIt()
        {
            SoundboardConfig config = SoundboardConfigParser.Parse("events:\n  PlayerThrowSpear:\n    - file: whoosh.wav\n", Catalog);
            Assert.Empty(config.Issues);
        }
    }
}
