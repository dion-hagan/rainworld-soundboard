# Full list of every event name

[← Back to the README](../README.md) · [Events](events.md)

Every event you can put under `events:` in `soundboard.yaml`, as of Rain World v1.11.8 with the
More Slugcats and Watcher creatures. Creatures added by other mods get the same three events
automatically; the list the mod writes to `events.txt` always includes them.

**Built-in events (64)** - described on the [events page](events.md) and the pages it links to:

```
PlayerDeath
PlayerJump
PlayerJumpCooldown
PlayerJumpWithCicada
PlayerArtificerPyroJump
PlayerHardLanding
PlayerTerminalVelocity
PlayerEat
PlayerEatCreature
PlayerGrabExplosive
PlayerGrabSlugcat
PlayerGrabYeek
PlayerThrowSpear
PlayerThrowExplosiveSpear
PlayerBitByLizard
PlayerHitByDartMaggot
PlayerRoomTransition
PlayerEnterShelter
PlayerSlide
PlayerSlidePounce
PlayerSlideFlip
PlayerRollPounce
PlayerWallJump
PlayerBackflip
PlayerSuperJump
RivuletJump
RivuletSlide
RivuletSlidePounce
RegionGateTransition
CreatureEnteredOccupiedShelter
FatalRainImminent
SnailExplosion
VultureGrubSignal
FlareBombThrown
CyanLizardJump
ScavengerThrowSpear
ScavengerDeath
LizardDeath
SpiderDeath
CicadaOrLanternMouseDeath
PlayerSpottedByPredator
PlayerSpottedByScavenger
PlayerSpottedByMajorThreat
PlayerSpottedByMiros
PlayerSwimUnderwater
PlayerDrowning
PlayerDrowned
GourmandSlideHit
GourmandDropHit
GourmandRollHit
PlayerEatDangleFruit
PlayerEatSlimeMold
PlayerEatMushroom
PlayerEatWaterNut
PlayerEatJellyFish
PlayerEatKarmaFlower
PlayerEatEggBugEgg
PlayerEatSSOracleSwarmer
PlayerEatSLOracleSwarmer
PlayerEatDandelionPeach
PlayerEatFireEgg
PlayerEatGlowWeed
PlayerEatGooieDuck
PlayerEatLillyPuck
PlayerEatFireSpriteLarva
```

**Per-creature events (88 creature types)** - one "dies", one "notices you" and one "comes near" event each:

| Creature | Dies | Notices you | Comes near |
|---|---|---|---|
| Angler | `AnglerDeath` | `PlayerSpottedByAngler` | `AnglerNear` |
| AquaCenti | `AquaCentiDeath` | `PlayerSpottedByAquaCenti` | `AquaCentiNear` |
| Barnacle | `BarnacleDeath` | `PlayerSpottedByBarnacle` | `BarnacleNear` |
| BasiliskLizard | `BasiliskLizardDeath` | `PlayerSpottedByBasiliskLizard` | `BasiliskLizardNear` |
| BigEel | `BigEelDeath` | `PlayerSpottedByBigEel` | `BigEelNear` |
| BigJelly | `BigJellyDeath` | `PlayerSpottedByBigJelly` | `BigJellyNear` |
| BigMoth | `BigMothDeath` | `PlayerSpottedByBigMoth` | `BigMothNear` |
| BigNeedleWorm | `BigNeedleWormDeath` | `PlayerSpottedByBigNeedleWorm` | `BigNeedleWormNear` |
| BigSandGrub | `BigSandGrubDeath` | `PlayerSpottedByBigSandGrub` | `BigSandGrubNear` |
| BigSpider | `BigSpiderDeath` | `PlayerSpottedByBigSpider` | `BigSpiderNear` |
| BlackLizard | `BlackLizardDeath` | `PlayerSpottedByBlackLizard` | `BlackLizardNear` |
| BlizzardLizard | `BlizzardLizardDeath` | `PlayerSpottedByBlizzardLizard` | `BlizzardLizardNear` |
| BlueLizard | `BlueLizardDeath` | `PlayerSpottedByBlueLizard` | `BlueLizardNear` |
| BoxWorm | `BoxWormDeath` | `PlayerSpottedByBoxWorm` | `BoxWormNear` |
| BrotherLongLegs | `BrotherLongLegsDeath` | `PlayerSpottedByBrotherLongLegs` | `BrotherLongLegsNear` |
| Centipede | `CentipedeDeath` | `PlayerSpottedByCentipede` | `CentipedeNear` |
| Centiwing | `CentiwingDeath` | `PlayerSpottedByCentiwing` | `CentiwingNear` |
| CicadaA | `CicadaADeath` | `PlayerSpottedByCicadaA` | `CicadaANear` |
| CicadaB | `CicadaBDeath` | `PlayerSpottedByCicadaB` | `CicadaBNear` |
| CyanLizard | `CyanLizardDeath` | `PlayerSpottedByCyanLizard` | `CyanLizardNear` |
| DaddyLongLegs | `DaddyLongLegsDeath` | `PlayerSpottedByDaddyLongLegs` | `DaddyLongLegsNear` |
| Deer | `DeerDeath` | `PlayerSpottedByDeer` | `DeerNear` |
| DrillCrab | `DrillCrabDeath` | `PlayerSpottedByDrillCrab` | `DrillCrabNear` |
| DropBug | `DropBugDeath` | `PlayerSpottedByDropBug` | `DropBugNear` |
| EelLizard | `EelLizardDeath` | `PlayerSpottedByEelLizard` | `EelLizardNear` |
| EggBug | `EggBugDeath` | `PlayerSpottedByEggBug` | `EggBugNear` |
| FireBug | `FireBugDeath` | `PlayerSpottedByFireBug` | `FireBugNear` |
| FireSprite | `FireSpriteDeath` | `PlayerSpottedByFireSprite` | `FireSpriteNear` |
| Fly | `FlyDeath` | `PlayerSpottedByFly` | `FlyNear` |
| Frog | `FrogDeath` | `PlayerSpottedByFrog` | `FrogNear` |
| GarbageWorm | `GarbageWormDeath` | `PlayerSpottedByGarbageWorm` | `GarbageWormNear` |
| GrappleSnake | `GrappleSnakeDeath` | `PlayerSpottedByGrappleSnake` | `GrappleSnakeNear` |
| GreenLizard | `GreenLizardDeath` | `PlayerSpottedByGreenLizard` | `GreenLizardNear` |
| Hazer | `HazerDeath` | `PlayerSpottedByHazer` | `HazerNear` |
| HunterDaddy | `HunterDaddyDeath` | `PlayerSpottedByHunterDaddy` | `HunterDaddyNear` |
| IndigoLizard | `IndigoLizardDeath` | `PlayerSpottedByIndigoLizard` | `IndigoLizardNear` |
| Inspector | `InspectorDeath` | `PlayerSpottedByInspector` | `InspectorNear` |
| JetFish | `JetFishDeath` | `PlayerSpottedByJetFish` | `JetFishNear` |
| JungleLeech | `JungleLeechDeath` | `PlayerSpottedByJungleLeech` | `JungleLeechNear` |
| KingVulture | `KingVultureDeath` | `PlayerSpottedByKingVulture` | `KingVultureNear` |
| LanternMouse | `LanternMouseDeath` | `PlayerSpottedByLanternMouse` | `LanternMouseNear` |
| Leech | `LeechDeath` | `PlayerSpottedByLeech` | `LeechNear` |
| Loach | `LoachDeath` | `PlayerSpottedByLoach` | `LoachNear` |
| Millipede | `MillipedeDeath` | `PlayerSpottedByMillipede` | `MillipedeNear` |
| MirosBird | `MirosBirdDeath` | `PlayerSpottedByMirosBird` | `MirosBirdNear` |
| MirosVulture | `MirosVultureDeath` | `PlayerSpottedByMirosVulture` | `MirosVultureNear` |
| MotherSpider | `MotherSpiderDeath` | `PlayerSpottedByMotherSpider` | `MotherSpiderNear` |
| MothGrub | `MothGrubDeath` | `PlayerSpottedByMothGrub` | `MothGrubNear` |
| Overseer | `OverseerDeath` | `PlayerSpottedByOverseer` | `OverseerNear` |
| PeachLizard | `PeachLizardDeath` | `PlayerSpottedByPeachLizard` | `PeachLizardNear` |
| PinkLizard | `PinkLizardDeath` | `PlayerSpottedByPinkLizard` | `PinkLizardNear` |
| PoleMimic | `PoleMimicDeath` | `PlayerSpottedByPoleMimic` | `PoleMimicNear` |
| Rat | `RatDeath` | `PlayerSpottedByRat` | `RatNear` |
| Rattler | `RattlerDeath` | `PlayerSpottedByRattler` | `RattlerNear` |
| RedCentipede | `RedCentipedeDeath` | `PlayerSpottedByRedCentipede` | `RedCentipedeNear` |
| RedLizard | `RedLizardDeath` | `PlayerSpottedByRedLizard` | `RedLizardNear` |
| RippleSpider | `RippleSpiderDeath` | `PlayerSpottedByRippleSpider` | `RippleSpiderNear` |
| RotLoach | `RotLoachDeath` | `PlayerSpottedByRotLoach` | `RotLoachNear` |
| Salamander | `SalamanderDeath` | `PlayerSpottedBySalamander` | `SalamanderNear` |
| SandGrub | `SandGrubDeath` | `PlayerSpottedBySandGrub` | `SandGrubNear` |
| Scavenger | `ScavengerDeath` | `PlayerSpottedByScavenger` | `ScavengerNear` |
| ScavengerDisciple | `ScavengerDiscipleDeath` | `PlayerSpottedByScavengerDisciple` | `ScavengerDiscipleNear` |
| ScavengerElite | `ScavengerEliteDeath` | `PlayerSpottedByScavengerElite` | `ScavengerEliteNear` |
| ScavengerKing | `ScavengerKingDeath` | `PlayerSpottedByScavengerKing` | `ScavengerKingNear` |
| ScavengerTemplar | `ScavengerTemplarDeath` | `PlayerSpottedByScavengerTemplar` | `ScavengerTemplarNear` |
| SeaLeech | `SeaLeechDeath` | `PlayerSpottedBySeaLeech` | `SeaLeechNear` |
| SkyWhale | `SkyWhaleDeath` | `PlayerSpottedBySkyWhale` | `SkyWhaleNear` |
| SmallCentipede | `SmallCentipedeDeath` | `PlayerSpottedBySmallCentipede` | `SmallCentipedeNear` |
| SmallMoth | `SmallMothDeath` | `PlayerSpottedBySmallMoth` | `SmallMothNear` |
| SmallNeedleWorm | `SmallNeedleWormDeath` | `PlayerSpottedBySmallNeedleWorm` | `SmallNeedleWormNear` |
| Snail | `SnailDeath` | `PlayerSpottedBySnail` | `SnailNear` |
| Spider | `SpiderDeath` | `PlayerSpottedBySpider` | `SpiderNear` |
| SpitLizard | `SpitLizardDeath` | `PlayerSpottedBySpitLizard` | `SpitLizardNear` |
| SpitterSpider | `SpitterSpiderDeath` | `PlayerSpottedBySpitterSpider` | `SpitterSpiderNear` |
| StowawayBug | `StowawayBugDeath` | `PlayerSpottedByStowawayBug` | `StowawayBugNear` |
| Tardigrade | `TardigradeDeath` | `PlayerSpottedByTardigrade` | `TardigradeNear` |
| TempleGuard | `TempleGuardDeath` | `PlayerSpottedByTempleGuard` | `TempleGuardNear` |
| TentaclePlant | `TentaclePlantDeath` | `PlayerSpottedByTentaclePlant` | `TentaclePlantNear` |
| TerrorLongLegs | `TerrorLongLegsDeath` | `PlayerSpottedByTerrorLongLegs` | `TerrorLongLegsNear` |
| TowerCrab | `TowerCrabDeath` | `PlayerSpottedByTowerCrab` | `TowerCrabNear` |
| TrainLizard | `TrainLizardDeath` | `PlayerSpottedByTrainLizard` | `TrainLizardNear` |
| TubeWorm | `TubeWormDeath` | `PlayerSpottedByTubeWorm` | `TubeWormNear` |
| Vulture | `VultureDeath` | `PlayerSpottedByVulture` | `VultureNear` |
| VultureGrub | `VultureGrubDeath` | `PlayerSpottedByVultureGrub` | `VultureGrubNear` |
| WhiteLizard | `WhiteLizardDeath` | `PlayerSpottedByWhiteLizard` | `WhiteLizardNear` |
| Yeek | `YeekDeath` | `PlayerSpottedByYeek` | `YeekNear` |
| YellowLizard | `YellowLizardDeath` | `PlayerSpottedByYellowLizard` | `YellowLizardNear` |
| ZoopLizard | `ZoopLizardDeath` | `PlayerSpottedByZoopLizard` | `ZoopLizardNear` |

Notes: `ScavengerDeath` / `PlayerSpottedByScavenger` cover every scavenger variant, and `SpiderDeath`
covers Spiders and all Big Spider variants (see the [events page](events.md)). Slugcats (including slugpups) use the
`Player...` events instead. Some creatures never notice anything (they have no senses), so their
"notices you" event will simply never fire.

