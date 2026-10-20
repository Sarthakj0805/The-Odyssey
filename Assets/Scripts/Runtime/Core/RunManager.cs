using System.Collections;
using System.Collections.Generic;
using Odyssey.Combat;
using Odyssey.Dialogue;
using Odyssey.Director;
using Odyssey.Director.ML;
using Odyssey.Enemies;
using Odyssey.LevelGen;
using Odyssey.Level;
using Odyssey.Modifiers;
using Odyssey.Player;
using Odyssey.Telemetry;
using Odyssey.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Odyssey.Core
{
    /// <summary>
    /// The Level01 game loop:
    ///   intro chunk + opening dialogue
    ///   for each segment: Director picks a difficulty -> a chunk is added -> telemetry is recorded
    ///   at the shrine gate: telemetry closes, Director decides the next difficulty, player picks a boon
    ///   after the last segment: ship chunk -> ending dialogue -> end screen.
    /// Dying respawns the player at the segment's checkpoint; losing every life ends the run.
    /// </summary>
    public class RunManager : MonoBehaviour
    {
        [SerializeField] private int seed = 0;   // 0 = random each run

        private GameDatabase _db;
        private RunModifierRegistry _boons;
        private TelemetryTracker _telemetry;
        private IDirector _director;
        private string _directorLabel;
        private LevelAssembler _level;
        private PlayerController _player;
        private CameraFollow _camera;
        private Hud _hud;
        private BoonChoicePanel _boonPanel;
        private DialogueRunner _dialogue;
        private PauseMenu _pause;
        private System.Random _rng;

        private ChunkInstance _currentChunk;
        private ChunkDefinition _lastCombatChunk;
        private int _segment;          // segment being played (== Segments once on the ship chunk)
        private int _difficulty = 1;   // difficulty of the segment being played
        private SegmentTelemetry _lastTelemetry;
        private int _deaths, _kills;
        private float _runStart;
        private bool _ending;
        private readonly List<string> _decisionLog = new List<string>();

        // ------------------------------------------------------------------ setup
        private void Awake()
        {
            Time.timeScale = 1f;
            Physics2D.queriesHitTriggers = false;
            Layers.ConfigureCollisionMatrix();
            _db = GameDatabase.Load();
            _rng = new System.Random(seed != 0 ? seed : System.Environment.TickCount);
            _boons = new RunModifierRegistry();
            _telemetry = new TelemetryTracker();

            _hud = Hud.Create();
            _boonPanel = BoonChoicePanel.Create();
            _dialogue = DialogueRunner.Create();
            _pause = PauseMenu.Create();
            _pause.CanPause = () => !_dialogue.IsPlaying && !_boonPanel.IsOpen && !_ending;

            CreateDirector();
            _telemetry.DirectorName = _directorLabel;
        }

        private void CreateDirector()
        {
            switch (GameSession.Director)
            {
                case DirectorKind.Fixed: _director = new FixedDirector(); _directorLabel = "Fixed"; break;
                case DirectorKind.Heuristic: _director = new HeuristicDirector(); _directorLabel = "Heuristic"; break;
                default:
                    var agent = FindFirstObjectByType<DirectorAgent>();
                    if (agent == null) { _director = new HeuristicDirector(); _directorLabel = "Heuristic (no DirectorAgent in scene)"; break; }
                    var rl = new RLDirector(agent);
                    _director = rl;
                    _directorLabel = rl.HasModel ? "RL (PPO policy)" : "RL (no model yet -> rule-based fallback)";
                    break;
            }
        }

        private void Start()
        {
            CreateBackdrop();
            _level = new GameObject("Level").AddComponent<LevelAssembler>();
            _level.Init(null, _db.MeleeEnemy, _db.RangedEnemy);
            _level.EnemySpawned += OnEnemySpawned;

            var intro = _level.Append(_db.Chunks.Intro, 0, _rng, -1);
            SpawnPlayer(intro.PlayerStart ?? intro.Checkpoint);
            _level.SetTarget(_player.transform);
            _currentChunk = intro;

            _camera = Camera.main.gameObject.AddComponent<CameraFollow>();
            _camera.Target = _player.transform;
            _camera.MinX = intro.StartX + 8f;
            _camera.Snap();

            // first decision has no telemetry yet -> neutral observation
            _lastTelemetry = SegmentTelemetry.Neutral(_db.Segments, _player.MaxHealthStat, _db.ParSecondsPerSegment);
            _director.ResetRun();
            DecideAndAppend(0);

            _runStart = Time.time;
            RefreshHud();
            _player.Input.Enabled = false;
            _dialogue.Play(_db.Opening, () =>
            {
                _player.Input.Enabled = true;
                StartSegmentTelemetry();
                _hud.Toast("Reach the shrine at the end of each segment");
            });
        }

        private void SpawnPlayer(Vector2 at)
        {
            var go = new GameObject("Odysseus");
            go.transform.position = at;
            go.AddComponent<Rigidbody2D>();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<Health>();
            _player = go.AddComponent<PlayerController>();
            _player.Init(_db.Player, _boons);
            _player.DamageTaken += amount => { _telemetry.ReportDamageTaken(amount); RefreshHud(); };
            _player.Died += OnPlayerDied;
            _player.Health.Model.Changed += (c, m) => _hud.SetHealth(c, m);
            _boons.Changed += () => _hud.SetBoons(_boons);
        }

        private static void CreateBackdrop()
        {
            var root = new GameObject("Backdrop").transform;
            Placeholder.AddSprite(root, "Sea", null, new Color(0.15f, 0.35f, 0.55f), new Vector2(2000f, 14f), new Vector2(900f, -7f), -20);
            Placeholder.AddSprite(root, "Sun", Placeholder.Circle, new Color(1f, 0.92f, 0.7f), new Vector2(3f, 3f), new Vector2(14f, 11f), -30)
                .gameObject.AddComponent<ParallaxFollow>().Factor = 0.9f;
        }

        // ------------------------------------------------------------------ segments
        /// <summary>Ask the Director for a difficulty and add a matching chunk for segment <paramref name="index"/>.</summary>
        private void DecideAndAppend(int index)
        {
            _difficulty = Odyssey.Director.Difficulty.Clamp(_director.Decide(_lastTelemetry));
            LogDecision(index);
            var def = ChunkSelector.Pick(_db.Chunks.Combat, _difficulty, _rng, _lastCombatChunk);
            _lastCombatChunk = def;
            var chunk = _level.Append(def, _difficulty, _rng, index);
            chunk.Gate.Reached += OnGateReached;
            _nextChunk = chunk;
        }
        private ChunkInstance _nextChunk;

        private void StartSegmentTelemetry()
        {
            _currentChunk = _nextChunk;
            _telemetry.BeginSegment(_segment, _db.Segments, _difficulty, _player.MaxHealthStat, _db.ParSecondsPerSegment);
            RefreshHud();
        }

        private void OnGateReached(SegmentGate gate)
        {
            if (_ending) return;
            _lastTelemetry = _telemetry.EndSegment(_player.Health.Model.Max);
            _player.Health.Model.Heal(_player.Health.Model.Max * _db.ShrineHeal);   // shrine heal

            var offer = BoonOffer.Draw(_db.Boons, _boons, 3, _rng);
            if (offer.Count == 0) { AfterShrine(gate); return; }
            _boonPanel.Show($"Shrine of the Gods - segment {gate.SegmentIndex + 1} cleared", offer, boon =>
            {
                _boons.Add(boon);
                _hud.Toast($"{boon.God} grants {boon.DisplayName}");
                AfterShrine(gate);
            });
        }

        private void AfterShrine(SegmentGate gate)
        {
            _segment++;
            if (_segment < _db.Segments)
            {
                DecideAndAppend(_segment);
                StartSegmentTelemetry();
            }
            else
            {
                var ship = _level.Append(_db.Chunks.Finale, 0, _rng, _segment);
                ship.Exit.Reached += OnVictory;
                _currentChunk = ship;
            }
            gate.Open();
            RefreshHud();
        }

        // ------------------------------------------------------------------ combat events
        private void OnEnemySpawned(EnemyController e)
        {
            e.Died += (enemy, info) =>
            {
                if (info.SourceTeam != Team.Player) return;
                _kills++;
                float heal = _player.HealOnKillStat;
                if (heal > 0f) _player.Health.Model.Heal(heal);
                RefreshHud();
            };
        }

        private void OnPlayerDied()
        {
            if (_ending) return;
            _deaths++;
            _telemetry.ReportDeath();
            RefreshHud();
            if (_deaths >= _db.Lives) { StartCoroutine(EndRun(false, 1.5f)); return; }
            int left = _db.Lives - _deaths;
            _hud.Toast($"Fallen! {left} {(left == 1 ? "life" : "lives")} left");
            StartCoroutine(RespawnAfter(1.2f));
        }

        private IEnumerator RespawnAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (_ending) yield break;
            _player.Respawn(_currentChunk.Checkpoint);
            _camera.Snap();
        }

        private void OnVictory()
        {
            if (_ending) return;
            StartCoroutine(VictoryRoutine());
        }

        private IEnumerator VictoryRoutine()
        {
            _ending = true;
            _player.Input.Enabled = false;
            bool done = false;
            _dialogue.Play(_db.Ending, () => done = true);
            while (!done) yield return null;
            yield return EndRun(true, 0.2f);
        }

        private IEnumerator EndRun(bool victory, float delay)
        {
            _ending = true;
            GameSession.LastResult = new GameSession.RunResult
            {
                Victory = victory, Seconds = Time.time - _runStart, Kills = _kills, Deaths = _deaths,
                Boons = _boons.Picked.Count, SegmentsCleared = Mathf.Min(_segment, _db.Segments), DirectorName = _directorLabel
            };
            yield return new WaitForSecondsRealtime(delay);
            GameSession.Load(GameSession.EndScene);
        }

        // ------------------------------------------------------------------ per frame + HUD
        private void Update()
        {
            if (_player == null) return;
            if (_player.transform.position.y < -8f && _player.Health.IsAlive)   // fell into the sea
                _player.Health.TakeDamage(new DamageInfo(9999f, Vector2.zero, Team.Environment, null));

            var kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame) _hud.ShowDirector = !_hud.ShowDirector;
            if (_hud.ShowDirector) _hud.SetDirector(DirectorText());
        }

        private void LogDecision(int segmentIndex)
        {
            string line = $"segment {segmentIndex + 1} -> {Odyssey.Director.Difficulty.Names[_difficulty]}";
            _decisionLog.Add(line);
            if (_decisionLog.Count > 5) _decisionLog.RemoveAt(0);
            Debug.Log($"[Director:{_directorLabel}] {line}  | based on {_lastTelemetry}");
        }

        private string DirectorText()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"AI Director: {_directorLabel}");
            if (_telemetry.IsOpen)
            {
                var c = _telemetry.Current;
                sb.AppendLine($"now: {Odyssey.Director.Difficulty.Names[_difficulty]}, hp lost {(c.MaxHp > 0 ? c.HpLost / c.MaxHp : 0):P0}, deaths {c.Deaths}");
            }
            if (_lastTelemetry.SegmentIndex >= 0) sb.AppendLine($"last: {_lastTelemetry}");
            foreach (var l in _decisionLog) sb.AppendLine(l);
            return sb.ToString();
        }

        private void RefreshHud()
        {
            if (_hud == null || _player == null) return;
            _hud.SetHealth(_player.Health.Model.Current, _player.Health.Model.Max);
            _hud.SetInfo(_segment, _db.Segments, _db.Lives - _deaths, _kills);
            _hud.SetBoons(_boons);
        }
    }
}
