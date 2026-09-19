This is possibly the dumbest thing I've ever written. It's just a simple soundboard mod for me and my friends to play around with
in Rainworld. I couldn't find anything general purpose like this. Feel free to use it/extend it.

Note: This is AI-assisted code -- I reviewed it, but didn't play close attention to architecture.

### Example

https://github.com/user-attachments/assets/29a73c3b-f976-43dd-b9c5-7516e414860d

# Custom Soundboard (Rain World mod)

Plays your own sound effects when things happen in Rain World: you die, you jump, you eat, a
Green Lizard spots you, you fall too fast, you change rooms, a scavenger throws a spear... **One
text file, `soundboard.yaml`, decides which sound plays for which event** - along with how loud it
is and how long to wait before it plays. No coding, no rebuilding, and no game restart to try a change.

*(Versions before 1.0.0 needed a recompile plus two data files per sound. If you have a `0.1.0`
setup, see [Upgrading from 0.1.0](#upgrading-from-010).)*

## Quick start

1. Install the mod and enable **Custom Soundboard** under **Options → Mods**, then restart the game.
   It comes with a set of example sounds so you can hear it working straight away.
2. Open **Options → Mods → Custom Soundboard**. Press **OPEN FOLDER**.
3. In that folder:
   - drop your own `.wav`, `.ogg` or `.mp3` files into the `sounds` folder, and
   - open `soundboard.yaml` in Notepad (or any text editor) and point an event at them.
4. Back in the game, press **RELOAD CONFIG**. Done - jump around and listen.

The options screen also has a checkbox for every sound so you can switch individual ones off
without editing anything, and it lists any problems it found in your file (with line numbers).

> **Where is my config?** In the game's data folder (`%USERPROFILE%\AppData\LocalLow\Videocult\Rain World\Soundboard`
> on Windows), *not* in the mod's own folder - Steam overwrites that whenever the mod updates, which would
> wipe your changes. The first time the game starts it puts a copy of the default `soundboard.yaml`
> there. Delete that copy if you ever want the defaults back.

## Writing `soundboard.yaml`

The file has two sections. `events:` is the important one.

```yaml
settings:
  hard-landing-speed: 30

events:
  PlayerDeath:
    - boom.wav                 # simplest form: just a file name
    - file: sad-trombone.wav   # or with options
      volume: 0.5
      delay: 1.5
```

Under an event name, list what should play. Each list item is a file name, or a block of options:

| Option | What it does | Default |
|---|---|---|
| `file` | The audio file, from your `sounds` folder (`.wav`, `.ogg`, `.mp3`; the extension is optional; sub-folders work: `funny/boom.wav`). | *required* |
| `volume` | `1` = as recorded, `0.5` = half as loud, `2` = twice (0 to 10). | `1` |
| `delay` | Seconds to wait after the event before the sound plays (0 to 120). | `0` |
| `name` | The label shown in the options screen. | made from the file name |
| `description` | A tooltip for the options screen. | what the event is |
| `enabled` | `false` = starts switched off (can be turned on in the options screen). | `true` |

There's also a compact one-line form: `- { file: boom.wav, volume: 0.5, delay: 1 }`.

### Taking turns

If an event has **several items, they take turns**: the first time it happens the first item plays,
the next time the second, and so on around again. So `PlayerDeath` with seven items plays a different
death sound each time. Items switched off in the options screen are skipped.

### Playing sounds together

To make one step of the rotation play **several sounds at once**, use `together`:

```yaml
  PlayerSpottedByScavenger:
    - can-i-put-my-balls-in-your-jaws.wav       # step 1: one sound
    - together:                                 # step 2: both of these at once
        - enrique.wav
        - file: indian-song.wav
          volume: 0.2
      volume: 0.8       # optional: multiplies each sound's volume
      delay: 0.5        # optional: added to each sound's delay
      name: "Enrique + Indian song"
```

### Settings

A few events have numbers you may want to tune. All are optional.

| Setting | Default | Meaning |
|---|---|---|
| `hard-landing-speed` | `30` | Impact speed that counts as a hard landing (`PlayerHardLanding`). |
| `terminal-velocity` | `40` | Fall speed that triggers `PlayerTerminalVelocity`. |
| `player-jump-cooldown` | `2` | Seconds between `PlayerJumpCooldown` sounds. |
| `artificer-pyro-jump-cooldown` | `10` | Seconds between Artificer pyro-jump sounds. |
| `spotted-cooldown` | `10` | Seconds before the same creature can "spot" you again. |
| `debug` | `false` | `true` writes every event that fires to `BepInEx/LogOutput.log` (great for working out why a sound doesn't play, and for finding good speed values). |

Falling in Rain World has no real speed cap, so "terminal velocity" is just a speed *you* pick.
With `debug: true` the log shows the impact speed of every landing, which is the easiest way to
choose a value you like.

### Tips

- Indent with **spaces**, never tabs. Lines that line up belong together.
- Put quotes around text containing a colon or `#`: `name: "Fire: hot"`.
- Write decimals with a dot: `0.5`, not `0,5`.
- Event names are forgiving: `PlayerDeath`, `player death` and `player-death` all work.
- Typos get a hint: *"'PlayerDeth' isn't an event this mod knows... Did you mean 'PlayerDeath'?"*
- If the file has a syntax error the mod falls back to the default config (and tells you so on the
  options screen); if you press RELOAD CONFIG with an error in it, nothing changes.
- Replacing a sound? Save your new file over the old one with the same name and press RELOAD CONFIG.

## Events

The tables below describe every built-in event. The complete list of names,
including the per-creature ones, is in the collapsed section at the end of this chapter, and the mod
also writes it to `events.txt` in your config folder each time the game starts (that copy includes
creatures added by other mods).

**Every creature type also gets its own two events**, named after the creature the way the game spells it:

- `<Creature>Death` - e.g. `GreenLizardDeath`, `KingVultureDeath`, `BigSpiderDeath`, `EggBugDeath`
- `PlayerSpottedBy<Creature>` - e.g. `PlayerSpottedByRedLizard`, `PlayerSpottedByMirosBird`

Grouped events like `LizardDeath` (any lizard) or `PlayerSpottedByPredator` are in the tables below.
When something matches both, both events fire, so you can give a specific creature its own sound
and still have a generic one for the rest. (Each event's rotation is separate.)

### The player

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
| `PlayerEnterShelter` | A shelter door closes with you inside. |

### The world

| Event | Fires when |
|---|---|
| `RegionGateTransition` | A region gate starts carrying you into the next region. |
| `CreatureEnteredOccupiedShelter` | Any creature walks into a shelter that already has a player in it. |
| `SnailExplosion` | A snail pops (its stunning shockwave). |
| `VultureGrubSignal` | A thrown vulture grub starts calling for vultures. |
| `FlareBombThrown` | A flashbang is thrown by anyone. |
| `CyanLizardJump` | A Cyan Lizard leaps. |
| `ScavengerThrowSpear` | A scavenger throws a spear. |

### Creatures dying

| Event | Fires when |
|---|---|
| `ScavengerDeath` | Any scavenger dies (every variant). For one variant use e.g. ScavengerEliteDeath. |
| `LizardDeath` | Any lizard dies (all colours). For one colour use e.g. RedLizardDeath. |
| `SpiderDeath` | A Spider or any Big Spider variant dies. |
| `CicadaOrLanternMouseDeath` | A cicada or lantern mouse dies. |

### The player being spotted

| Event | Fires when |
|---|---|
| `PlayerSpottedByPredator` | A lizard, spider or vulture notices you - except scavengers, Cyan Lizards, Miros and the 'major threats' below, which have their own events. Once per creature per 'spotted-cooldown' seconds (setting, default 10). |
| `PlayerSpottedByScavenger` | Any scavenger (every variant) notices you. For one variant use e.g. PlayerSpottedByScavengerElite. |
| `PlayerSpottedByMajorThreat` | A Red Lizard, Red Centipede, King Vulture or Daddy Long Legs notices you. |
| `PlayerSpottedByMiros` | A Miros Bird or Miros Vulture notices you. |

<details>
<summary><b>Full list of every event name (205)</b> - click to expand</summary>

Every event you can put under `events:` in `soundboard.yaml`, as of Rain World v1.11.8 with the
More Slugcats and Watcher creatures. Creatures added by other mods get the same two events
automatically; the list the mod writes to `events.txt` always includes them.

**Built-in events (32)** - described in the tables above:

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
PlayerThrowExplosiveSpear
PlayerBitByLizard
PlayerHitByDartMaggot
PlayerRoomTransition
PlayerEnterShelter
RegionGateTransition
CreatureEnteredOccupiedShelter
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
```

**Per-creature events (88 creature types)** - one "dies" and one "notices you" event each:

| Creature | Dies | Notices you |
|---|---|---|
| Angler | `AnglerDeath` | `PlayerSpottedByAngler` |
| AquaCenti | `AquaCentiDeath` | `PlayerSpottedByAquaCenti` |
| Barnacle | `BarnacleDeath` | `PlayerSpottedByBarnacle` |
| BasiliskLizard | `BasiliskLizardDeath` | `PlayerSpottedByBasiliskLizard` |
| BigEel | `BigEelDeath` | `PlayerSpottedByBigEel` |
| BigJelly | `BigJellyDeath` | `PlayerSpottedByBigJelly` |
| BigMoth | `BigMothDeath` | `PlayerSpottedByBigMoth` |
| BigNeedleWorm | `BigNeedleWormDeath` | `PlayerSpottedByBigNeedleWorm` |
| BigSandGrub | `BigSandGrubDeath` | `PlayerSpottedByBigSandGrub` |
| BigSpider | `BigSpiderDeath` | `PlayerSpottedByBigSpider` |
| BlackLizard | `BlackLizardDeath` | `PlayerSpottedByBlackLizard` |
| BlizzardLizard | `BlizzardLizardDeath` | `PlayerSpottedByBlizzardLizard` |
| BlueLizard | `BlueLizardDeath` | `PlayerSpottedByBlueLizard` |
| BoxWorm | `BoxWormDeath` | `PlayerSpottedByBoxWorm` |
| BrotherLongLegs | `BrotherLongLegsDeath` | `PlayerSpottedByBrotherLongLegs` |
| Centipede | `CentipedeDeath` | `PlayerSpottedByCentipede` |
| Centiwing | `CentiwingDeath` | `PlayerSpottedByCentiwing` |
| CicadaA | `CicadaADeath` | `PlayerSpottedByCicadaA` |
| CicadaB | `CicadaBDeath` | `PlayerSpottedByCicadaB` |
| CyanLizard | `CyanLizardDeath` | `PlayerSpottedByCyanLizard` |
| DaddyLongLegs | `DaddyLongLegsDeath` | `PlayerSpottedByDaddyLongLegs` |
| Deer | `DeerDeath` | `PlayerSpottedByDeer` |
| DrillCrab | `DrillCrabDeath` | `PlayerSpottedByDrillCrab` |
| DropBug | `DropBugDeath` | `PlayerSpottedByDropBug` |
| EelLizard | `EelLizardDeath` | `PlayerSpottedByEelLizard` |
| EggBug | `EggBugDeath` | `PlayerSpottedByEggBug` |
| FireBug | `FireBugDeath` | `PlayerSpottedByFireBug` |
| FireSprite | `FireSpriteDeath` | `PlayerSpottedByFireSprite` |
| Fly | `FlyDeath` | `PlayerSpottedByFly` |
| Frog | `FrogDeath` | `PlayerSpottedByFrog` |
| GarbageWorm | `GarbageWormDeath` | `PlayerSpottedByGarbageWorm` |
| GrappleSnake | `GrappleSnakeDeath` | `PlayerSpottedByGrappleSnake` |
| GreenLizard | `GreenLizardDeath` | `PlayerSpottedByGreenLizard` |
| Hazer | `HazerDeath` | `PlayerSpottedByHazer` |
| HunterDaddy | `HunterDaddyDeath` | `PlayerSpottedByHunterDaddy` |
| IndigoLizard | `IndigoLizardDeath` | `PlayerSpottedByIndigoLizard` |
| Inspector | `InspectorDeath` | `PlayerSpottedByInspector` |
| JetFish | `JetFishDeath` | `PlayerSpottedByJetFish` |
| JungleLeech | `JungleLeechDeath` | `PlayerSpottedByJungleLeech` |
| KingVulture | `KingVultureDeath` | `PlayerSpottedByKingVulture` |
| LanternMouse | `LanternMouseDeath` | `PlayerSpottedByLanternMouse` |
| Leech | `LeechDeath` | `PlayerSpottedByLeech` |
| Loach | `LoachDeath` | `PlayerSpottedByLoach` |
| Millipede | `MillipedeDeath` | `PlayerSpottedByMillipede` |
| MirosBird | `MirosBirdDeath` | `PlayerSpottedByMirosBird` |
| MirosVulture | `MirosVultureDeath` | `PlayerSpottedByMirosVulture` |
| MotherSpider | `MotherSpiderDeath` | `PlayerSpottedByMotherSpider` |
| MothGrub | `MothGrubDeath` | `PlayerSpottedByMothGrub` |
| Overseer | `OverseerDeath` | `PlayerSpottedByOverseer` |
| PeachLizard | `PeachLizardDeath` | `PlayerSpottedByPeachLizard` |
| PinkLizard | `PinkLizardDeath` | `PlayerSpottedByPinkLizard` |
| PoleMimic | `PoleMimicDeath` | `PlayerSpottedByPoleMimic` |
| Rat | `RatDeath` | `PlayerSpottedByRat` |
| Rattler | `RattlerDeath` | `PlayerSpottedByRattler` |
| RedCentipede | `RedCentipedeDeath` | `PlayerSpottedByRedCentipede` |
| RedLizard | `RedLizardDeath` | `PlayerSpottedByRedLizard` |
| RippleSpider | `RippleSpiderDeath` | `PlayerSpottedByRippleSpider` |
| RotLoach | `RotLoachDeath` | `PlayerSpottedByRotLoach` |
| Salamander | `SalamanderDeath` | `PlayerSpottedBySalamander` |
| SandGrub | `SandGrubDeath` | `PlayerSpottedBySandGrub` |
| Scavenger | `ScavengerDeath` | `PlayerSpottedByScavenger` |
| ScavengerDisciple | `ScavengerDiscipleDeath` | `PlayerSpottedByScavengerDisciple` |
| ScavengerElite | `ScavengerEliteDeath` | `PlayerSpottedByScavengerElite` |
| ScavengerKing | `ScavengerKingDeath` | `PlayerSpottedByScavengerKing` |
| ScavengerTemplar | `ScavengerTemplarDeath` | `PlayerSpottedByScavengerTemplar` |
| SeaLeech | `SeaLeechDeath` | `PlayerSpottedBySeaLeech` |
| SkyWhale | `SkyWhaleDeath` | `PlayerSpottedBySkyWhale` |
| SmallCentipede | `SmallCentipedeDeath` | `PlayerSpottedBySmallCentipede` |
| SmallMoth | `SmallMothDeath` | `PlayerSpottedBySmallMoth` |
| SmallNeedleWorm | `SmallNeedleWormDeath` | `PlayerSpottedBySmallNeedleWorm` |
| Snail | `SnailDeath` | `PlayerSpottedBySnail` |
| Spider | `SpiderDeath` | `PlayerSpottedBySpider` |
| SpitLizard | `SpitLizardDeath` | `PlayerSpottedBySpitLizard` |
| SpitterSpider | `SpitterSpiderDeath` | `PlayerSpottedBySpitterSpider` |
| StowawayBug | `StowawayBugDeath` | `PlayerSpottedByStowawayBug` |
| Tardigrade | `TardigradeDeath` | `PlayerSpottedByTardigrade` |
| TempleGuard | `TempleGuardDeath` | `PlayerSpottedByTempleGuard` |
| TentaclePlant | `TentaclePlantDeath` | `PlayerSpottedByTentaclePlant` |
| TerrorLongLegs | `TerrorLongLegsDeath` | `PlayerSpottedByTerrorLongLegs` |
| TowerCrab | `TowerCrabDeath` | `PlayerSpottedByTowerCrab` |
| TrainLizard | `TrainLizardDeath` | `PlayerSpottedByTrainLizard` |
| TubeWorm | `TubeWormDeath` | `PlayerSpottedByTubeWorm` |
| Vulture | `VultureDeath` | `PlayerSpottedByVulture` |
| VultureGrub | `VultureGrubDeath` | `PlayerSpottedByVultureGrub` |
| WhiteLizard | `WhiteLizardDeath` | `PlayerSpottedByWhiteLizard` |
| Yeek | `YeekDeath` | `PlayerSpottedByYeek` |
| YellowLizard | `YellowLizardDeath` | `PlayerSpottedByYellowLizard` |
| ZoopLizard | `ZoopLizardDeath` | `PlayerSpottedByZoopLizard` |

Notes: `ScavengerDeath` / `PlayerSpottedByScavenger` cover every scavenger variant, and `SpiderDeath`
covers Spiders and all Big Spider variants (see the tables above). Slugcats (including slugpups) use the
`Player...` events instead. Some creatures never notice anything (they have no senses), so their
"notices you" event will simply never fire.

</details>

## Something not working?

1. Open **Options → Mods → Custom Soundboard**: problems in the file are listed at the top with line numbers.
2. Set `debug: true` under `settings:`, press RELOAD CONFIG, then look at `BepInEx/LogOutput.log` in the
   game folder. Every event that fires is logged along with the sound chosen, so you can tell
   "the event never happened" apart from "the event happened but nothing is set up for it".
3. A sound file that can't be played (corrupt, or an odd format) is reported by name. Re-saving it as a
   plain `.wav` (16-bit PCM) with a free editor like Audacity always works.
4. Sounds you've switched off with the checkboxes are skipped - **ENABLE ALL** turns everything back on.

## Upgrading from 0.1.0

Everything the old `meta.json` + `sounds.txt` pair did is now one entry in `soundboard.yaml`:

| 0.1.0 | 1.0.0 |
|---|---|
| `"file": "boom.wav"` in `meta.json` | `file: boom.wav` |
| `"event": "PlayerDeath"` | put it under `PlayerDeath:` |
| `vol=0.4` in `sounds.txt` | `volume: 0.4` |
| `"displayName"` / `"description"` | `name:` / `description:` |
| `"defaultEnabled": false` | `enabled: false` |
| `"group": "X"` on several sounds | one `together:` list |
| order in `meta.json` | order of the list |

The shipped `soundboard.yaml` is the old set of sounds converted this way. Sound files moved from
`soundeffects/` to `sounds/`. The on/off checkboxes start from scratch (their saved keys changed).
`PlayerDeath` and the other death events now fire once per death rather than on every `Die()` call.

## For developers

```
SoundboardMod/
├─ src/                        C# source (the BepInEx plugin)
│  ├─ Plugin.cs                Entry point
│  ├─ MiniYaml.cs              Small dependency-free YAML-subset parser with friendly errors
│  ├─ SoundboardConfig.cs      Turns the YAML into typed config + a list of issues
│  ├─ EventCatalog.cs          Every event name + description; forgiving name matching
│  ├─ ConfigFiles.cs           Where config/sounds live; finds audio files
│  ├─ SoundboardRuntime.cs     Loads/reloads the config, picks what to play, applies volume/delay
│  ├─ SoundRegistry.cs         Loads audio files and adds them to the game's SoundLoader at runtime
│  ├─ EventHooks.cs            Harmony patches that turn game moments into event names
│  ├─ Options.cs               The in-game options screen
│  └─ Cooldown.cs, DelayQueue.cs, FallTracker.cs, SoundRotation.cs   Small game-independent helpers
├─ tests/SoundboardMod.Tests/  xUnit tests for everything that doesn't need the game
├─ mod/                        The deployable Rain World mod folder
│  ├─ modinfo.json
│  ├─ soundboard.yaml          The default config (copied to the player's data folder on first run)
│  ├─ sounds/                  The bundled example sounds
│  └─ plugins/                 Build output (SoundboardMod.dll) lands here
└─ scripts/deploy.ps1          Build + copy mod/ into your Rain World install
```

**How sounds get into the game.** The game normally learns about sounds from `modify/soundeffects/sounds.txt`,
merged only when mods are *applied* from the menu - before plugins run - so a plugin can't use it for the
current launch. Instead `SoundRegistry` loads each file with `UnityWebRequestMultimedia`, registers a
`SoundID`, and adds it to the tables inside the game's own `SoundLoader` (private `soundTriggers` /
`allAudio`, by reflection), so playback still goes through `Room.PlaySound` with all its positional-audio
behaviour. If a game update changes those fields, `SoundRegistry.Available` turns false and the reason is
logged and shown on the options screen; nothing else breaks.

**Adding a hook.** Add a nested `[HarmonyPatch]` class to `EventHooks.cs` that calls
`Trigger("YourEventName", thing)`, and add `YourEventName` to `EventCatalog` so it appears in `events.txt`
and validates in the config. Per-creature events (`<Creature>Death`, `PlayerSpottedBy<Creature>`) need no
hook at all: they're generated from the game's creature list.

**Build and test** (needs the .NET SDK, and a Rain World install with BepInEx; set `RAINWORLD_PATH` if
it isn't in the default Steam location):

```powershell
dotnet build src/SoundboardMod.csproj                   # builds and copies the DLL into mod/plugins
dotnet test tests/SoundboardMod.Tests                   # 90+ tests; no game needed
./scripts/deploy.ps1                                    # build + install into your Rain World mods folder
./scripts/deploy.ps1 -SkipBuild                         # install the DLL that's already in mod/plugins
```

`deploy.ps1` also removes files left over from 0.1.0 (the old `sounds.txt` would otherwise still be merged
by the game). VS Code: *Ctrl+Shift+B* builds, and there are `test` and `deploy` tasks.

> `dotnet` on PATH: some machines have a runtime-only .NET on the *system* PATH that shadows the SDK.
> The VS Code tasks and `deploy.ps1` prepend `%USERPROFILE%\.dotnet` to work around it; otherwise
> `winget install --id Microsoft.DotNet.SDK.8 -e`.

**Releases** are git tags: `v0.1.0` is the last version with `meta.json`/`sounds.txt`; `v1.0.0` introduced
`soundboard.yaml`. `mod/modinfo.json`, the `[BepInPlugin]` version in `Plugin.cs` and the tag should agree.
