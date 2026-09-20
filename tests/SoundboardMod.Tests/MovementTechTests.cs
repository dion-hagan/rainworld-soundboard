using System.Linq;
using Xunit;

namespace SoundboardMod.Tests
{
    /// <summary>
    /// The before/after tables mirror the branches of the decompiled Player.Jump()
    /// (Rain World 1.11.8); see MovementTech for which branch each row is.
    /// </summary>
    public class MovementTechTests
    {
        private static JumpSnapshot S(MoveAnim anim, int slideCounter = 0, bool standing = false, int superLaunchJump = 0)
        {
            return new JumpSnapshot(anim, slideCounter, standing, superLaunchJump);
        }

        [Fact]
        public void CrawlingIntoASlideIsASlideStart()
        {
            // Player.Jump(): DownOnFours + down-diagonal -> animation = BellySlide, standing = false, return.
            Assert.Equal(MoveTech.SlideStart, MovementTech.Classify(S(MoveAnim.Other), S(MoveAnim.BellySlide)));
        }

        [Fact]
        public void JumpingOutOfASlideForwardsIsAPounce()
        {
            // BellySlide -> RocketJump (rocketJumpFromBellySlide = true).
            Assert.Equal(MoveTech.SlidePounce, MovementTech.Classify(S(MoveAnim.BellySlide), S(MoveAnim.RocketJump)));
        }

        [Fact]
        public void JumpingOutOfASlideBackwardsIsAWhiplashFlip()
        {
            // BellySlide with whiplashJump / input opposite the slide -> Flip (flipFromSlide = true).
            Assert.Equal(MoveTech.SlideFlip, MovementTech.Classify(S(MoveAnim.BellySlide), S(MoveAnim.Flip)));
        }

        [Fact]
        public void JumpingOutOfARollIsARollPounce()
        {
            Assert.Equal(MoveTech.RollPounce, MovementTech.Classify(S(MoveAnim.Roll), S(MoveAnim.RocketJump)));
        }

        [Fact]
        public void APounceOutOfARollIsNotASlidePounce()
        {
            Assert.NotEqual(MoveTech.SlidePounce, MovementTech.Classify(S(MoveAnim.Roll), S(MoveAnim.RocketJump)));
            Assert.NotEqual(MoveTech.RollPounce, MovementTech.Classify(S(MoveAnim.BellySlide), S(MoveAnim.RocketJump)));
        }

        [Theory]
        [InlineData(1, true)]
        [InlineData(5, true)]
        [InlineData(9, true)]
        [InlineData(0, false)]
        [InlineData(10, false)]
        [InlineData(20, false)]
        public void BackflipNeedsTheSkidTurnWindow(int slideCounter, bool expected)
        {
            MoveTech tech = MovementTech.Classify(
                S(MoveAnim.Other, slideCounter: slideCounter, standing: true),
                S(MoveAnim.Flip, slideCounter: 0, standing: true));
            Assert.Equal(expected ? MoveTech.Backflip : MoveTech.None, tech);
        }

        [Fact]
        public void AFlipWithoutTheSkidTurnIsNotABackflip()
        {
            // e.g. some other mod sets Flip: no slide counter, so not our tech.
            Assert.Equal(MoveTech.None, MovementTech.Classify(S(MoveAnim.Other, standing: true), S(MoveAnim.Flip, standing: true)));
        }

        [Fact]
        public void ABackflipRequiresBeingUpright()
        {
            Assert.Equal(MoveTech.None, MovementTech.Classify(S(MoveAnim.Other, slideCounter: 4, standing: false), S(MoveAnim.Flip)));
        }

        [Fact]
        public void ChargedCrouchJumpIsASuperJump()
        {
            // Jump() resets superLaunchJump to 0 in exactly this branch.
            Assert.Equal(MoveTech.SuperJump, MovementTech.Classify(S(MoveAnim.Other, superLaunchJump: 20), S(MoveAnim.Other, superLaunchJump: 0)));
        }

        [Fact]
        public void AnUnchargedCrouchJumpIsAnOrdinaryJump()
        {
            Assert.Equal(MoveTech.None, MovementTech.Classify(S(MoveAnim.Other, superLaunchJump: 19), S(MoveAnim.Other, superLaunchJump: 0)));
        }

        [Fact]
        public void ASlideStartWithAFullChargeIsStillOnlyASlideStart()
        {
            // The slide branch returns before touching superLaunchJump.
            Assert.Equal(MoveTech.SlideStart, MovementTech.Classify(S(MoveAnim.Other, superLaunchJump: 20), S(MoveAnim.BellySlide, superLaunchJump: 20)));
        }

        [Fact]
        public void AnOrdinaryJumpIsNoTech()
        {
            Assert.Equal(MoveTech.None, MovementTech.Classify(S(MoveAnim.Other, standing: true), S(MoveAnim.Other, standing: true)));
            Assert.Equal(MoveTech.None, MovementTech.Classify(S(MoveAnim.Flip), S(MoveAnim.Other)));
        }

        [Fact]
        public void EveryTechHasADistinctEventNameAndOrdinaryJumpsHaveNone()
        {
            var techs = new[] { MoveTech.SlideStart, MoveTech.SlidePounce, MoveTech.SlideFlip, MoveTech.RollPounce, MoveTech.Backflip, MoveTech.SuperJump };
            string[] names = techs.Select(MovementTech.EventName).ToArray();
            Assert.All(names, Assert.NotNull);
            Assert.Equal(names.Length, names.Distinct().Count());
            Assert.Null(MovementTech.EventName(MoveTech.None));
        }

        [Fact]
        public void EveryEventNameThisFeatureFiresIsInTheCatalog()
        {
            var catalog = new EventCatalog(new string[0]);
            var fired = new[] { MoveTech.SlideStart, MoveTech.SlidePounce, MoveTech.SlideFlip, MoveTech.RollPounce, MoveTech.Backflip, MoveTech.SuperJump }
                .SelectMany(t => new[] { MovementTech.EventName(t), MovementTech.RivuletEventName(t) })
                .Where(n => n != null)
                .Concat(new[] { "PlayerWallJump", "RivuletJump" });

            Assert.All(fired, name => Assert.Equal(name, catalog.Resolve(name)));
        }

        [Fact]
        public void RivuletOnlyTwinsExistForSlideAndPounce()
        {
            Assert.Equal("RivuletSlide", MovementTech.RivuletEventName(MoveTech.SlideStart));
            Assert.Equal("RivuletSlidePounce", MovementTech.RivuletEventName(MoveTech.SlidePounce));
            Assert.Null(MovementTech.RivuletEventName(MoveTech.Backflip));
            Assert.Null(MovementTech.RivuletEventName(MoveTech.None));
        }

        [Fact]
        public void MovementEventsAreTheirOwnCatalogSection()
        {
            var catalog = new EventCatalog(new string[0]);
            Assert.Contains(catalog.All, e => e.Name == "PlayerSlide" && e.Section == EventCatalog.SectionMovement);
            Assert.Contains(catalog.All, e => e.Name == "RivuletSlidePounce" && e.Section == EventCatalog.SectionMovement);
        }

        [Theory]
        [InlineData(false, false, false, true)]   // in the air against a wall: a real kick
        [InlineData(true, false, false, false)]   // standing on a floor: the game plays the normal jump sound instead
        [InlineData(false, true, false, false)]   // in water
        [InlineData(false, false, true, false)]   // climbing over a ledge top
        public void OnlyAKickOffAWallIsAWallJump(bool floorBelow, bool inWater, bool ledgeHop, bool expected)
        {
            Assert.Equal(expected, MovementTech.IsWallKick(floorBelow, inWater, ledgeHop));
        }
    }
}
