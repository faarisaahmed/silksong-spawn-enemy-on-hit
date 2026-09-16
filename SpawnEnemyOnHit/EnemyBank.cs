using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpawnEnemyOnHit
{
    /// <summary>One enemy type the mod knows how to produce.</summary>
    public sealed class BankedEnemy
    {
        public string Id;
        public GameObject Template;

        /// <summary>Health of the template, used to tell a boss from a crawler.</summary>
        public int TemplateHp;

        /// <summary>True when the template is a prefab asset rather than a copy we keep alive.</summary>
        public bool IsPrefab;
    }

    /// <summary>
    /// The roster of everything the run has run into, built up room by room.
    /// </summary>
    /// <remarks>
    /// Finding enemies in a Silksong room is not the obvious job it looks like. Only a minority
    /// are placed in the scene as objects; most are *pooled* - the room carries a
    /// PersonalObjectPool listing prefabs and creates instances from it at runtime. A sweep for
    /// HealthManagers therefore finds almost nothing in a freshly loaded room. This is a lesson
    /// already paid for in GauntletMod, where every imported room banked zero until the pools
    /// were read instead, so it is taken as given here rather than rediscovered.
    ///
    /// The two sources also differ in how long they survive. A pooled prefab is an asset: it
    /// outlives the room and can be held onto directly. An enemy placed in the scene is
    /// destroyed when that scene unloads, so keeping one means taking an inactive copy and
    /// parking it outside the scene - otherwise the roster quietly empties itself every time
    /// you walk through a door.
    /// </remarks>
    public static class EnemyBank
    {
        private static readonly Dictionary<string, BankedEnemy> _byId = new Dictionary<string, BankedEnemy>();
        private static GameObject _holder;

        public static int Count => _byId.Count;
        public static IEnumerable<BankedEnemy> All => _byId.Values;

        /// <summary>Sweeps the live scene and banks anything new.</summary>
        public static int HarvestActiveScene(Action<string> log)
        {
            int added = 0;
            added += HarvestPools(log);
            added += HarvestPlaced(log);
            if (added > 0) log($"banked {added} new enemy type(s); roster now {_byId.Count}");
            return added;
        }

        /// <summary>
        /// Reads the prefabs a room would have spawned, without spawning any of them.
        /// </summary>
        private static int HarvestPools(Action<string> log)
        {
            int added = 0;
            try
            {
                foreach (var pool in UnityEngine.Object.FindObjectsByType<PersonalObjectPool>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (pool == null || pool.startupPool == null) continue;

                    foreach (var entry in pool.startupPool)
                    {
                        var prefab = entry.prefab;
                        if (prefab == null) continue;

                        var hm = prefab.GetComponentInChildren<HealthManager>(true);
                        if (hm == null) continue;

                        if (TryAdd(prefab.name, prefab, hm.hp, isPrefab: true)) added++;
                    }
                }
            }
            catch (Exception ex)
            {
                log($"pool harvest failed: {ex.Message}");
            }
            return added;
        }

        /// <summary>
        /// Banks enemies that were placed in the room directly, by keeping a copy of each.
        /// </summary>
        private static int HarvestPlaced(Action<string> log)
        {
            int added = 0;
            try
            {
                foreach (var hm in UnityEngine.Object.FindObjectsByType<HealthManager>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (hm == null) continue;
                    var go = hm.gameObject;
                    if (IsHeroOwned(go)) continue;
                    if (_byId.ContainsKey(go.name)) continue;

                    // Copy it inactive and park it outside the scene, or this reference dies with
                    // the room. Instantiating while inactive also stops the copy ever running a
                    // frame of its own AI in the holder.
                    bool wasActive = go.activeSelf;
                    go.SetActive(false);
                    GameObject copy;
                    try
                    {
                        copy = UnityEngine.Object.Instantiate(go, Holder.transform);
                    }
                    finally
                    {
                        go.SetActive(wasActive);
                    }

                    copy.name = go.name;
                    copy.SetActive(false);
                    if (TryAdd(go.name, copy, hm.hp, isPrefab: false)) added++;
                }
            }
            catch (Exception ex)
            {
                log($"scene harvest failed: {ex.Message}");
            }
            return added;
        }

        private static bool TryAdd(string id, GameObject template, int hp, bool isPrefab)
        {
            if (string.IsNullOrEmpty(id) || template == null) return false;
            if (_byId.ContainsKey(id)) return false;

            _byId[id] = new BankedEnemy { Id = id, Template = template, TemplateHp = hp, IsPrefab = isPrefab };
            return true;
        }

        private static GameObject Holder
        {
            get
            {
                if (_holder == null)
                {
                    _holder = new GameObject("SpawnOnHit_Templates");
                    _holder.SetActive(false);
                    UnityEngine.Object.DontDestroyOnLoad(_holder);
                }
                return _holder;
            }
        }

        private static bool IsHeroOwned(GameObject go)
        {
            var hero = HeroController.instance;
            return hero != null && go.transform.IsChildOf(hero.transform);
        }

        /// <summary>Picks one at random, optionally skipping anything too big to be fair.</summary>
        public static BankedEnemy PickRandom(System.Random rng, int maxHp)
        {
            var pool = new List<BankedEnemy>();
            foreach (var e in _byId.Values)
            {
                if (e.Template == null) continue;
                if (maxHp > 0 && e.TemplateHp > maxHp) continue;
                pool.Add(e);
            }
            if (pool.Count == 0) return null;
            return pool[rng.Next(pool.Count)];
        }

        /// <summary>Drops templates whose object has gone away, so the roster cannot rot.</summary>
        public static void Prune()
        {
            var dead = new List<string>();
            foreach (var kv in _byId)
                if (kv.Value.Template == null) dead.Add(kv.Key);
            foreach (var k in dead) _byId.Remove(k);
        }
    }
}
