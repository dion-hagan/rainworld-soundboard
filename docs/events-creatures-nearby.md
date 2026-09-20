# Creatures nearby

[← Back to the README](../README.md)

There are no fixed events on this page: like `<Creature>Death`, **every creature type gets its own event**,
named `<Creature>Near` after the creature the way the game spells it - `RedLizardNear`, `GreenLizardNear`,
`ScavengerEliteNear`, `KingVultureNear`, `DaddyLongLegsNear`, and one for every creature another mod adds (the
per-creature table in the [full list](events-full-list.md) has them all). Each fires when a
creature of that type comes within `creature-near-distance` tiles of you ([setting](settings.md), default 10), and at most once per
creature per `creature-near-cooldown` seconds (setting, default 10). Unlike `PlayerSpottedBy...`, the creature
doesn't have to have noticed you: this is "something is close", not "something has seen me".

```yaml
events:
  RedLizardNear:
    - oh-no.wav
```

How exactly it works, so you know when to expect it:

- **Same room only.** A creature counts only while it is in the room you are in. One in the next room, or
  inside a pipe or den, isn't near however close it is on the map. Walls don't block it: a creature on the other
  side of a thin wall counts.
- **Distance** is a straight line in *tiles* (the small grid squares rooms are built from; the game screen is about 68 tiles
  wide, so the default 10 is about a seventh of it), measured from you to the *closest part* of the
  creature's body, so a long one (a Daddy Long Legs, a centipede) counts when any part of it is close.
  It is checked four times a second, so a creature that darts in and out between two checks can be missed.
- **Only when it arrives.** It fires the moment the creature comes into range, not repeatedly while it stays
  there. A creature has to get clearly outside (a quarter further away than the range) before it counts as having
  left, so one hovering right at the edge doesn't keep firing; and after it has left and come back, the
  cooldown still has to be over. A creature that is already near when you enter its room counts as arriving.
- **Not counted:** dead creatures, other slugcats (including slugpups), and a creature you are carrying - one
  you're holding doesn't fire, and doesn't fire when you put it down next to you either.
- Friendly or tame creatures count like any other, so a lizard that follows you around fires whenever the
  cooldown allows. With several players (Jolly Co-op) the cooldown is shared, so one creature fires once rather than once per player (unless the cooldown is 0).
- The check only runs at all if `soundboard.yaml` has a sound for at least one `...Near` event, so leaving them
  out costs nothing.
