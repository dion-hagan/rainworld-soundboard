# Changelog

Every release of Custom Soundboard, newest first. Each version is a git tag (`vX.Y.Z`), and
`mod/modinfo.json`, the `[BepInPlugin]` version in `src/Plugin.cs` and the tag always agree.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

- **`PlayerThrowSpear` event.** Plays whenever the slugcat throws a spear of any kind (explosive and electric
  spears too, alongside `PlayerThrowExplosiveSpear` for an explosive one). Saint's weak spear toss doesn't count.

### Fixed

- **`PlayerEnterShelter` no longer double-plays in 2-player co-op.** Each player has their own camera, and
  when a second camera ends up pointed at a shelter that's already sheltering the other player - the second
  player walking in after the first, or a dead player's camera being pulled along with the survivor's - it
  used to fire the event again (advancing the sound rotation and starting a second, different sound) instead
  of doing nothing.

## [1.7.0] - 2026-09-20

### Changed

- **Sounds under an event now play in a shuffled order.** Entries still take turns, but in a random order:
  every entry plays once before any plays again, the same one never plays twice in a row, and the order is
  different each time you start the game (it used to start at the top of the list every launch). Set
  `shuffle: false` under `settings:` in `soundboard.yaml` to get the old in-file order back.

## [1.6.0] - 2026-09-20

### Added

- **`FatalRainImminent` event.** Plays once per cycle when the rain timer reaches one minute, so a sound can
  tell you the cycle is about to end. It plays even if you're already in a shelter, doesn't play again when you
  cross a region gate, and never plays in The Rot (More Slugcats), where the rain doesn't hit.
- This changelog.

### Changed

- **New sounds start quieter and rate-limited.** The Add Sound tab now starts at **Volume 30** (was 100) and
  **Cooldown 30** seconds (was 0), and goes back to those after each add. Change either per sound as before.
  Only that form is affected: entries already in `soundboard.yaml`, and entries you write by hand without
  `volume:` or `cooldown:`, keep playing at full volume with no limit.

## [1.5.0] - 2026-09-20

### Added

- **TEST button** beside each sound on the Add Sound tab: hear the file once, at that row's Volume box, before
  adding it. A long file plays only its first 10 seconds.
- **Text search** in the event, sound and entry dropdowns on the Add Sound and Edit Sound tabs.
- **`<CreatureType>Near` events**: a creature of any type coming within `creature-near-distance` of you, with
  `creature-near-distance` and `creature-near-cooldown` settings.
- **`PlayerEat<Food>` events**: one per kind of non-creature food.
- **Water events**: `PlayerSwimUnderwater`, `PlayerDrowning` (low on air) and `PlayerDrowned`.
- **Movement-tech events**: `PlayerSlide`, `PlayerSlidePounce`, `PlayerSlideFlip`, `PlayerRollPounce`,
  `PlayerWallJump`, `PlayerBackflip`, `PlayerSuperJump`, plus the Rivulet versions `RivuletJump`, `RivuletSlide`
  and `RivuletSlidePounce`.
- **Gourmand hit events**: `GourmandSlideHit`, `GourmandDropHit` and `GourmandRollHit`.
- Files for publishing to the Steam Workshop (`mod/thumbnail.png`, `workshop/`).

### Fixed

- The README and scripts now say the options screen is at **Remix → Custom Soundboard**, not Options → Mods.

## [1.4.1] - 2026-09-20

### Changed

- `PlayerEnterShelter` plays when you walk into a shelter instead of when its door finishes closing, which was
  too late for the sound to be heard before the sleep screen.

## [1.4.0] - 2026-09-19

### Added

- **Delete** on the Edit Sound tab: remove a whole entry, or single sounds of a `together` group. A timestamped
  backup of `soundboard.yaml` is made first, and nothing is deleted if the backup fails.

## [1.3.0] - 2026-09-19

### Added

- **Edit Sound tab**: change an existing entry's volume, delay and cooldown in place, without touching anything
  else in the file.

## [1.2.0] - 2026-09-18

### Added

- Per-entry **`cooldown:`** option (0 to 3600 seconds): after an entry plays it can't play again for that long,
  and the event's rotation skips it meanwhile. The Add Sound tab gained a Cooldown box.

## [1.1.1] - 2026-09-18

### Added

- The Add Sound tab can add a **`together:` group**: up to three sounds that play at the same time as one entry.

## [1.1.0] - 2026-09-18

### Added

- **Add Sound tab** in the Remix options screen: pick an event and a sound from dropdowns, set volume and delay,
  press APPLY, and the sound is added to `soundboard.yaml` and starts working straight away.

## [1.0.2] - 2026-09-18

### Changed

- The options-screen checkboxes now work like any Remix setting: tick, then APPLY writes them into
  `soundboard.yaml`. They are re-seeded from the file every time the page opens.

## [1.0.1] - 2026-09-18

### Changed

- `soundboard.yaml` is the single source of truth for whether a sound is on or off (the checkboxes write back to
  it, and `disabled:` is accepted).

### Added

- `scripts/sync-config.ps1`, to bring a personal copy of the config up to date with the shipped template.

## [1.0.0] - 2026-09-18

### Changed

- Sounds, volumes, delays and grouping are all configured in a single **`soundboard.yaml`** (replacing
  `meta.json` and `sounds.txt`), with friendly error messages and "did you mean...?" suggestions for event names.
- Every event is documented in a collapsed section of the README.

## [0.1.0] - 2026-09-18

First release: plays your own sound files on Rain World events, configured with `meta.json` and `sounds.txt`.

[1.7.0]: https://github.com/dion-hagan/rainworld-soundboard/compare/v1.6.0...v1.7.0
[1.6.0]: https://github.com/dion-hagan/rainworld-soundboard/compare/v1.5.0...v1.6.0
[1.5.0]: https://github.com/dion-hagan/rainworld-soundboard/compare/v1.4.1...v1.5.0
[1.4.1]: https://github.com/dion-hagan/rainworld-soundboard/compare/v1.4.0...v1.4.1
[1.4.0]: https://github.com/dion-hagan/rainworld-soundboard/compare/v1.3.0...v1.4.0
[1.3.0]: https://github.com/dion-hagan/rainworld-soundboard/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/dion-hagan/rainworld-soundboard/compare/v1.1.1...v1.2.0
[1.1.1]: https://github.com/dion-hagan/rainworld-soundboard/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/dion-hagan/rainworld-soundboard/compare/v1.0.2...v1.1.0
[1.0.2]: https://github.com/dion-hagan/rainworld-soundboard/compare/v1.0.1...v1.0.2
[1.0.1]: https://github.com/dion-hagan/rainworld-soundboard/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/dion-hagan/rainworld-soundboard/compare/v0.1.0...v1.0.0
[0.1.0]: https://github.com/dion-hagan/rainworld-soundboard/releases/tag/v0.1.0
