# Custom Soundboard (Rain World mod)

Plays your own sound effects when things happen in Rain World: you die, you jump, you eat, a
Green Lizard spots you, you fall too fast, you change rooms, a scavenger throws a spear... **One
text file, `soundboard.yaml`, decides which sound plays for which event** - along with how loud it
is and how long to wait before it plays. You can edit that file by hand, or use the **Add Sound**
and **Edit Sound** tabs in the mod's options screen to add sounds from dropdowns and tweak or delete
the ones you already have. No coding, no rebuilding, and no game restart to try a change.

*(Versions before 1.0.0 needed a recompile plus two data files per sound. If you have a `0.1.0`
setup, see [Upgrading from 0.1.0](docs/upgrading-from-0.1.0.md).)*

### Example

https://github.com/user-attachments/assets/29a73c3b-f976-43dd-b9c5-7516e414860d

## About

This is possibly the dumbest thing I've ever written. It's just a simple soundboard mod for me and my friends to play around with
in Rainworld. I couldn't find anything general purpose like this. Feel free to use it/extend it.

Note: This is AI-assisted code -- I reviewed it, but didn't play close attention to architecture.

## Features

- **Hundreds of events** to hang a sound on: the player (death, jumping, landing, eating, movement techs,
  drowning...), the world (the one-minute rain warning, region gates, flashbangs...), and for every creature type
  when it dies, when it notices you and when it comes near.
- **Volume, delay and cooldown** for every sound, and **several sounds at once** if you want a combo.
- **Shuffled rotation:** give an event several sounds and they take turns in a random order.
- **An in-game options screen** to add sounds from dropdowns (with a **TEST** button), edit or delete them,
  and switch individual sounds on and off with checkboxes.
- **Bring your own sounds:** `.wav`, `.ogg` or `.mp3`, plus a script that imports your
  [MyInstants](https://www.myinstants.com) favorites. A set of example sounds is included.

## Quick start

1. Install the mod and enable **Custom Soundboard** in **Remix**, then restart the game.
   It comes with a set of example sounds so you can hear it working straight away.
   <!-- TODO: link the Workshop page here after the first upload -->
   *Install from the Steam Workshop:* subscribe to it there and it appears in Remix's mod list.
   *Install from GitHub:* copy the contents of
   this repo's `mod/` folder into `Rain World\RainWorld_Data\StreamingAssets\mods\dion_soundboard\` (or run
   `./scripts/deploy.ps1`).
2. Open **Remix → Custom Soundboard**. Press **OPEN FOLDER**.
3. In that folder:
   - drop your own `.wav`, `.ogg` or `.mp3` files into the `sounds` folder, and
   - open `soundboard.yaml` in Notepad (or any text editor) and point an event at them.
4. Back in the game, press **RELOAD CONFIG**. Done - jump around and listen.

Prefer not to touch the file? Use the **Add Sound** tab instead (pick an event and a sound, then press **APPLY**),
and the **Edit Sound** tab to change or delete sounds later. Both are covered in
[The options screen](docs/options-screen.md).

## Documentation

**Getting set up**

- [Where your files live: the data folder, and which `soundboard.yaml` the game actually reads](docs/where-files-live.md)
- [Using the options screen: the three tabs, APPLY / BACK / REVERT, and searching the dropdowns](docs/options-screen.md)

**Adding and changing sounds**

- [Adding a sound from the Add Sound tab: event, file, volume, delay, cooldown and playing several together](docs/add-sound.md)
- [Changing or deleting a sound from the Edit Sound tab](docs/edit-sound.md)
- [Importing your MyInstants favorites into the `sounds` folder](docs/myinstants.md)

**Configuring `soundboard.yaml`**

- [Writing `soundboard.yaml`: per-sound options, shuffled rotation, cooldowns and `together` groups](docs/config-file.md)
- [Settings: landing speed, cooldowns, creature distance, `shuffle` and `debug`](docs/settings.md)

**Events**

- [Event reference: what each built-in event is and when it fires](docs/events.md)
  - [Creatures nearby: the `<Creature>Near` events](docs/events-creatures-nearby.md)
  - [Water and breathing: swimming, drowning and drowned](docs/events-water-and-breathing.md)
  - [The Gourmand: slide, drop and roll hits](docs/events-gourmand.md)
- [Full list of every event name, including all the per-creature ones](docs/events-full-list.md)

**Help and reference**

- [Troubleshooting: something not working?](docs/troubleshooting.md)
- [Upgrading from 0.1.0: how the old `meta.json` and `sounds.txt` map onto `soundboard.yaml`](docs/upgrading-from-0.1.0.md)
- [Developer guide: project layout, building and testing, adding a hook, the Steam Workshop and releases](docs/developers.md)

## Something not working?

Open **Remix → Custom Soundboard**: problems in your file are listed at the top with line numbers. If a sound
still doesn't play, set `debug: true` and check the log - the steps are in [Something not working?](docs/troubleshooting.md).

## Contributing

Bug reports and pull requests are welcome. [For developers](docs/developers.md) covers building the
plugin, running the tests (no game needed) and how to add an event hook.

## Changelog

Every release is listed in [CHANGELOG.md](CHANGELOG.md).
