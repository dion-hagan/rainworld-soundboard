# Settings

[← Back to the README](../README.md)

A few events have numbers you may want to tune. All are optional.

| Setting | Default | Meaning |
|---|---|---|
| `hard-landing-speed` | `30` | Impact speed that counts as a hard landing (`PlayerHardLanding`). |
| `terminal-velocity` | `40` | Fall speed that triggers `PlayerTerminalVelocity`. |
| `player-jump-cooldown` | `2` | Seconds between `PlayerJumpCooldown` sounds. |
| `artificer-pyro-jump-cooldown` | `10` | Seconds between Artificer pyro-jump sounds. |
| `spotted-cooldown` | `10` | Seconds before the same creature can "spot" you again. |
| `swim-underwater-cooldown` | `5` | Seconds between `PlayerSwimUnderwater` sounds. |
| `creature-near-distance` | `10` | How close a creature has to get, in tiles (1 to 200), for its `<Creature>Near` event. See [Creatures nearby](events-creatures-nearby.md). |
| `creature-near-cooldown` | `10` | Seconds before the same creature can fire its `<Creature>Near` event again (`0` = only ever when it newly comes into range). |
| `shuffle` | `true` | `true` = the items under an event take turns in a random order (see [Taking turns](config-file.md#taking-turns)); `false` = in the order they're written. |
| `debug` | `false` | `true` writes every event that fires to `BepInEx/LogOutput.log` (great for working out why a sound doesn't play, and for finding good speed values). |

Falling in Rain World has no real speed cap, so "terminal velocity" is just a speed *you* pick.
With `debug: true` the log shows the impact speed of every landing, which is the easiest way to
choose a value you like.
