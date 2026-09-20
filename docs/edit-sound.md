# Changing or deleting a sound from the options screen

[← Back to the README](../README.md)

The **Edit Sound** tab changes the numbers of sounds that are already in your file, and can delete them:

1. **Entry** - pick one from the dropdown (they're listed as `Event: Name`, in the order they appear in the file;
   [type to search](options-screen.md#searching-the-dropdowns), hover to see the files).
2. Its sounds appear in up to three rows, each with **Volume** (%) and **Delay** (seconds). An entry with a
   [`together` group](config-file.md#playing-sounds-together) shows one row per sound; unused rows are greyed out. (If a group has
   more than three sounds, only the first three can be edited here.)
3. **Cooldown** is the entry's [`cooldown:`](config-file.md#cooldowns) (`0` = none); for a group it covers the whole group.
4. Press **APPLY**. Only the numbers you changed are written, right where they belong: an existing `volume:` line has
   its value replaced (a comment after it stays), a missing one is added under the entry's other options, and a bare
   `- boom.wav` is turned into `- file: boom.wav` to make room. Changing numbers never removes anything, everything
   else in the file is left exactly as it was, and the change takes effect immediately.

**Deleting.** Tick **Delete this whole entry** to remove the entry (a single sound, or a whole group) from the file,
or - for a group - tick the **Delete** box on a sound's own row to remove just that sound. Nothing is deleted until
you press **APPLY**: the ticks are ordinary pending changes, so REVERT (or leaving without applying) discards them, and
the line under the entry name turns red to say what's about to go. On APPLY:

- the previous `soundboard.yaml` is first copied to `soundboard.yaml.<date>-<time>.bak` in your soundboard folder
  (the newest 10 are kept, shared with `scripts/sync-config.ps1`), and **nothing is deleted if that copy fails** -
  to undo a delete, copy the backup over `soundboard.yaml` and press RELOAD CONFIG;
- the entry's own lines go (including comments *inside* it and one after it on the same line); comment lines around
  it stay, and if that was its event's last entry the now-empty `EventName:` line goes too;
- at least one sound has to stay in a group - to remove them all, delete the whole entry;
- the result is re-read and checked, so exactly what you asked for is gone and nothing else in the file has moved.

Like Add Sound, nothing is written until you press **APPLY**, and if the file has a mistake, or the entry is written in
a shape that can't be edited safely (for example inside an inline `[ ... ]` list, or several sounds on one line),
nothing is changed and the status line says why so you can edit that one by hand. A `together` group's own
`volume:`/`delay:` (which multiply/add to every sound in it) aren't shown or changed here - only each sound's own numbers.
