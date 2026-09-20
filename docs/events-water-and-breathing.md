# Water and breathing

[← Back to the README](../README.md)

| Event | Fires when |
|---|---|
| `PlayerSwimUnderwater` | The slugcat dives under the surface: head fully under water and swimming (the deep-swim animation). Fires once per dive, and at most once per 'swim-underwater-cooldown' seconds (setting, default 5) so bobbing at the surface doesn't spam it. |
| `PlayerDrowning` | The slugcat runs low on air underwater - the point where the game slows it down and makes it thrash about. Fires once per struggle; it fires again only after the slugcat has recovered most of its breath. |
| `PlayerDrowned` | The slugcat dies of drowning. Fires alongside PlayerDeath. |

How the game's own rules are used, so you know exactly when these fire:

- **Underwater** means the game's `submerged` flag (the head is more than 90% under water) *and* the deep-swim
  animation. Treading water at the surface, standing on the floor of a flooded room or climbing a pole in it
  doesn't count. Diving fires `PlayerSwimUnderwater` once; the next dive counts again after the cooldown.
- **Low on air** means the slugcat's air is below *its own* "out of breath" level (the game's `drownThreshold`,
  a third of the lungs for every slugcat), not a number of ours. Slugcats that hold their breath longer (like
  Rivulet) just take longer to get there. It only counts while the head is under water.
  It fires once and is armed again once the slugcat has got its air back to roughly two thirds (halfway between
  "out of breath" and full), so bobbing up for one gasp doesn't re-fire it every time.
- **Drowned** is the death that happens when the game's drowning counter fills (about three seconds after the
  air hits zero), not any death that happens to be underwater. `PlayerDeath` fires as well.
- Slugpups and other computer-controlled slugcats never fire these.
