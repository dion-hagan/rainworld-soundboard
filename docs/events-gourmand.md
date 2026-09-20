# The Gourmand

[← Back to the README](../README.md)

The Gourmand's body is a weapon: sliding, rolling or dropping onto a living creature stuns and hurts it.
These events fire at the moment the game applies that damage (not on every overlap), for the Gourmand only,
and play at the Gourmand. The same creature can't set off another of them for half a second, since the game
can register one impact more than once. Creatures that are already dead, the slugpup and (unless friendly fire
is on) other players aren't hurt by it, so they don't fire it either.

| Event | Fires when |
|---|---|
| `GourmandSlideHit` | The Gourmand's belly slide (or the rocket jump out of one) slams into a living creature and hurts it. Plays at the Gourmand, once per creature per half second. |
| `GourmandDropHit` | The Gourmand comes down hard on a living creature (a fast fall onto it) and hurts it. Plays at the Gourmand, once per creature per half second. |
| `GourmandRollHit` | The Gourmand rolls into a living creature and hurts it (the roll has its own half-second lockout). Plays at the Gourmand. |
