# Where your files live

[← Back to the README](../README.md)

Your personal `soundboard.yaml` and your own sounds are kept in the game's data folder, **not** in the mod's folder, so
Steam Workshop updates never wipe them. On Windows that is:

```
%USERPROFILE%\AppData\LocalLow\Videocult\Rain World\Soundboard
```

Press **OPEN FOLDER** on the mod's options screen to jump straight there. It holds:

| What | Notes |
|---|---|
| `soundboard.yaml` | Your config. See [Writing `soundboard.yaml`](config-file.md). |
| `sounds/` | Your `.wav`, `.ogg` and `.mp3` files (sub-folders work). Press **RELOAD CONFIG** after adding or replacing one. |
| `events.txt` | Every event name you can use, including creatures added by other mods. The mod rewrites it each time the game starts. |
| `soundboard.yaml.<date>-<time>.bak` | Backups made before an [Edit Sound delete](edit-sound.md) (the newest 10 are kept). |

> **Which `soundboard.yaml` counts?** The one in the game's data folder (above) - press **OPEN FOLDER**
> to get there. The `soundboard.yaml` inside the mod's own folder is only a **template**: Steam overwrites
> that folder whenever the mod updates, which would wipe your changes, so the game never reads it. The first
> time the game starts it copies the template into the data folder for you. Edit the copy in the data
> folder; editing the template changes nothing (the options screen will point this out if it notices).
> Delete your copy if you ever want the defaults back.
