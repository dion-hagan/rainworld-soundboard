# Something not working?

[← Back to the README](../README.md)

1. Open **Remix → Custom Soundboard**: problems in the file are listed at the top with line numbers.
2. Set `debug: true` under `settings:`, press RELOAD CONFIG, then look at `BepInEx/LogOutput.log` in the
   game folder. Every event that fires is logged along with the sound chosen, so you can tell
   "the event never happened" apart from "the event happened but nothing is set up for it".
3. A sound file that can't be played (corrupt, or an odd format) is reported by name. Re-saving it as a
   plain `.wav` (16-bit PCM) with a free editor like Audacity always works.
4. Sounds you've switched off with the checkboxes are skipped - **ENABLE ALL** turns everything back on.
