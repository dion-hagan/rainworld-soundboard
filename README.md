# Custom Soundboard (Rain World mod)

A BepInEx mod for Rain World that plays your own `.wav` sound effects when
specific in-game events happen (player death, jumping, eating, etc), with an
in-game options menu that lists every registered sound and lets you enable or
disable each one individually, or all at once.

Sound loading follows the approach described in
[EtiTheSpirit's "Rain World New Sound Tutorial"](https://gist.github.com/EtiTheSpirit/b66450898bfb559c8578a4de04dc1029):
sounds are declared as `SoundID`s in code and attached to `.wav` files through
the game's own data-merge system (`modify/soundeffects/sounds.txt`). This
means sounds get full support for volume/pitch randomization, doppler, and
positional audio, same as vanilla sounds - but it also means **the game needs
a restart whenever you add or edit a sound** (see below).

This repo started as a **skeleton** (one placeholder beep, `examplebeep.wav`,
to verify everything works) and now also ships a small set of meme/soundboard
clips wired up to real gameplay events as a working example set - swap them
out for your own whenever you like.

## Sound effects

| ID | File | Triggers on | Default | Description |
|---|---|---|---|---|
| `Soundboard_Example_Beep` | `examplebeep.wav` | `PlayerJump` | Enabled | A short placeholder beep used to verify the mod is installed and working correctly. Safe to delete once you add your own sounds. |
| `Soundboard_AnimeWow` | `anime-wow-sound-effect.wav` | `PlayerGrabExplosive` | Enabled | Plays when you pick up an explosive spear or a scavenger grenade. |
| `Soundboard_DiscordLeave` | `discord-leave-noise.wav` | `ScavengerDeath` | Enabled | Plays when a Scavenger dies. |
| `Soundboard_Fah` | `fahhhhhhhhhhhhhh.wav` | `PlayerDeath` | Enabled | Plays when you die. |
| `Soundboard_FortniteDeath` | `fortnite-death.wav` | `SpiderDeath` | Enabled | Plays when a Spider or Big Spider dies. |
| `Soundboard_Gunshot` | `gunshot-one.wav` | `PlayerThrowExplosiveSpear` | Enabled | Plays when you throw an explosive spear. |
| `Soundboard_HubIntro` | `hub-intro-sound.wav` | `PlayerEnterShelter` | Enabled | Plays when a shelter door closes (typically right after you enter for the cycle). |
| `Soundboard_FortniteDeath_Lizard` | `fortnite-death.wav` | `LizardDeath` | Enabled | Plays when a Lizard dies. |
| `Soundboard_Romance` | `romanceeeeeeeeeeeeee.wav` | `PlayerGrabSlugcat` | Enabled | Plays when you pick up another slugcat onto your back. |
| `Soundboard_VineBoom` | `vine-boom.wav` | `PlayerHardLanding` | Enabled | Plays on a hard landing, sharing the slot with Bone Crack (roughly 50/50). |
| `Soundboard_GoodBoy` | `what-a-good-boy.wav` | `PlayerEatCreature` | Enabled | Plays when you eat a creature (meat), as opposed to fruit/plants. |
| `Soundboard_BennyHill` | `benny-hill.wav` | `PlayerSpottedByPredator` | Enabled | Plays when a Lizard (other than Cyan), Spider/BigSpider, or Vulture first notices you. |
| `Soundboard_YameteKudasai` | `yamete-kudasai.wav` | `SnailExplosion` | Enabled | Plays when a Snail explodes. |
| `Soundboard_ScavengerSpotted` | `can-i-put-my-balls-in-your-jaws.wav` | `PlayerSpottedByScavenger` | Enabled | Plays when a Scavenger first notices you - a 50/50 pick against Enrique+Indian Song playing together. |
| `Soundboard_AnimeAhh` | `anime-ahh.wav` | `CicadaOrLanternMouseDeath` | Enabled | Plays when a Cicada ("squidcada") or Lantern Mouse dies. |
| `Soundboard_BoneCrack` | `bone-crack.wav` | `PlayerHardLanding` | Enabled | Plays on a hard landing, sharing the slot with Vine Boom (roughly 50/50). |
| `Soundboard_FartMeme` | `fartmeme.wav` | `ScavengerDeath` | Enabled | Plays when a Scavenger dies, sharing the slot with We Do Not Care and Discord Leave Noise. |
| `Soundboard_WeDoNotCare` | `we-do-not-care.wav` | `ScavengerDeath` | Enabled | Plays when a Scavenger dies, sharing the slot with Fart Meme and Discord Leave Noise. |
| `Soundboard_MusicaElevador` | `musica-elevador-short.wav` | `RegionGateTransition` | Enabled | Plays when a region gate transition starts. |
| `Soundboard_HatsuneMikuWeee` | `hatsune-miku-weeeeeeee.wav` | `PlayerJumpWithCicada` | Enabled | Plays when you jump while holding a Cicada ("squidcada"). |
| `Soundboard_Enrique` | `enrique.wav` | `PlayerSpottedByScavenger` | Enabled | Plays together with Indian Song (as one 50/50 pick against Can I Put My Balls In Your Jaws) when a Scavenger first notices you. |
| `Soundboard_IndianSong` | `indian-song.wav` | `PlayerSpottedByScavenger` | Enabled | Plays together with Enrique (as one 50/50 pick against Can I Put My Balls In Your Jaws) when a Scavenger first notices you. |
| `Soundboard_Meow` | `m-e-o-w.wav` | `PlayerArtificerPyroJump` | Enabled | Plays when Artificer does a pyro jump (explosion-boosted "double jump"). |
| `Soundboard_Scatman` | `scatman.wav` | `PlayerSpottedByCyanLizard` | Enabled | Plays when a Cyan Lizard first notices you (instead of Benny Hill). |
| `Soundboard_NuclearAlarm` | `nuclear-alarm-siren.wav` | `VultureGrubSignal` | Enabled | Plays when a thrown Vulture Grub starts emitting its signal, calling nearby vultures. |
| `Soundboard_FbiOpenUp` | `fbi-open-up-sfx.wav` | `CreatureEnteredOccupiedShelter` | Enabled | Plays when any creature enters a shelter that already has a player in it. |

This table is hand-maintained; the source of truth for each sound's
description and menu label is [`mod/soundeffects/meta.json`](mod/soundeffects/meta.json).
Update both when you add or change a sound.

> **Note:** `PlayerHardLanding` uses a fall-speed threshold
> (`HardLandingSpeedThreshold` in `EventHooks.cs`, currently `20`) tuned from
> real in-game measurements: normal jump landings topped out around `~9.4`,
> a drop from a tall pole hit `~22.9`. Adjust it if it fires too often/rarely.
> Also, "Spearmaster spearing a creature for food pips" (a MoreSlugcats-only
> mechanic) wasn't wired up - no verified stable hook was found for it, so
> `what-a-good-boy.wav` currently only covers "ate a creature directly".

## Project layout

```
SoundboardMod/
├─ src/                          C# source (the BepInEx plugin)
│  ├─ SoundboardMod.csproj
│  ├─ Plugin.cs                  Entry point (BepInPlugin)
│  ├─ SoundboardData.cs          Loads meta.json, registers SoundIDs
│  ├─ Options.cs                 In-game options menu (OptionInterface)
│  └─ EventHooks.cs              Harmony patches that trigger sounds
├─ mod/                          The deployable Rain World mod folder
│  ├─ modinfo.json
│  ├─ soundeffects/
│  │  ├─ meta.json               Menu labels/descriptions/event mapping
│  │  └─ examplebeep.wav         Placeholder test sound
│  ├─ modify/soundeffects/
│  │  └─ sounds.txt              Registers .wav files against SoundIDs
│  └─ plugins/                   Build output (SoundboardMod.dll) goes here
├─ scripts/deploy.ps1            Build + copy mod/ into your Rain World install
└─ Directory.Build.props         Points the build at your Rain World install
```

## Prerequisites

- **.NET SDK** (8.0 or later) - used to compile the plugin.
- **VS Code** with the **C# Dev Kit** (or C#) extension, for editing/IntelliSense/build tasks.
- A Rain World install with **BepInEx** already present (the game ships with
  it as of Downpour; check for a `BepInEx` folder next to `RainWorld.exe`).

By default the project assumes Rain World is installed at
`C:\Program Files (x86)\Steam\steamapps\common\Rain World`. If yours is
elsewhere, either set an environment variable before building:

```powershell
$env:RAINWORLD_PATH = "D:\Games\Rain World"
```

or edit `Directory.Build.props` directly.

> **Note on `dotnet` on PATH:** this machine already had a runtime-only
> install of .NET at `C:\Program Files\dotnet\` on the *system* PATH, which
> always gets checked before anything on your *user* PATH - so a plain
> `dotnet build` in an arbitrary terminal will say "No .NET SDKs were found"
> even though the SDK is installed (at `%USERPROFILE%\.dotnet`). The VS Code
> build task and `deploy.ps1` both work around this automatically by
> prepending the SDK's folder to PATH for that command only. If you want
> a plain `dotnet` to work everywhere, run
> `winget install --id Microsoft.DotNet.SDK.8 -e` yourself and accept the
> UAC prompt when it appears - that installs the SDK into the same
> `C:\Program Files\dotnet\` the system already resolves to.

## Building

```bash
dotnet build src/SoundboardMod.csproj
```

This restores `Microsoft.NETFramework.ReferenceAssemblies` (so you don't need
a full .NET Framework install to target `net472`) and references BepInEx,
Harmony, `Assembly-CSharp.dll` and the Unity engine DLLs straight out of your
Rain World install - none of those files are copied into this repo. A
post-build step copies the compiled `SoundboardMod.dll` into `mod/plugins/`
automatically.

In VS Code: `Ctrl+Shift+B` runs the default build task. There's also a
"deploy (build + copy to Rain World mods folder)" task (Terminal -> Run Task)
that builds and then copies the whole `mod/` folder into your Rain World
install for you - equivalent to running:

```powershell
./scripts/deploy.ps1
```

## Installing / testing in-game

1. Run the deploy script (above), or on a machine with no .NET SDK/dev
   setup, run it with `-SkipBuild` to just copy the already-committed
   `mod/plugins/SoundboardMod.dll` as-is instead of rebuilding:
   ```powershell
   ./scripts/deploy.ps1 -SkipBuild
   ```
   You can also skip the script entirely and manually copy the `mod/`
   folder's *contents* into
   `<Rain World install>/RainWorld_Data/StreamingAssets/mods/dion_soundboard/`.
2. Launch Rain World, go to **Options -> Mods**, and enable **Custom Soundboard**.
3. Restart the game (required - the sound-merge system only runs on launch).
4. Start/continue a game and jump - you should hear the example beep.
5. Open **Options -> Mods -> Custom Soundboard** (the gear/arrow icon next to
   the mod) to see the sound list and its Enable All / Disable All buttons.

## Adding your own sound

1. Convert your sound to `.wav` (or `.ogg`) and drop it in `mod/soundeffects/`.
   File names must not contain underscores unless you're providing numbered
   variants (`myclip_1.wav`, `myclip_2.wav`, ...).
2. Register it in `mod/modify/soundeffects/sounds.txt`:
   ```
   [ADD]MyCoolSound/vol=0.5 : myclip
   ```
   See the comments at the top of that file, or the
   [tutorial](https://gist.github.com/EtiTheSpirit/b66450898bfb559c8578a4de04dc1029),
   for all supported parameters (volume/pitch ranges, doppler, etc).
3. Add an entry to `mod/soundeffects/meta.json` with the **same id**:
   ```json
   {
     "id": "MyCoolSound",
     "displayName": "My Cool Sound",
     "description": "Plays when I do the thing.",
     "file": "myclip.wav",
     "event": "PlayerJump",
     "defaultEnabled": true
   }
   ```
   The `event` value must match one of the event keys wired up in
   `src/EventHooks.cs` (see below), or a new one you add yourself.

   Optionally add a `"group"` string. When multiple *enabled* sounds share
   both the same `event` and the same `group`, they're treated as one unit:
   the random pick is between groups (not individual sounds), and every
   sound in the winning group plays together. Sounds with no `group` are
   their own group of one, so e.g. two grouped sounds sharing an event with
   one ungrouped sound gives a 50/50 split between "the ungrouped one alone"
   and "both grouped ones together" - see `Soundboard_Enrique` /
   `Soundboard_IndianSong` (grouped) vs `Soundboard_ScavengerSpotted`
   (ungrouped) in `meta.json` for a working example.
4. Rebuild/redeploy, restart the game, and toggle it on in the mods menu.

### Available event keys

Wired up in `src/EventHooks.cs`:

| Event key | Fires when |
|---|---|
| `PlayerDeath` | The slugcat dies (`Player.Die`) |
| `PlayerJump` | The slugcat jumps (`Player.Jump`) - easiest one to test with |
| `PlayerEat` | The slugcat eats anything, food or creature (`Player.ObjectEaten`) |
| `PlayerEatCreature` | ...specifically when what it ate was a creature (meat) |
| `PlayerGrabExplosive` | The slugcat picks up an `ExplosiveSpear` or `ScavengerBomb` (`Creature.Grab`) |
| `PlayerGrabSlugcat` | The slugcat picks up another slugcat onto its back (`Creature.Grab`) |
| `PlayerThrowExplosiveSpear` | The slugcat throws an `ExplosiveSpear` (`Player.ThrownSpear`) |
| `PlayerHardLanding` | The slugcat hits the ground above a fall-speed threshold (`Player.TerrainImpact`) |
| `PlayerEnterShelter` | A shelter door closes (`ShelterDoor.DoorClosed`) |
| `ScavengerDeath` | A Scavenger dies (`Creature.Die`) |
| `LizardDeath` | A Lizard dies (`Creature.Die`) |
| `SpiderDeath` | A Spider or BigSpider dies (`Spider.Die` / `BigSpider.Die`) |
| `PlayerSpottedByPredator` | A Lizard (other than Cyan), Spider/BigSpider, or Vulture first notices you (`Tracker.CreatureNoticed`) - fires once per sighting, not continuously while it's chasing you |
| `SnailExplosion` | A Snail dies/explodes (`Snail.Die`) |
| `PlayerSpottedByScavenger` | A Scavenger first notices you (`Tracker.CreatureNoticed`) - separate from `PlayerSpottedByPredator` since Scavengers aren't strictly hostile |
| `PlayerSpottedByCyanLizard` | Specifically a Cyan Lizard first notices you (`Tracker.CreatureNoticed`) - takes priority over `PlayerSpottedByPredator` for that one lizard color |
| `CicadaOrLanternMouseDeath` | A Cicada ("squidcada") or Lantern Mouse dies (`Cicada.Die` / `LanternMouse.Die`) |
| `RegionGateTransition` | A region gate's door-opening sequence starts (`RegionGate.OPENCLOSE`) - heuristic, not confirmed strictly one-shot |
| `PlayerJumpWithCicada` | The slugcat jumps while grasping a Cicada ("squidcada") (`Player.Jump` + grasp check) - fires alongside `PlayerJump`, not instead of it |
| `PlayerArtificerPyroJump` | Artificer does an explosion-boosted jump (`Player.ClassMechanicsArtificer`, edge-detected on `pyroJumpped`) |
| `VultureGrubSignal` | A thrown Vulture Grub starts emitting its call (`VultureGrub.InitiateSignal`), which summons nearby vultures |
| `CreatureEnteredOccupiedShelter` | Any creature moves into a shelter room that already has a player in it (`Creature.NewRoom`) - some creature types override `NewRoom` themselves and may not trigger this, same caveat as the death-event hooks above |

If multiple enabled sounds share the same event key, one group is chosen at
random each time that event fires, and everything in that group plays
together (see `ChooseGroup` in `EventHooks.cs`, and the `group` field
described above).

To hook a different event, add another Harmony patch in `EventHooks.cs`
following the same pattern (patch a method, call `Trigger("YourEventKey", creature)`
or `TriggerAt`/`TriggerNonPositional` directly), then reference
`"YourEventKey"` from any sound's `"event"` field in `meta.json`. A couple of
the patches above target `private` game methods, so they're referenced by
string name (`"ThrownSpear"`) rather than `nameof(...)` - `nameof` only works
on members your code could otherwise call directly.

## How enable/disable works

Every sound gets its own persisted `Configurable<bool>` (Rain World's mod
config system), shown as a checkbox in the options menu. `EventHooks` checks
that value before playing a sound, so disabling one just silences it - no
restart needed for toggling on/off, only for adding/changing sounds themselves.
