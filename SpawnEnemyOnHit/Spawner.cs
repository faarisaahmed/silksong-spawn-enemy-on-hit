using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpawnEnemyOnHit
{
    /// <summary>
    /// Puts a banked enemy into the room next to Hornet, and keeps track of what it put there.
    /// </summary>
    /// <remarks>
    /// Placement is the fiddly part. Dropping an enemy exactly on Hornet is both unreadable and
    /// unfair - it can land a hit before you have seen it exist - so spawns are offset to one
    /// side and then dropped onto whatever floor is below. Without that downward probe an enemy
    /// spawned beside Hornet mid-jump appears in mid-air, and a walker with no ground under it
    /// either falls out of the room or stands on nothing.
    /// </remarks>
    public sealed class Spawner
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly System.Random _rng = new System.Random();

        public int ActiveCount
        {
            get
            {
                _spawned.RemoveAll(go => go == null);
                return _spawned.Count;
            }
        }

        public GameObject SpawnNearHero(BankedEnemy enemy, float minDistance, float maxDistance,
                                        float groundProbe, Action<string> log)
        {
            var hero = HeroController.instance;
            if (hero == null || enemy?.Template == null) return null;

            Vector3 at = ChoosePosition(hero.transform.position, minDistance, maxDistance, groundProbe);

            GameObject spawned;
            try
            {
                spawned = UnityEngine.Object.Instantiate(enemy.Template);
            }
            catch (Exception ex)
            {
                log($"could not spawn {enemy.Id}: {ex.Message}");
                return null;
            }

            spawned.name = enemy.Id + " (spawned)";
            spawned.transform.position = at;
            // Templates are kept inactive; this is the point it becomes a real enemy.
            spawned.SetActive(true);

            _spawned.Add(spawned);
            return spawned;
        }

        /// <summary>
        /// Finds somewhere next to Hornet that an enemy can actually stand.
        /// </summary>
        /// <remarks>
        /// Offsetting sideways and dropping to the floor is not enough on its own, and the ways
        /// it fails are all things you would notice immediately:
        ///
        /// A sideways offset ignores walls, so a spawn beside Hornet in a narrow shaft lands
        /// *inside* the rock or in the room on the other side of it. The fix is to sweep from
        /// Hornet outwards and stop at the first thing in the way, so a spawn can never end up
        /// somewhere she could not walk to in a straight line.
        ///
        /// A downward probe finds the first floor beneath the *candidate*, which across a wall is
        /// a completely different platform - hence the sweep coming first, so the probe only ever
        /// runs somewhere reachable.
        ///
        /// And a room has edges. Silksong reports its own dimensions, so anything landing outside
        /// them is rejected rather than dropped into the void.
        ///
        /// Candidates are tried nearest-first and alternating sides, so the enemy turns up as
        /// close as the geometry allows rather than as far as the settings permit.
        /// </remarks>
        private Vector3 ChoosePosition(Vector3 heroPos, float minDistance, float maxDistance, float groundProbe)
        {
            int terrain = TerrainMask();
            float firstSide = _rng.Next(2) == 0 ? -1f : 1f;

            // Nearest-first: the point is to appear beside her, not somewhere technically legal.
            for (float d = minDistance; d <= maxDistance; d += 1f)
            {
                for (int s = 0; s < 2; s++)
                {
                    float side = s == 0 ? firstSide : -firstSide;
                    if (TryPlace(heroPos, side, d, groundProbe, terrain, out Vector3 spot))
                        return spot;
                }
            }

            // Nothing passed. Standing on Hornet is poor but visible and in-bounds, which beats
            // a spawn inside a wall or outside the room.
            return heroPos;
        }

        private bool TryPlace(Vector3 heroPos, float side, float distance, float groundProbe,
                              int terrain, out Vector3 spot)
        {
            spot = heroPos;

            float reach = distance;
            if (terrain != 0)
            {
                // Sweep out from Hornet. A wall in the way caps how far the spawn can be, which
                // is what stops it appearing through geometry or in the next room along.
                var wall = Physics2D.Raycast(heroPos, new Vector2(side, 0f), distance, terrain);
                if (wall.collider != null)
                {
                    reach = wall.distance - 0.6f;
                    if (reach < 1.0f) return false; // flush against a wall on this side
                }
            }

            var candidate = new Vector3(heroPos.x + side * reach, heroPos.y, heroPos.z);
            if (!InSceneBounds(candidate)) return false;

            if (terrain != 0)
            {
                var ground = Physics2D.Raycast(candidate + Vector3.up * 1f, Vector2.down,
                                               groundProbe + 1f, terrain);
                // No floor within reach means a pit or the open air below - not a place to stand.
                if (ground.collider == null) return false;
                candidate.y = ground.point.y + 0.5f;

                // And the resulting spot must not itself be buried in something solid.
                if (Physics2D.OverlapCircle(candidate, 0.4f, terrain) != null) return false;
            }

            if (!InSceneBounds(candidate)) return false;

            spot = candidate;
            return true;
        }

        private static int TerrainMask()
        {
            try
            {
                int mask = LayerMask.GetMask("Terrain", "Soft Terrain");
                return mask;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// Keeps a spawn inside the room the game says it has.
        /// </summary>
        private static bool InSceneBounds(Vector3 p)
        {
            try
            {
                var gm = GameManager.instance;
                if (gm == null || gm.sceneWidth <= 0f || gm.sceneHeight <= 0f) return true;

                const float margin = 1.5f;
                return p.x > margin && p.x < gm.sceneWidth - margin
                    && p.y > margin && p.y < gm.sceneHeight - margin;
            }
            catch (Exception)
            {
                // No GameManager to ask - do not block the spawn over it.
                return true;
            }
        }

        /// <summary>Removes everything this spawner created. Used when the room changes.</summary>
        public int ClearAll()
        {
            int n = 0;
            foreach (var go in _spawned)
            {
                if (go == null) continue;
                UnityEngine.Object.Destroy(go);
                n++;
            }
            _spawned.Clear();
            return n;
        }

        /// <summary>Forgets the list without destroying anything, for a scene that unloaded itself.</summary>
        public void Forget() => _spawned.Clear();
    }
}
