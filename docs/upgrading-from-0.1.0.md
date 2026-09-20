# Upgrading from 0.1.0

[← Back to the README](../README.md)

Everything the old `meta.json` + `sounds.txt` pair did is now one entry in `soundboard.yaml`:

| 0.1.0 | 1.0.0 |
|---|---|
| `"file": "boom.wav"` in `meta.json` | `file: boom.wav` |
| `"event": "PlayerDeath"` | put it under `PlayerDeath:` |
| `vol=0.4` in `sounds.txt` | `volume: 0.4` |
| `"displayName"` / `"description"` | `name:` / `description:` |
| `"defaultEnabled": false` | `enabled: false` (or `disabled: true`) |
| `"group": "X"` on several sounds | one `together:` list |
| order in `meta.json` | order of the list |

The shipped `soundboard.yaml` is the old set of sounds converted this way. Sound files moved from
`soundeffects/` to `sounds/`. On/off state now lives in `soundboard.yaml` itself (`enabled:`), not in the
game's saved mod settings, so any checkboxes you'd ticked in 0.1.0 start from what the file says.
`PlayerDeath` and the other death events now fire once per death rather than on every `Die()` call.
