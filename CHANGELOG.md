# Changelog

## 1.0.0

First release.

- A hit summons one enemy, drawn only from types you have already encountered.
- Spawns are validated against room geometry: no spawning through walls, out of bounds,
  over pits, or inside terrain.
- Summoned enemies are cleared when you change rooms, so no room becomes impassable.
- `F4` toggles the challenge, `F3` toggles the counter.
