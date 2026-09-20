This is possibly the dumbest thing I've ever written. It's just a simple soundboard mod for me and my friends to play around with
in Rainworld. I couldn't find anything general purpose like this. Feel free to use it/extend it.

Note: This is AI-assisted code -- I reviewed it, but didn't play close attention to architecture.

### Example

https://github.com/user-attachments/assets/29a73c3b-f976-43dd-b9c5-7516e414860d

# Custom Soundboard (Rain World mod)

Plays your own sound effects when things happen in Rain World: you die, you jump, you eat, a
Green Lizard spots you, you fall too fast, you change rooms, a scavenger throws a spear... **One
text file, `soundboard.yaml`, decides which sound plays for which event** - along with how loud it
is and how long to wait before it plays. You can edit that file by hand, or use the **Add Sound**
and **Edit Sound** tabs in the mod's options screen to add sounds from dropdowns and tweak or delete
the ones you already have. No coding, no rebuilding, and no game restart to try a change.

*(Versions before 1.0.0 needed a recompile plus two data files per sound. If you have a `0.1.0`
setup, see [Upgrading from 0.1.0](#upgrading-from-010).)*

## Quick start

1. Install the mod and enable **Custom Soundboard** in **Remix**, then restart the game.
   It comes with a set of example sounds so you can hear it working straight away.
2. Open **Remix → Custom Soundboard**. Press **OPEN FOLDER**.
3. In that folder:
   - drop your own `.wav`, `.ogg` or `.mp3` files into the `sounds` folder, and
   - open `soundboard.yaml` in Notepad (or any text editor) and point an event at them.
4. Back in the game, press **RELOAD CONFIG**. Done - jump around and listen.

Prefer not to touch the file? Use the **Add Sound** tab instead - see
[Adding a sound from the options screen](#adding-a-sound-from-the-options-screen) - and the **Edit Sound**
tab to change or delete sounds later - see [Changing or deleting a sound](#changing-or-deleting-a-sound-from-the-options-screen).

The options screen also has a checkbox for every sound so you can switch individual ones off
without editing anything - **tick or untick them, then press SAVE and the change is written into
`soundboard.yaml`**, so the file and the screen always agree (REVERT, or leaving without saving, discards
the change) - and it lists any problems it found in your file (with line numbers). Note that Remix's
**APPLY** button is greyed out on every mod's page; that one is for turning mods on and off, not for settings.

> **Which `soundboard.yaml` counts?** The one in the game's data folder
> (`%USERPROFILE%\AppData\LocalLow\Videocult\Rain World\Soundboard` on Windows) - press **OPEN FOLDER**
> to get there. The `soundboard.yaml` inside the mod's own folder is only a **template**: Steam overwrites
> that folder whenever the mod updates, which would wipe your changes, so the game never reads it. The first
> time the game starts it copies the template into the data folder for you. Edit the copy in the data
> folder; editing the template changes nothing (the options screen will point this out if it notices).
> Delete your copy if you ever want the defaults back.

## Adding a sound from the options screen

Open **Remix → Custom Soundboard** and switch to the **Add Sound** tab:

1. **When this happens** - pick an event from the dropdown (click it and scroll, or just start typing to
   search; hover a name in the list to see what it means).
2. **Sound** - pick a `.wav`, `.ogg` or `.mp3` from your `sounds` folder or the ones that came with
   the mod. Dropped new files into the folder? Press **RELOAD CONFIG** on the Sounds tab and they show up.
   Press **TEST** beside a sound to hear it once, at the volume in that row's Volume box, before you add
   it - nothing is saved. A long file plays only its first 10 seconds. It's the level you'd get in the game from a sound that isn't tied to a spot in the
   room; sounds that come from somewhere in a room get quieter with distance in the game, which a menu can't copy.
3. **Volume** (a percentage, `100` = as recorded) and **Delay** (seconds after the event) for that sound -
   the same `volume:` and `delay:` options described below.
   Want several sounds at once? Pick more in the two extra rows (each with its own volume and delay) and tick
   **Play several sounds together** - it ticks itself when you pick a second sound. They're saved as one
   [`together:` group](#playing-sounds-together): a single entry in the event's list whose sounds all play at
   the same time. (Untick it and only the first sound is added.)
   **Cooldown** (seconds, `0` = none) is the entry's [`cooldown:`](#cooldowns); for a group it covers the whole group.
4. Press **SAVE**. The sound is added to the end of that event's list in your `soundboard.yaml` and starts
   working straight away. If the event wasn't in the file yet, it's added too. Everything else in the file -
   your comments, layout and other entries - is left exactly as it was.

A few things to know:

- Like the checkboxes, nothing is written until you press **SAVE**; leaving without saving discards what you picked.
- If the file has a mistake in it (the Sounds tab lists them with line numbers), the sound isn't added and your
  picks stay on the tab so you can fix the file, press RELOAD CONFIG, and save again.
- The new entry appears in the Sounds tab's checkbox list the next time you open the Mods menu (it plays
  right away either way).
- This page only *adds*. To change an entry's numbers or delete it use the Edit Sound tab, and to switch it off untick it
  on the Sounds tab.

## Changing or deleting a sound from the options screen

The **Edit Sound** tab changes the numbers of sounds that are already in your file, and can delete them:

1. **Entry** - pick one from the dropdown (they're listed as `Event: Name`, in the order they appear in the file;
   type to search, hover to see the files).
2. Its sounds appear in up to three rows, each with **Volume** (%) and **Delay** (seconds). An entry with a
   [`together` group](#playing-sounds-together) shows one row per sound; unused rows are greyed out. (If a group has
   more than three sounds, only the first three can be edited here.)
3. **Cooldown** is the entry's [`cooldown:`](#cooldowns) (`0` = none); for a group it covers the whole group.
4. Press **SAVE**. Only the numbers you changed are written, right where they belong: an existing `volume:` line has
   its value replaced (a comment after it stays), a missing one is added under the entry's other options, and a bare
   `- boom.wav` is turned into `- file: boom.wav` to make room. Changing numbers never removes anything, everything
   else in the file is left exactly as it was, and the change takes effect immediately.

**Deleting.** Tick **Delete this whole entry** to remove the entry (a single sound, or a whole group) from the file,
or - for a group - tick the **Delete** box on a sound's own row to remove just that sound. Nothing is deleted until
you press **SAVE**: the ticks are ordinary pending changes, so REVERT or leaving without saving discards them, and
the line under the entry name turns red to say what's about to go. On SAVE:

- the previous `soundboard.yaml` is first copied to `soundboard.yaml.<date>-<time>.bak` in your soundboard folder
  (the newest 10 are kept, shared with `scripts/sync-config.ps1`), and **nothing is deleted if that copy fails** -
  to undo a delete, copy the backup over `soundboard.yaml` and press RELOAD CONFIG;
- the entry's own lines go (including comments *inside* it and one after it on the same line); comment lines around
  it stay, and if that was its event's last entry the now-empty `EventName:` line goes too;
- at least one sound has to stay in a group - to remove them all, delete the whole entry;
- the result is re-read and checked, so exactly what you asked for is gone and nothing else in the file has moved.

Like Add Sound, nothing is written until you press **SAVE**, and if the file has a mistake, or the entry is written in
a shape that can't be edited safely (for example inside an inline `[ ... ]` list, or several sounds on one line),
nothing is changed and the status line says why so you can edit that one by hand. A `together` group's own
`volume:`/`delay:` (which multiply/add to every sound in it) aren't shown or changed here - only each sound's own numbers.

## Importing your MyInstants favorites

Got a pile of favorites on [myinstants.com](https://www.myinstants.com)? `scripts/download_myinstants_favorites.py`
grabs all of them and drops the `.mp3` files straight into your `sounds` folder. It only needs
[Python 3](https://www.python.org/downloads/) - nothing to `pip install`. The script isn't shipped inside the
Workshop mod, so grab it from this repo's [`scripts/`](scripts) folder.

1. Open a terminal where you saved the script and run it with your MyInstants username:

   ```powershell
   python download_myinstants_favorites.py your-username
   ```

2. Wait for it to finish - it prints a line per sound and a summary at the end.
3. In the game, open **Options → Mods → Custom Soundboard** and press **RELOAD CONFIG**. The new sounds now show up
   in the **Add Sound** tab's dropdown.

It saves to `%USERPROFILE%\AppData\LocalLow\Videocult\Rain World\Soundboard\sounds` (the `sounds` folder inside the one
**OPEN FOLDER** opens) and names each file after the sound's title. Some handy bits:

- Safe to re-run: a sound whose file already exists is skipped, so it only fetches what's new (and a failed download
  just gets retried next time).
- `--list` shows what it would download without downloading anything.
- `-o "C:\some\folder"` saves somewhere else instead, and `--delay 1` slows it down (seconds between downloads, default 0.3).

> **Why not just use the MyInstants API?** The unofficial one only ever returns the first 90 favorites, so the script
> reads your profile pages itself and picks up all of them.

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
| `cooldown` | After this entry plays, it can't play again for this many seconds (0 to 3600). See [Cooldowns](#cooldowns). | `0` (no limit) |
| `name` | The label shown in the options screen. | made from the file name |
| `description` | A tooltip for the options screen. | what the event is |
| `enabled` | `false` = switched off. This is the checkbox in the options screen: ticking it and pressing SAVE edits this line for you, and opening the screen or pressing RELOAD CONFIG updates the checkbox from it. `disabled: true` means the same thing. | `true` |

There's also a compact one-line form: `- { file: boom.wav, volume: 0.5, delay: 1 }`.

### Taking turns

If an event has **several items, they take turns**: the first time it happens the first item plays,
the next time the second, and so on around again. So `PlayerDeath` with seven items plays a different
death sound each time. Items switched off in the options screen are skipped.

### Cooldowns

`delay` waits *before* a sound plays; **`cooldown` stops it playing *again* too soon**. After an entry plays, it's
unavailable for that many seconds - handy for a long sound on something that fires constantly:

```yaml
  PlayerHardLanding:
    - file: long-scream.wav
      cooldown: 30        # at most once every 30 seconds
    - file: vine-boom.wav # no cooldown: plays whenever it's its turn
```

A cooling-down entry is skipped like a switched-off one, so the other entries under the event keep taking turns;
if it's the event's only entry (or all of them are cooling down), the event is simply quiet until one is ready.
On a [`together` group](#playing-sounds-together) put `cooldown` next to `together:` - it covers the whole group.
Cooldowns count game time (pausing doesn't run them down) and start over with each new game session.
(The `settings:` cooldowns below are different: they limit how often an *event* is allowed to fire.)

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
| `swim-underwater-cooldown` | `5` | Seconds between `PlayerSwimUnderwater` sounds. |
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
| `PlayerEnterShelter` | The slugcat walks into a shelter, before the door closes (so a long sound has time to play). Fires every time you enter one, even if you leave again without sleeping. |

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

### Water and breathing

| Event | Fires when |
|---|---|
| `PlayerSwimUnderwater` | The slugcat dives under the surface: head fully under water and swimming (the deep-swim animation). Fires once per dive, and at most once per 'swim-underwater-cooldown' seconds (setting, default 5) so bobbing at the surface doesn't spam it. |
| `PlayerDrowning` | The slugcat runs low on air underwater - the point where the game slows it down and makes it thrash about. Fires once per struggle; it fires again only after the slugcat has recovered most of its breath. |
| `PlayerDrowned` | The slugcat dies of drowning. Fires alongside PlayerDeath. |

How the game's own rules are used, so you know exactly when these fire:

- **Underwater** means the game's `submerged` flag (the head is more than 90% under water) *and* the deep-swim
  animation. Treading water at the surface, standing on the floor of a flooded room or climbing a pole in it
  doesn't count. Diving fires `PlayerSwimUnderwater` once; the next dive counts again after the cooldown.
- **Low on air** means the slugcat's air is below *its own* "out of breath" level (the game's `drownThreshold`,
  a third of the lungs for every slugcat), not a number of ours. Slugcats that hold their breath longer (like
  Rivulet) just take longer to get there. It only counts while the head is under water.
  It fires once and is armed again once the slugcat has got its air back to roughly two thirds (halfway between
  "out of breath" and full), so bobbing up for one gasp doesn't re-fire it every time.
- **Drowned** is the death that happens when the game's drowning counter fills (about three seconds after the
  air hits zero), not any death that happens to be underwater. `PlayerDeath` fires as well.
- Slugpups and other computer-controlled slugcats never fire these.

### The Gourmand

The Gourmand's body is a weapon: sliding, rolling or dropping onto a living creature stuns and hurts it.
These events fire at the moment the game applies that damage (not on every overlap), for the Gourmand only,
and play at the Gourmand. The same creature can't set off another of them for half a second, since the game
can register one impact more than once. Creatures that are already dead, the slugpup and (unless friendly fire
is on) other players aren't hurt by it, so they don't fire it either.

| Event | Fires when |
|---|---|
| `GourmandSlideHit` | The Gourmand's belly slide (or the rocket jump out of one) slams into a living creature and hurts it. Plays at the Gourmand, once per creature per half second. |
| `GourmandDropHit` | The Gourmand comes down hard on a living creature (a fast fall onto it) and hurts it. Plays at the Gourmand, once per creature per half second. |
| `GourmandRollHit` | The Gourmand rolls into a living creature and hurts it (the roll has its own half-second lockout). Plays at the Gourmand. |

<details>
<summary><b>Full list of every event name (211)</b> - click to expand</summary>

Every event you can put under `events:` in `soundboard.yaml`, as of Rain World v1.11.8 with the
More Slugcats and Watcher creatures. Creatures added by other mods get the same two events
automatically; the list the mod writes to `events.txt` always includes them.

**Built-in events (38)** - described in the tables above:

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
PlayerSwimUnderwater
PlayerDrowning
PlayerDrowned
GourmandSlideHit
GourmandDropHit
GourmandRollHit
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

1. Open **Remix → Custom Soundboard**: problems in the file are listed at the top with line numbers.
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
| `"defaultEnabled": false` | `enabled: false` (or `disabled: true`) |
| `"group": "X"` on several sounds | one `together:` list |
| order in `meta.json` | order of the list |

The shipped `soundboard.yaml` is the old set of sounds converted this way. Sound files moved from
`soundeffects/` to `sounds/`. On/off state now lives in `soundboard.yaml` itself (`enabled:`), not in the
game's saved mod settings, so any checkboxes you'd ticked in 0.1.0 start from what the file says.
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
│  ├─ YamlEditor.cs            Edits soundboard.yaml's text in place: an entry's enabled line, adding, changing options, removing
│  ├─ SoundAdder.cs            Builds and double-checks a new entry before it's written (Add Sound tab -> file)
│  ├─ SoundTweaker.cs          Changes or deletes an existing entry / group sounds and double-checks it (Edit Sound tab -> file)
│  ├─ SoundLibrary.cs          Lists the audio files the Add Sound dropdown offers
│  ├─ SoundboardRuntime.cs     Loads/reloads the config, picks what to play, applies volume/delay
│  ├─ SoundRegistry.cs         Loads audio files and adds them to the game's SoundLoader at runtime
│  ├─ EventHooks.cs            Harmony patches that turn game moments into event names
│  ├─ Options.cs               The in-game options screen (Sounds, Add Sound and Edit Sound tabs)
│  ├─ EntryCooldowns.cs        Tracks which entries are cooling down (an entry's `cooldown:`)
│  ├─ EventHooks.Gourmand.cs   The Gourmand's slide/drop/roll hit events (a partial of EventHooks)
│  └─ Cooldown.cs, DelayQueue.cs, FallTracker.cs, GourmandHitTracker.cs, SoundRotation.cs   Small game-independent helpers
├─ tests/SoundboardMod.Tests/  xUnit tests for everything that doesn't need the game
├─ mod/                        The deployable Rain World mod folder
│  ├─ modinfo.json
│  ├─ soundboard.yaml          The default config (copied to the player's data folder on first run)
│  ├─ sounds/                  The bundled example sounds
│  └─ plugins/                 Build output (SoundboardMod.dll) lands here
└─ scripts/
   ├─ deploy.ps1               Build + copy mod/ into your Rain World install
   ├─ sync-config.ps1          Push mod/soundboard.yaml (+ new sounds) into the running game's personal copy
   └─ download_myinstants_favorites.py   Download a MyInstants user's favorites into the game's sounds folder
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
dotnet test tests/SoundboardMod.Tests                   # 100+ tests; no game needed
./scripts/deploy.ps1                                    # build + install into your Rain World mods folder
./scripts/deploy.ps1 -SkipBuild                         # install the DLL that's already in mod/plugins
```

**Editing the default config while the game is running.** The game reads the *personal copy* in its data
folder (`%USERPROFILE%\AppData\LocalLow\Videocult\Rain World\Soundboard`), never `mod/soundboard.yaml`, which is
just the template that gets copied there on first run. So edits to `mod/soundboard.yaml` don't reach the game
by themselves. To push them in without restarting:

```powershell
./scripts/sync-config.ps1            # backs up the personal copy, replaces it, copies new sound files
./scripts/sync-config.ps1 -WhatIf    # show what it would do without doing it
```

then press **RELOAD CONFIG** in the mod's options screen. The backup (`soundboard.yaml.<time>.bak`, last 10 kept)
matters because checkbox changes saved from the options screen are written into the personal copy. Code changes still
need `deploy.ps1` and a restart (the game locks the DLL while it runs).

`deploy.ps1` also removes files left over from 0.1.0 (the old `sounds.txt` would otherwise still be merged
by the game). VS Code: *Ctrl+Shift+B* builds, and there are `test` and `deploy` tasks.

> `dotnet` on PATH: some machines have a runtime-only .NET on the *system* PATH that shadows the SDK.
> The VS Code tasks and `deploy.ps1` prepend `%USERPROFILE%\.dotnet` to work around it; otherwise
> `winget install --id Microsoft.DotNet.SDK.8 -e`.

**Releases** are git tags: `v0.1.0` is the last version with `meta.json`/`sounds.txt`; `v1.0.0` introduced
`soundboard.yaml`; `v1.0.1` made the YAML the single source of truth for on/off state (checkboxes write back to it,
`disabled:` accepted) and added `scripts/sync-config.ps1`. `mod/modinfo.json`, the `[BepInPlugin]` version in
`Plugin.cs` and the tag should agree. `v1.0.2` made the options-screen checkboxes work like any Remix setting
(tick, then SAVE writes them into `soundboard.yaml`) and re-seeds them from the file every time the page opens.
`v1.1.0` added the **Add Sound** tab: event and sound dropdowns plus volume/delay boxes, and SAVE appends the
sound to the event's list in `soundboard.yaml`. `v1.1.1` lets that tab add a `together:` group (up to three sounds).
`v1.2.0` added the per-entry `cooldown:` option (and a Cooldown box on the Add Sound tab).
`v1.3.0` added the **Edit Sound** tab: change an existing entry's volume, delay and cooldown in place.
`v1.4.0` lets that tab delete an entry, or single sounds of a group (with a backup of the file first).
`v1.4.1` moves `PlayerEnterShelter` to the moment you walk into a shelter instead of when its door finishes closing,
which was too late for the sound to be heard before the sleep screen.
