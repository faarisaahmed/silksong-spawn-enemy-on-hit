using System;
using System.Globalization;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpawnEnemyOnHit
{
    /// <summary>
    /// Every time Hornet takes a hit, something else joins the room.
    /// </summary>
    /// <remarks>
    /// The whole challenge is one feedback loop: getting hit makes the room more dangerous, which
    /// makes getting hit more likely. Everything here exists to keep that loop tight and legible
    /// - one enemy per hit so cause and effect stay obvious, and a hard clear on room change so a
    /// doorway is always an escape hatch. That second rule is what stops the loop being merely
    /// unfair: bosses are in the roster, and without a way out a stairwell with a boss in it
    /// would be the end of the run.
    /// </remarks>
    [BepInPlugin(Guid, "Spawn Enemy On Hit", "1.0.1")]
    public sealed class SpawnOnHitPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.faaris.spawnenemyonhit";

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<KeyCode> _toggleKey;
        private ConfigEntry<KeyCode> _hudKey;
        private ConfigEntry<int> _perHit;
        private ConfigEntry<int> _maxActive;
        private ConfigEntry<int> _maxEnemyHp;
        private ConfigEntry<float> _minDistance;
        private ConfigEntry<float> _maxDistance;
        private ConfigEntry<float> _groundProbe;
        private ConfigEntry<float> _cooldown;
        private ConfigEntry<bool> _clearOnRoomChange;
        private ConfigEntry<float> _harvestInterval;

        private readonly Spawner _spawner = new Spawner();
        private readonly System.Random _rng = new System.Random();

        private int _lastHealth = -1;
        private string _lastScene = "";
        private float _lastSpawnTime = -999f;
        private float _lastHarvestTime = -999f;
        private int _hitsTaken;
        private int _spawnedTotal;
        private string _lastSpawnName = "-";
        private bool _hudVisible = true;
        private GUIStyle _style;

        private void Awake()
        {
            _enabled = Config.Bind("SpawnEnemyOnHit", "Enabled", true,
                "Master switch. Turning this off leaves the game completely untouched.");
            _toggleKey = Config.Bind("SpawnEnemyOnHit", "ToggleKey", KeyCode.F4,
                "Turns the challenge on and off mid-run.");
            _hudKey = Config.Bind("SpawnEnemyOnHit", "HudKey", KeyCode.F3,
                "Shows or hides the counter.");

            _perHit = Config.Bind("Spawning", "EnemiesPerHit", 1,
                "How many enemies one hit summons.");
            _maxActive = Config.Bind("Spawning", "MaxActiveSpawns", 25,
                "Ceiling on how many summoned enemies can exist at once. This is a safety rail " +
                "against a room becoming a slideshow, not a difficulty setting.");
            _maxEnemyHp = Config.Bind("Spawning", "MaxEnemyHp", 0,
                "Skip anything with more health than this when picking. 0 means no limit, which " +
                "is what puts bosses in the pool. Set it to something like 200 if a boss in a " +
                "corridor stops being funny.");
            _minDistance = Config.Bind("Spawning", "MinDistance", 2.5f,
                "Closest an enemy can appear, in world units, and the distance tried first. Much " +
                "below this it can land a hit before you have registered that it exists.");
            _maxDistance = Config.Bind("Spawning", "MaxDistance", 5f,
                "Furthest an enemy can appear. Kept deliberately short so spawns land next to you " +
                "rather than across the room; placement only reaches for the larger distances when " +
                "the closer ones are blocked by geometry.");
            _groundProbe = Config.Bind("Spawning", "GroundProbeDistance", 30f,
                "How far below the spawn point to look for a floor. Without this an enemy summoned " +
                "while you are mid-jump is left standing in mid-air.");
            _cooldown = Config.Bind("Spawning", "CooldownSeconds", 0.35f,
                "Minimum gap between spawns. Stops a single damage event that ticks health twice " +
                "from counting as two hits.");
            _clearOnRoomChange = Config.Bind("Spawning", "ClearOnRoomChange", true,
                "Wipe summoned enemies when you leave the room. Leaving this on is what keeps a " +
                "doorway an escape hatch and stops any room becoming impassable.");

            _harvestInterval = Config.Bind("Spawning", "HarvestIntervalSeconds", 3f,
                "How often to re-scan the room for enemy types worth banking. It repeats rather " +
                "than running once per room because pooled enemies do not exist yet when a room " +
                "loads, and some rooms only populate after a trigger.");

            SceneManager.activeSceneChanged += OnSceneChanged;
            Logger.LogInfo($"Spawn Enemy On Hit ready - {_toggleKey.Value} toggles, {_hudKey.Value} hides the counter.");
        }

        private void OnDestroy() => SceneManager.activeSceneChanged -= OnSceneChanged;

        private void OnSceneChanged(Scene from, Scene to)
        {
            // The old room's objects are already gone with it, so this only drops the bookkeeping.
            _spawner.Forget();
            EnemyBank.Prune();
            _lastScene = to.name;
            // Health reads as 0 across a transition; re-baseline or the first frame back looks
            // like a hit for the entire health bar.
            _lastHealth = -1;
        }

        private void Update()
        {
            if (Input.GetKeyDown(_hudKey.Value)) _hudVisible = !_hudVisible;
            if (Input.GetKeyDown(_toggleKey.Value))
            {
                _enabled.Value = !_enabled.Value;
                Logger.LogInfo(_enabled.Value ? "hit-spawning on" : "hit-spawning off");
            }

            if (!_enabled.Value) return;

            string scene = CurrentScene();
            if (scene != _lastScene)
            {
                _lastScene = scene;
                if (_clearOnRoomChange.Value) _spawner.ClearAll();
                _lastHealth = -1;
                _lastHarvestTime = -999f;
            }

            MaybeHarvest();
            CheckForHit();
        }

        /// <summary>
        /// Sweeps the room for new enemy types, occasionally rather than constantly.
        /// </summary>
        /// <remarks>
        /// A harvest is two FindObjectsByType sweeps over every object in the scene, and for
        /// placed enemies it instantiates a copy of anything it has not seen. That is fine a
        /// couple of times per room and ruinous sixty times a second, which is what calling it
        /// straight from Update would have meant.
        ///
        /// It still has to repeat rather than run once on load: pooled enemies do not exist as
        /// instances when the room appears, and some rooms only populate after a trigger, so a
        /// single sweep at load time would miss them. A few seconds apart catches those without
        /// the cost ever being noticeable.
        /// </remarks>
        private void MaybeHarvest()
        {
            if (Time.unscaledTime - _lastHarvestTime < _harvestInterval.Value) return;
            _lastHarvestTime = Time.unscaledTime;
            EnemyBank.HarvestActiveScene(m => Logger.LogInfo(m));
        }

        /// <summary>
        /// Watches the health bar rather than the damage call.
        /// </summary>
        /// <remarks>
        /// Hornet loses health down several different paths - contact, projectiles, spikes, a bad
        /// landing - and hooking one of them would silently miss the others. Health going down is
        /// the one thing all of them agree on, so that is what gets watched. The trade is that it
        /// cannot tell a spike from a sword, which for a challenge run is arguably correct: a hit
        /// is a hit.
        /// </remarks>
        private void CheckForHit()
        {
            int hp = HeroHealth();
            if (hp < 0) return;

            if (_lastHealth < 0)
            {
                _lastHealth = hp;
                return;
            }

            if (hp < _lastHealth && hp > 0)
            {
                int lost = _lastHealth - hp;
                _lastHealth = hp;
                OnHornetHit(lost);
                return;
            }

            _lastHealth = hp;
        }

        private void OnHornetHit(int masksLost)
        {
            if (Time.unscaledTime - _lastSpawnTime < _cooldown.Value) return;
            _lastSpawnTime = Time.unscaledTime;
            _hitsTaken++;

            if (_spawner.ActiveCount >= _maxActive.Value)
            {
                Logger.LogInfo($"hit taken, but {_maxActive.Value} summons are already out - holding");
                return;
            }

            for (int i = 0; i < Mathf.Max(1, _perHit.Value); i++)
            {
                var pick = EnemyBank.PickRandom(_rng, _maxEnemyHp.Value);
                if (pick == null)
                {
                    Logger.LogInfo("hit taken, but no enemy types are known yet - walk past a few first");
                    return;
                }

                var go = _spawner.SpawnNearHero(pick, _minDistance.Value, _maxDistance.Value,
                                                _groundProbe.Value, m => Logger.LogWarning(m));
                if (go != null)
                {
                    _spawnedTotal++;
                    _lastSpawnName = pick.Id;
                    Logger.LogInfo($"hit for {masksLost} - summoned {pick.Id} ({pick.TemplateHp} hp)");
                }
            }
        }

        private static string CurrentScene()
        {
            try
            {
                var gm = GameManager.instance;
                if (gm != null && !string.IsNullOrEmpty(gm.sceneName)) return gm.sceneName;
            }
            catch (Exception) { }
            return SceneManager.GetActiveScene().name;
        }

        private static int HeroHealth()
        {
            try
            {
                var pd = PlayerData.instance;
                return pd != null ? pd.health : -1;
            }
            catch (Exception) { return -1; }
        }

        private void OnGUI()
        {
            if (!_hudVisible) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.UpperLeft };
                _style.normal.textColor = Color.white;
            }

            var rect = new Rect(12f, 12f, 380f, 92f);
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, rect.height - 16f));
            GUILayout.Label($"Spawn On Hit: {(_enabled.Value ? "ON" : "off")}" +
                            $"    enemies known: {EnemyBank.Count}", _style);
            GUILayout.Label($"hits taken {_hitsTaken}    summoned {_spawnedTotal}    in room {_spawner.ActiveCount}", _style);
            GUILayout.Label($"last: {_lastSpawnName}", _style);
            GUILayout.Label($"{_toggleKey.Value} toggle   {_hudKey.Value} hide", _style);
            GUILayout.EndArea();
        }
    }
}
