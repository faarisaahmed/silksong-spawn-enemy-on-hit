# Changelog

## 1.0.1

Icon only - no gameplay or behaviour changes.

- Moss Grotto is no longer blurred, so the background is clearly visible behind the figures.
- Fixed Hornet's mask being erased. The background was being cut by deleting near-white
  pixels, and her mask is white, so it was punching a hole through her head and leaving her
  as a dark silhouette. The backing is now flooded away from the edges inwards, which only
  removes background actually connected to the border and leaves enclosed white intact.
- Lace and Garmond ship with transparency already, so they are composited untouched rather
  than run through background removal that could only damage them.

## 1.0.0

First release.

- A hit summons one enemy, drawn only from types you have already encountered.
- Spawns are validated against room geometry: no spawning through walls, out of bounds,
  over pits, or inside terrain.
- Summoned enemies are cleared when you change rooms, so no room becomes impassable.
- `F4` toggles the challenge, `F3` toggles the counter.
