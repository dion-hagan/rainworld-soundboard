# Writing `soundboard.yaml`

[← Back to the README](../README.md)

The file has two sections. `events:` is the important one.

```yaml
settings:
  hard-landing-speed: 30

events:
  PlayerDeath:
    - boom.wav                 # simplest form: just a file name
    - file: sad-trombone.wav   # or with options
      volume: 0.5
      delay: 1.5
```

Under an event name, list what should play. Each list item is a file name, or a block of options:

| Option | What it does | Default |
|---|---|---|
| `file` | The audio file, from your `sounds` folder (`.wav`, `.ogg`, `.mp3`; the extension is optional; sub-folders work: `funny/boom.wav`). | *required* |
| `volume` | `1` = as recorded, `0.5` = half as loud, `2` = twice (0 to 10). | `1` |
| `delay` | Seconds to wait after the event before the sound plays (0 to 120). | `0` |
| `cooldown` | After this entry plays, it can't play again for this many seconds (0 to 3600). See [Cooldowns](#cooldowns). | `0` (no limit) |
| `name` | The label shown in the options screen. | made from the file name |
| `description` | A tooltip for the options screen. | what the event is |
| `enabled` | `false` = switched off. This is the checkbox in the options screen: ticking it and pressing APPLY edits this line for you, and opening the screen or pressing RELOAD CONFIG updates the checkbox from it. `disabled: true` means the same thing. | `true` |

There's also a compact one-line form: `- { file: boom.wav, volume: 0.5, delay: 1 }`.

## Taking turns

If an event has **several items, they take turns in a random order**: every item plays once before any
plays again, then it reshuffles (and never plays the same item twice in a row). The order is different each
time you start the game. So `PlayerDeath` with seven items plays a different death sound each time.
Items switched off in the options screen are skipped, and each event shuffles on its own.

Prefer the order you wrote them in (the first time the first item plays, the next time the second, and so on
around again)? Set `shuffle: false` under [`settings:`](settings.md).

## Cooldowns

`delay` waits *before* a sound plays; **`cooldown` stops it playing *again* too soon**. After an entry plays, it's
unavailable for that many seconds - handy for a long sound on something that fires constantly:

```yaml
  PlayerHardLanding:
    - file: long-scream.wav
      cooldown: 30        # at most once every 30 seconds
    - file: vine-boom.wav # no cooldown: plays whenever it's its turn
```

A cooling-down entry is skipped like a switched-off one, so the other entries under the event keep taking turns;
if it's the event's only entry (or all of them are cooling down), the event is simply quiet until one is ready.
On a [`together` group](#playing-sounds-together) put `cooldown` next to `together:` - it covers the whole group.
Cooldowns count game time (pausing doesn't run them down) and start over with each new game session.
(The `settings:` cooldowns (see [Settings](settings.md)) are different: they limit how often an *event* is allowed to fire.)

## Playing sounds together

To make one step of the rotation play **several sounds at once**, use `together`:

```yaml
  PlayerSpottedByScavenger:
    - can-i-put-my-balls-in-your-jaws.wav       # step 1: one sound
    - together:                                 # step 2: both of these at once
        - enrique.wav
        - file: indian-song.wav
          volume: 0.2
      volume: 0.8       # optional: multiplies each sound's volume
      delay: 0.5        # optional: added to each sound's delay
      name: "Enrique + Indian song"
```

## Tips

- Indent with **spaces**, never tabs. Lines that line up belong together.
- Put quotes around text containing a colon or `#`: `name: "Fire: hot"`.
- Write decimals with a dot: `0.5`, not `0,5`.
- Event names are forgiving: `PlayerDeath`, `player death` and `player-death` all work.
- Typos get a hint: *"'PlayerDeth' isn't an event this mod knows... Did you mean 'PlayerDeath'?"*
- If the file has a syntax error the mod falls back to the default config (and tells you so on the
  options screen); if you press RELOAD CONFIG with an error in it, nothing changes.
- Replacing a sound? Save your new file over the old one with the same name and press RELOAD CONFIG.
