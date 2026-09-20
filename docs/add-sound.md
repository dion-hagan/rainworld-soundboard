# Adding a sound from the options screen

[← Back to the README](../README.md)

Open **Remix → Custom Soundboard** and switch to the **Add Sound** tab:

1. **When this happens** - pick an event from the dropdown: click it, then just type to narrow the list
   down ([more on searching](options-screen.md#searching-the-dropdowns)), or scroll; hover a name in the list to see what it means.
2. **Sound** - pick a `.wav`, `.ogg` or `.mp3` from your `sounds` folder or the ones that came with
   the mod (the sound dropdowns search the same way). Dropped new files into the folder? Press **RELOAD CONFIG** on the Sounds tab and they show up.
   Press **TEST** beside a sound to hear it once, at the volume in that row's Volume box, before you add
   it - nothing is saved. A long file plays only its first 10 seconds. It's the level you'd get in the game from a sound that isn't tied to a spot in the
   room; sounds that come from somewhere in a room get quieter with distance in the game, which a menu can't copy.
3. **Volume** (a percentage, `100` = as recorded; a new sound starts at `30`) and **Delay** (seconds after the event) for that sound -
   the same `volume:` and `delay:` options described in [Writing `soundboard.yaml`](config-file.md).
   Want several sounds at once? Pick more in the two extra rows (each with its own volume and delay) and tick
   **Play several sounds together** - it ticks itself when you pick a second sound. They're saved as one
   [`together:` group](config-file.md#playing-sounds-together): a single entry in the event's list whose sounds all play at
   the same time. (Untick it and only the first sound is added.)
   **Cooldown** (seconds, `0` = none; a new sound starts at `30`) is the entry's [`cooldown:`](config-file.md#cooldowns); for a group it covers the whole group.
   Those two starting values are only what this tab fills in, to keep a freshly added sound quiet and from playing over and over -
   change them per sound as you like. An entry you write by hand with no `volume:` or `cooldown:` still plays at full volume with no limit.
4. Press **APPLY**. The sound is added to the end of that event's list in your `soundboard.yaml` and starts
   working straight away. If the event wasn't in the file yet, it's added too. Everything else in the file -
   your comments, layout and other entries - is left exactly as it was.

## Things to know

- Like the checkboxes, nothing is written until you press **APPLY**; leaving without applying discards what you picked (see [APPLY, BACK and REVERT](options-screen.md#apply-back-and-revert)).
- If the file has a mistake in it (the Sounds tab lists them with line numbers), the sound isn't added and your
  picks stay on the tab so you can fix the file, press RELOAD CONFIG, and press **APPLY** again.
- The new entry appears in the Sounds tab's checkbox list the next time you open the Mods menu (it plays
  right away either way).
- This page only *adds*. To change an entry's numbers or delete it use the Edit Sound tab, and to switch it off untick it
  on the Sounds tab.
