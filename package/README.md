# Spawn Enemy On Hit

**Every time you take a hit, an enemy appears next to you.**

The catch, and the whole point of the mod:

> ### It only ever spawns enemies you have already seen.
>
> Nothing is pulled out of thin air. The mod quietly remembers every enemy type in every
> room you walk through, and a hit summons one of *those*. So **the more of Pharloom you
> explore, the more dangerous getting hit becomes.**

Hour one in Moss Grotto, a mistake costs you a crawler. Twenty hours in, that same
mistake reaches into everything you have ever fought and picks something at random.
You build your own difficulty curve by playing the game.

It also means the mod can never blindside you with something you have no idea how to
fight. Every enemy it throws at you is one you have met before — you just did not expect
to meet it *here*.

## The feedback loop

Getting hit puts another enemy in the room. More enemies in the room means more chances
to get hit. A single sloppy moment in a corridor can turn into a fight you have to
actually win.

Two rules keep that from becoming unfair:

- **One enemy per hit.** Cause and effect stay obvious. The pressure comes from enemies
  piling up, not from the rate accelerating.
- **Leaving the room clears them.** A doorway is always an escape hatch, so no room can
  become permanently impassable. Fleeing is a real option, and sometimes the right one.

## Controls

| Key | Does |
| --- | --- |
| `F4` | Turn the challenge on or off, mid-run |
| `F3` | Show or hide the counter |

The counter shows whether the mod is armed, how many enemy types it has learned, how many
hits you have taken, and what it summoned last.

## Where spawns appear

Next to you, and only somewhere an enemy could actually stand. Placement sweeps outward
from Hornet and stops at the first wall, checks the spot is inside the room, probes
downward for a floor, and rejects anything buried in terrain. It will not drop an enemy
through a wall, into the void, or hovering in mid-air because you happened to be jumping.

Spawns land 2.5–5 units away — close enough to be your problem immediately, far enough
that you get to see it arrive before it reaches you.

## Settings

Everything is in `BepInEx/config/com.faaris.spawnenemyonhit.cfg`, created on first run.

| Setting | Default | What it does |
| --- | --- | --- |
| `EnemiesPerHit` | `1` | How many appear per hit |
| `MaxActiveSpawns` | `25` | Ceiling on summoned enemies at once — a framerate rail, not a difficulty knob |
| `MaxEnemyHp` | `0` | Skip anything tougher than this. `0` means no limit, which leaves bosses in the pool |
| `MinDistance` / `MaxDistance` | `2.5` / `5` | How close spawns land |
| `ClearOnRoomChange` | `true` | Wipe summons when you leave. Turning this off removes your escape hatch |
| `CooldownSeconds` | `0.35` | Stops one damage event counting as two hits |
| `HarvestIntervalSeconds` | `3` | How often the room is re-scanned for new enemy types |

### Fair warning about `MaxEnemyHp`

Left at `0`, bosses are eligible once you have fought them. A boss in a stairwell is
funny exactly once. Set it to around `200` to cap the pool at regular enemies.

## Installing

**With a mod manager:** install it, and the BepInEx dependency comes along automatically.

**By hand:** install [BepInExPack Silksong](https://thunderstore.io/c/hollow-knight-silksong/p/BepInEx/BepInExPack_Silksong/),
then drop `SpawnEnemyOnHit.dll` into `BepInEx/plugins/SpawnEnemyOnHit/`.

## Notes

- Any health loss counts, including spikes and bad landings. The mod watches your health
  rather than hooking one damage path, because Hornet loses health several different ways
  and hooking a single one would miss the others. For a challenge run, a hit is a hit.
- The roster starts empty each session and fills within seconds of moving. If your very
  first hit happens before anything is known, the log says so and nothing spawns.
