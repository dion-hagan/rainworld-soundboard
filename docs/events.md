# Events

[← Back to the README](../README.md)

The tables below describe the built-in events; [creatures nearby](events-creatures-nearby.md),
[water and breathing](events-water-and-breathing.md) and [the Gourmand](events-gourmand.md) have pages of their own
(linked at the [end of this page](#more-events)). The complete list of names, including the per-creature ones,
is on the [full list](events-full-list.md) page, and the mod also writes it to `events.txt` in your
[config folder](where-files-live.md) each time the game starts (that copy includes creatures added by other mods).
The numbers some events mention ('hard-landing-speed' and so on) are [settings](settings.md).

**Every creature type also gets its own three events**, named after the creature the way the game spells it:

- `<Creature>Death` - e.g. `GreenLizardDeath`, `KingVultureDeath`, `BigSpiderDeath`, `EggBugDeath`
- `PlayerSpottedBy<Creature>` - e.g. `PlayerSpottedByRedLizard`, `PlayerSpottedByMirosBird`
- `<Creature>Near` - e.g. `RedLizardNear`, `ScavengerEliteNear`, `DaddyLongLegsNear` (see [Creatures nearby](events-creatures-nearby.md))

Every food you can eat that isn't a creature also gets a `PlayerEat<Food>` event, listed under "Food eaten" below.

Grouped events like `LizardDeath` (any lizard) or `PlayerSpottedByPredator` are in the tables below.
When something matches both, both events fire, so you can give a specific creature its own sound
and still have a generic one for the rest. (Each event's rotation is separate.)

## The player

| Event | Fires when |
|---|---|
| `PlayerDeath` | The slugcat dies. |
| `PlayerJump` | The slugcat jumps. Every single jump, no limit - see PlayerJumpCooldown for a rate-limited version. |
| `PlayerJumpCooldown` | The slugcat jumps, but at most once per 'player-jump-cooldown' seconds (setting, default 2). Good for longer sounds that shouldn't pile up. |
| `PlayerJumpWithCicada` | The slugcat jumps while holding a Cicada ("squidcada"). Fires alongside PlayerJump. |
| `PlayerArtificerPyroJump` | Artificer's explosion-boosted jump, at most once per 'artificer-pyro-jump-cooldown' seconds (setting, default 10). |
| `PlayerHardLanding` | The slugcat lands hard: impact speed above 'hard-landing-speed' (setting, default 30). |
| `PlayerTerminalVelocity` | The slugcat falls at 'terminal-velocity' speed or faster (setting, default 40). Fires once per fall, when the speed is first reached. |
| `PlayerEat` | The slugcat eats anything - fruit, plants or meat. |
| `PlayerEatCreature` | The slugcat eats a creature (meat) rather than fruit or plants. |
| `PlayerGrabExplosive` | The slugcat picks up an explosive spear or a scavenger bomb. |
| `PlayerGrabSlugcat` | The slugcat picks up another slugcat. |
| `PlayerGrabYeek` | The slugcat grabs a Yeek. |
| `PlayerThrowExplosiveSpear` | The slugcat throws an explosive spear. |
| `PlayerBitByLizard` | A lizard's bite lands on the slugcat. |
| `PlayerHitByDartMaggot` | A Spitter Spider's dart maggot sticks into the slugcat. |
| `PlayerRoomTransition` | The slugcat moves from one room into another (through a pipe/shortcut). |
| `PlayerEnterShelter` | The slugcat walks into a shelter, before the door closes (so a long sound has time to play). Fires every time you enter one, even if you leave again without sleeping. |

## Food eaten

Besides `PlayerEat` (anything) and `PlayerEatCreature` (meat), every kind of fruit and plant food has its own
event, named `PlayerEat` plus the food's type name in the game. They fire **in addition to** `PlayerEat`, so a
Blue Fruit plays both `PlayerEat` and `PlayerEatDangleFruit`, and you can give one food its own sound while a
generic `PlayerEat` sound covers the rest. Creatures you eat (batflies, vulture grubs, ...) are not in this
list: they use `PlayerEatCreature`.

"Eaten" means the food is *finished*, the same moment as `PlayerEat`: a Slime Mold or Bubble Fruit takes
several bites but its event fires once, on the last one, not per bite. It also fires when a slugpup eats. The
More Slugcats and Watcher foods are always listed so a shared config works for everyone; they just never fire
if that content is off.

| Event | Fires when |
|---|---|
| `PlayerEatDangleFruit` | The slugcat eats a Blue Fruit (dangle fruit). |
| `PlayerEatSlimeMold` | The slugcat eats a Slime Mold. |
| `PlayerEatMushroom` | The slugcat eats a Mushroom. |
| `PlayerEatWaterNut` | The slugcat eats a Bubble Fruit (the game calls it a water nut, or swollen water nut). |
| `PlayerEatJellyFish` | The slugcat eats a Jellyfish. |
| `PlayerEatKarmaFlower` | The slugcat eats a Karma Flower. |
| `PlayerEatEggBugEgg` | The slugcat eats an Eggbug egg. |
| `PlayerEatSSOracleSwarmer` | The slugcat eats a Neuron Fly (the ordinary kind, from around Five Pebbles). |
| `PlayerEatSLOracleSwarmer` | The slugcat eats one of Looks to the Moon's neuron flies (the kind that makes you glow). |
| `PlayerEatDandelionPeach` | The slugcat eats a Dandelion Peach. |
| `PlayerEatFireEgg` | The slugcat eats a Fire Egg. |
| `PlayerEatGlowWeed` | The slugcat eats a Glow Weed. |
| `PlayerEatGooieDuck` | The slugcat eats a Gooieduck. |
| `PlayerEatLillyPuck` | The slugcat eats a Lilypuck. |
| `PlayerEatFireSpriteLarva` | The slugcat eats a Box Worm larva (the game calls it a Fire Sprite larva). |

## Movement techs

The game has no "tech happened" callback, so these are worked out from what the slugcat is doing at the moment of the jump. They fire once per move, and (except `PlayerWallJump`) alongside `PlayerJump`, so you can give the plain jump one sound and a tech another. A slide has no "ended" event; use `PlayerSlidePounce` / `PlayerSlideFlip` for jumping out of one.

| Event | Fires when |
|---|---|
| `PlayerSlide` | The slugcat starts a belly slide (while crawling, jump with down and a direction held). Fires alongside PlayerJump, because the game starts a slide from the jump. |
| `PlayerSlidePounce` | The slugcat jumps out of a belly slide, launching forward in a rocket-style pounce. Fires alongside PlayerJump. Jumping backwards out of a slide (PlayerSlideFlip) or out of a roll (PlayerRollPounce) are separate events. |
| `PlayerSlideFlip` | The slugcat jumps backwards out of a belly slide (a whiplash flip: you held the opposite direction to the slide). Fires alongside PlayerJump. |
| `PlayerRollPounce` | The slugcat jumps out of a roll (the tumble after a fast, diagonal-down landing), launching forward like a pounce. Fires alongside PlayerJump. |
| `PlayerWallJump` | The slugcat kicks off a wall sideways, including from hanging on a ledge. Not the plain hop you get with a floor under you. The game doesn't always count these as a jump, so this can fire without PlayerJump. |
| `PlayerBackflip` | The slugcat backflips: jumps within the first moments of the skid you get from reversing direction at a run. Fires alongside PlayerJump. |
| `PlayerSuperJump` | The slugcat does a fully charged crouch super jump (crouch still, hold jump until charged, release). Fires alongside PlayerJump. |
| `RivuletJump` | Rivulet jumps: every jump, the same moments as PlayerJump. Only for the Rivulet character (More Slugcats), not the Expedition agility perk. |
| `RivuletSlide` | Rivulet starts a belly slide. Fires alongside PlayerSlide and PlayerJump. |
| `RivuletSlidePounce` | Rivulet jumps out of a belly slide. Fires alongside PlayerSlidePounce and PlayerJump. |

The three `Rivulet...` events fire **in addition to** the generic ones, only for the Rivulet character (they need More Slugcats), the same way `PlayerJumpWithCicada` fires alongside `PlayerJump`. Rivulet also gets `PlayerSlide`, `PlayerSlidePounce` and the other generic events.

## The world

| Event | Fires when |
|---|---|
| `RegionGateTransition` | A region gate starts carrying you into the next region. |
| `CreatureEnteredOccupiedShelter` | Any creature walks into a shelter that already has a player in it. |
| `FatalRainImminent` | The fatal rain is one minute away: the cycle's rain timer reaches 60 seconds. Once per cycle, even if you're already in a shelter. Never in The Rot (More Slugcats), where the rain doesn't hit. |
| `SnailExplosion` | A snail pops (its stunning shockwave). |
| `VultureGrubSignal` | A thrown vulture grub starts calling for vultures. |
| `FlareBombThrown` | A flashbang is thrown by anyone. |
| `CyanLizardJump` | A Cyan Lizard leaps. |
| `ScavengerThrowSpear` | A scavenger throws a spear. |

## Creatures dying

| Event | Fires when |
|---|---|
| `ScavengerDeath` | Any scavenger dies (every variant). For one variant use e.g. ScavengerEliteDeath. |
| `LizardDeath` | Any lizard dies (all colours). For one colour use e.g. RedLizardDeath. |
| `SpiderDeath` | A Spider or any Big Spider variant dies. |
| `CicadaOrLanternMouseDeath` | A cicada or lantern mouse dies. |

## The player being spotted

| Event | Fires when |
|---|---|
| `PlayerSpottedByPredator` | A lizard, spider or vulture notices you - except scavengers, Cyan Lizards, Miros and the 'major threats' below, which have their own events. Once per creature per 'spotted-cooldown' seconds (setting, default 10). |
| `PlayerSpottedByScavenger` | Any scavenger (every variant) notices you. For one variant use e.g. PlayerSpottedByScavengerElite. |
| `PlayerSpottedByMajorThreat` | A Red Lizard, Red Centipede, King Vulture or Daddy Long Legs notices you. |
| `PlayerSpottedByMiros` | A Miros Bird or Miros Vulture notices you. |

## More events

These have longer explanations, so they get a page each:

- [Creatures nearby](events-creatures-nearby.md) - `<Creature>Near`: a creature of any type coming close to you.
- [Water and breathing](events-water-and-breathing.md) - `PlayerSwimUnderwater`, `PlayerDrowning`, `PlayerDrowned`.
- [The Gourmand](events-gourmand.md) - `GourmandSlideHit`, `GourmandDropHit`, `GourmandRollHit`.
- [Full list of every event name](events-full-list.md), including all the per-creature events.
