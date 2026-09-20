# Importing your MyInstants favorites

[← Back to the README](../README.md)

Got a pile of favorites on [myinstants.com](https://www.myinstants.com)? `scripts/download_myinstants_favorites.py`
grabs all of them and drops the `.mp3` files straight into your `sounds` folder. It only needs
[Python 3](https://www.python.org/downloads/) - nothing to `pip install`. The script isn't shipped inside the
Workshop mod, so grab it from this repo's [`scripts/`](../scripts) folder.

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
