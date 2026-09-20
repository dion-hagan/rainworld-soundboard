# For developers

[← Back to the README](../README.md)

## Project layout

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
│  ├─ EdibleFoods.cs           The non-creature foods that get a PlayerEat<Food> event
│  ├─ Options.cs               The in-game options screen (Sounds, Add Sound and Edit Sound tabs)
│  ├─ EntryCooldowns.cs        Tracks which entries are cooling down (an entry's `cooldown:`)
│  ├─ EventHooks.Gourmand.cs   The Gourmand's slide/drop/roll hit events (a partial of EventHooks)
│  ├─ EventHooks.CreatureNear.cs   The <Creature>Near poll (a partial of EventHooks)
│  ├─ EventHooks.Rain.cs       The one-minute-to-rain warning (a partial of EventHooks)
│  └─ Cooldown.cs, DelayQueue.cs, FallTracker.cs, GourmandHitTracker.cs, NearTracker.cs, RainWarning.cs, SoundRotation.cs   Small game-independent helpers
├─ tests/SoundboardMod.Tests/  xUnit tests for everything that doesn't need the game
├─ mod/                        The deployable Rain World mod folder
│  ├─ modinfo.json             Also what the Workshop shows: title, description, tags
│  ├─ thumbnail.png            The Workshop / Remix preview image (16:9, under 1 MB)
│  ├─ soundboard.yaml          The default config (copied to the player's data folder on first run)
│  ├─ sounds/                  The bundled example sounds
│  └─ plugins/                 Build output (SoundboardMod.dll) lands here
├─ docs/                       The documentation the README links to
├─ workshop/                   Steam Workshop upload helpers (NOT shipped): description.bbcode.txt, PUBLISHING.md
└─ scripts/
   ├─ deploy.ps1               Build + copy mod/ into your Rain World install
   ├─ sync-config.ps1          Push mod/soundboard.yaml (+ new sounds) into the running game's personal copy
   └─ download_myinstants_favorites.py   Download a MyInstants user's favorites into the game's sounds folder
```

## Steam Workshop

Rain World uploads a mod from inside the game (Remix, select the mod, upload button), and it sends the
*installed* mod folder, so run `./scripts/deploy.ps1` first. The uploader rejects a `thumbnail.png` that is 1 MB or larger
or not 16:9. [`workshop/PUBLISHING.md`](../workshop/PUBLISHING.md) has the step-by-step checklist (first upload, updates) and
`workshop/description.bbcode.txt` is the Workshop page text to paste in.

## How sounds get into the game

The game normally learns about sounds from `modify/soundeffects/sounds.txt`,
merged only when mods are *applied* from the menu - before plugins run - so a plugin can't use it for the
current launch. Instead `SoundRegistry` loads each file with `UnityWebRequestMultimedia`, registers a
`SoundID`, and adds it to the tables inside the game's own `SoundLoader` (private `soundTriggers` /
`allAudio`, by reflection), so playback still goes through `Room.PlaySound` with all its positional-audio
behaviour. If a game update changes those fields, `SoundRegistry.Available` turns false and the reason is
logged and shown on the options screen; nothing else breaks.

## Adding a hook

Add a nested `[HarmonyPatch]` class to `EventHooks.cs` that calls
`Trigger("YourEventName", thing)`, and add `YourEventName` to `EventCatalog` so it appears in `events.txt`
and validates in the config. Per-creature events (`<Creature>Death`, `PlayerSpottedBy<Creature>`, `<Creature>Near`) need no
hook of their own: they are generated from the game's creature list, and one shared hook per family (the `Die()` patches,
`Tracker.CreatureNoticed`, the poll in `EventHooks.CreatureNear.cs`) covers every creature type.

## Build and test

Needs the .NET SDK, and a Rain World install with BepInEx; set `RAINWORLD_PATH` if
it isn't in the default Steam location.

```powershell
dotnet build src/SoundboardMod.csproj                   # builds and copies the DLL into mod/plugins
dotnet test tests/SoundboardMod.Tests                   # 100+ tests; no game needed
./scripts/deploy.ps1                                    # build + install into your Rain World mods folder
./scripts/deploy.ps1 -SkipBuild                         # install the DLL that's already in mod/plugins
```

## Editing the default config while the game is running

The game reads the *personal copy* in its data
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

## Releases

Releases are git tags: `v0.1.0` is the last version with `meta.json`/`sounds.txt`; `v1.0.0` introduced
`soundboard.yaml`; `v1.0.1` made the YAML the single source of truth for on/off state (checkboxes write back to it,
`disabled:` accepted) and added `scripts/sync-config.ps1`. `mod/modinfo.json`, the `[BepInPlugin]` version in
`Plugin.cs` and the tag should agree. `v1.0.2` made the options-screen checkboxes work like any Remix setting
(tick, then APPLY writes them into `soundboard.yaml`) and re-seeds them from the file every time the page opens.
`v1.1.0` added the **Add Sound** tab: event and sound dropdowns plus volume/delay boxes, and APPLY appends the
sound to the event's list in `soundboard.yaml`. `v1.1.1` lets that tab add a `together:` group (up to three sounds).
`v1.2.0` added the per-entry `cooldown:` option (and a Cooldown box on the Add Sound tab).
`v1.3.0` added the **Edit Sound** tab: change an existing entry's volume, delay and cooldown in place.
`v1.4.0` lets that tab delete an entry, or single sounds of a group (with a backup of the file first).
`v1.4.1` moves `PlayerEnterShelter` to the moment you walk into a shelter instead of when its door finishes closing,
which was too late for the sound to be heard before the sleep screen.
`v1.5.0` adds a **TEST** button beside each sound on the Add Sound tab, text search in the event, sound and entry
dropdowns, and a lot of new events: a creature of any type coming near (`<CreatureType>Near`), eating each kind of food
(`PlayerEat<Food>`), swimming underwater, running low on air and drowning, the movement techs (slide, slide pounce,
flips, wall jump, super jump) with Rivulet versions, and the Gourmand's slide/drop/roll hits. The repo also gained
the files for publishing to the Steam Workshop (`mod/thumbnail.png`, `workshop/`).
`v1.6.0` adds the `FatalRainImminent` event (one minute until the rain) and makes the Add Sound tab start new sounds at
30% volume with a 30 second cooldown. Every release is listed in [CHANGELOG.md](../CHANGELOG.md).
`v1.7.0` shuffles the order sounds play in: an event's entries take turns in a random order, different each launch,
unless you set `shuffle: false`.
