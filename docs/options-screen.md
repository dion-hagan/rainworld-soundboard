# The options screen

[← Back to the README](../README.md)

Open **Remix → Custom Soundboard**. The screen has three tabs:

| Tab | What it's for |
|---|---|
| **Sounds** | A checkbox for every sound so you can switch individual ones off without editing anything, the **ENABLE ALL** / **DISABLE ALL** buttons, **RELOAD CONFIG** (re-read `soundboard.yaml` and your sound files without restarting) and **OPEN FOLDER**. It also lists any problems it found in your file, with line numbers. |
| **Add Sound** | Add a sound to an event from dropdowns. See [Adding a sound](add-sound.md). |
| **Edit Sound** | Change or delete the sounds that are already in your file. See [Changing or deleting a sound](edit-sound.md). |

## APPLY, BACK and REVERT

Nothing you do on this screen is written to `soundboard.yaml` until you press **APPLY**. Tick or untick the
checkboxes, pick or change things on the other tabs, then press **APPLY** and the change is written into
`soundboard.yaml`, so the file and the screen always agree.

The buttons at the bottom of the screen are Remix's own, the same on every mod's page (this mod can't rename them):

- **APPLY** writes your changes and takes you back to the mod list. It stays greyed out until you change something.
- **BACK** is the button next to it while nothing has changed: it just returns to the mod list.
- **REVERT** takes BACK's place as soon as you've changed something. **Hold it down** until it fills to throw
  the changes away and go back. Leaving without applying discards them too.

Don't confuse APPLY with **APPLY MODS** on Remix's mod list, which turns mods on and off. APPLY MODS is greyed out on
a mod's own page, and it isn't what saves a mod's settings.

## Searching the dropdowns

The event, sound and entry dropdowns on the Add Sound and Edit Sound tabs all search the same way:

- Click a dropdown to open it and **start typing** - the box shows what you've typed and the list narrows
  as you type. (Any dropdown from Remix itself needs a quick double-click before it will take typed letters;
  these don't.)
- It finds what you typed **anywhere in the name**, ignoring capitals and anything that isn't a letter or number,
  so `death`, `player death` and `player_death` all find `PlayerDeath`, and `boom1` finds `boom_1.wav`.
  Several words all have to match, in any order: `lizard near` shows every `...LizardNear` event.
- The best matches come first: names that start with what you typed, then names with a word starting with it,
  then the rest. Letters that merely appear in order somewhere in a name (`pdj` for `PlayerJump`) don't count.
- **Backspace** deletes a letter and **Enter** picks the top result and closes the list.
  Clicking a name still works as before, and hovering a name still shows what it means.
- With a controller the list opens as usual, without the search box.
