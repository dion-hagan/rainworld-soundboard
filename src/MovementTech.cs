namespace SoundboardMod
{
    /// <summary>The handful of Player.AnimationIndex values the movement techs care about; everything else is Other.</summary>
    public enum MoveAnim
    {
        Other,
        BellySlide,
        Roll,
        RocketJump,
        Flip,
    }

    /// <summary>The bits of Player state that tell one jump from another, copied out so the decision can be tested without the game.</summary>
    public struct JumpSnapshot
    {
        public MoveAnim Anim;

        /// <summary>Player.slideCounter: 0 normally, counts 1..20 through the skid you get from reversing direction at a run.</summary>
        public int SlideCounter;

        /// <summary>Player.standing (upright, as opposed to crouched or crawling).</summary>
        public bool Standing;

        /// <summary>Player.superLaunchJump: the crouch-and-hold-jump charge, 0..20 (20 = fully charged).</summary>
        public int SuperLaunchJump;

        public JumpSnapshot(MoveAnim anim, int slideCounter, bool standing, int superLaunchJump)
        {
            Anim = anim;
            SlideCounter = slideCounter;
            Standing = standing;
            SuperLaunchJump = superLaunchJump;
        }
    }

    /// <summary>The movement techs that Player.Jump() can start. A single call to Jump() is exactly one of these (or None for an ordinary jump).</summary>
    public enum MoveTech
    {
        None,
        SlideStart,
        SlidePounce,
        SlideFlip,
        RollPounce,
        Backflip,
        SuperJump,
    }

    /// <summary>
    /// Works out which movement tech a call to Player.Jump() was, by comparing
    /// the player just before and just after the call. Jump() is one big
    /// if/else chain on the player's state, and every tech below ends the chain
    /// in a state that no other branch produces, so "before + after" identifies
    /// the branch that ran. The decompiled branches (Rain World 1.11.8):
    ///
    ///   animation == BellySlide  -> Flip (whiplash: whiplashJump or input.x == -rollDirection)
    ///                               or RocketJump (the pounce)          [both certain]
    ///   animation == Roll        -> RocketJump                          [certain]
    ///   animation == DownOnFours (+ down-diagonal input) -> BellySlide  [certain: the ONLY
    ///                               place in the game that starts a belly slide]
    ///   standing && 0 &lt; slideCounter &lt; 10 -> Flip (backflip out of a skid turn)
    ///                               [certain; Flip is set nowhere else in Jump()]
    ///   !standing && superLaunchJump >= 20 -> super jump, resets superLaunchJump to 0
    ///                               [certain; that reset is the only touch of the field in Jump()]
    /// </summary>
    public static class MovementTech
    {
        /// <summary>The window (frames into a skid turn) in which a jump counts as a backflip: Player.Jump() tests slideCounter &gt; 0 &amp;&amp; slideCounter &lt; 10.</summary>
        public const int BackflipWindowMax = 9;

        /// <summary>A fully charged crouch super jump: Player.Jump() tests superLaunchJump &gt;= 20.</summary>
        public const int SuperJumpCharge = 20;

        public static MoveTech Classify(JumpSnapshot before, JumpSnapshot after)
        {
            if (before.Anim != MoveAnim.BellySlide && after.Anim == MoveAnim.BellySlide)
            {
                return MoveTech.SlideStart;
            }

            if (before.Anim == MoveAnim.BellySlide)
            {
                if (after.Anim == MoveAnim.RocketJump)
                {
                    return MoveTech.SlidePounce;
                }

                if (after.Anim == MoveAnim.Flip)
                {
                    return MoveTech.SlideFlip;
                }

                return MoveTech.None;
            }

            if (before.Anim == MoveAnim.Roll && after.Anim == MoveAnim.RocketJump)
            {
                return MoveTech.RollPounce;
            }

            if (after.Anim == MoveAnim.Flip
                && before.Standing
                && before.SlideCounter > 0
                && before.SlideCounter <= BackflipWindowMax)
            {
                return MoveTech.Backflip;
            }

            if (!before.Standing
                && before.SuperLaunchJump >= SuperJumpCharge
                && after.SuperLaunchJump == 0)
            {
                return MoveTech.SuperJump;
            }

            return MoveTech.None;
        }

        /// <summary>The generic event for a tech, or null for an ordinary jump.</summary>
        public static string EventName(MoveTech tech)
        {
            switch (tech)
            {
                case MoveTech.SlideStart: return "PlayerSlide";
                case MoveTech.SlidePounce: return "PlayerSlidePounce";
                case MoveTech.SlideFlip: return "PlayerSlideFlip";
                case MoveTech.RollPounce: return "PlayerRollPounce";
                case MoveTech.Backflip: return "PlayerBackflip";
                case MoveTech.SuperJump: return "PlayerSuperJump";
                default: return null;
            }
        }

        /// <summary>The Rivulet-only twin of a tech's event (fired in addition to the generic one), or null if that tech has none.</summary>
        public static string RivuletEventName(MoveTech tech)
        {
            switch (tech)
            {
                case MoveTech.SlideStart: return "RivuletSlide";
                case MoveTech.SlidePounce: return "RivuletSlidePounce";
                default: return null;
            }
        }

        /// <summary>
        /// Player.WallJump() has two branches: a real kick off a wall (plays the
        /// wall-jump sound, sets jumpStun) and a plain hop when there is floor
        /// under the player, they are in water, or they are climbing over a
        /// ledge (plays the normal jump sound). Only the first is a wall jump.
        /// Arguments mirror the game's own test: floorBelow =
        /// IsTileSolid(1,0,-1) || IsTileSolid(0,0,-1); inWater =
        /// bodyChunks[1].submersion &gt; 0.1; ledgeHop = pushing against a wall
        /// whose top is right at head height.
        /// </summary>
        public static bool IsWallKick(bool floorBelow, bool inWater, bool ledgeHop)
        {
            return !(floorBelow || inWater || ledgeHop);
        }
    }
}
