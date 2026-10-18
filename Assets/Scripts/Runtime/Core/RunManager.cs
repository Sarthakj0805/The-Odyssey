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
    /// Level01 game loop (the "game loop <-> telemetry <-> Director" box in the README diagram):
    ///   intro chunk + opening dialogue
    ///   for each segment: Director decides -> assembler appends chunk -> telemetry recorded
    ///                     -> shrine gate: telemetry closed, Director decides next, boon offered
    ///   finale chunk (Polyphemus) -> ship -> end screen.
    /// Death: respawn at the segment checkpoint; running out of lives ends the run.
    /// </summary>
    public class RunManager : MonoBehaviour
    {
        [SerializeField] private int seed = 0;   // 0 = random each run

        private GameDatabase _db;
        private RunModifierRegistry _registry;
        private TelemetryTracker _telemetry;
        private IDirector _director;
        private string _directorLabel;
        private LevelAssembler _assembler;
        private PlayerController _player;
        private CameraFollow _camera;
        private Hud _hud;
        private BoonChoicePanel _boonPanel;
        private DialogueRunner _dialogue;
        private PauseMenu _pause;
        private System.Random _rng;

        private ChunkInstance _currentChunk;
        private ChunkDefinition _lastCombatDef;
        private DirectorDecision _decision;
        private SegmentTelemetry _lastTelemetry;
        private int _segment;        // index of the segment being played (== Segments during the finale)
        private int _deaths, _kills;
        private float _runStart;
        private bool _ending;
        private readonly List<string> _decisionLog = new List<string>();

        private void Awake()
        {
            Time.timeScale = 1f;
            Physics2D.queriesHitTriggers = false;
            Layers.ConfigureCollisionMatrix();
            _db = GameDatabase.Load();
            _rng = new System.Random(seed != 0 ? seed : System.Environment.TickCount);
            _registry = new RunModifierRegistry();
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
                    if (agent != null)
                    {
                        var rl = new RLDirector(agent);
                        _director = rl;
                        _directorLabel = rl.HasModel ? "RL (PPO policy)" : "RL (no model assigned -> heuristic fallback)";
                    }
                    else
                    {
                        _director = new HeuristicDirector();
                        _directorLabel = "Heuristic (DirectorAgent missing from scene)";
                    }
                    break;
            }
        }

        private void Start()
        {
            CreateBackdrop();
            _assembler = new GameObject("Level").AddComponent<LevelAssembler>();
            _assembler.Init(null, _db.MeleeEnemy, _db.RangedEnemy, _db.Boss);
            _assembler.EnemySpawned += OnEnemySpawned;

            var intro = _assembler.Append(_db.Chunks.Intro, 0, 0, _rng, -1);
            SpawnPlayer(intro.PlayerStart ?? intro.Checkpoint);
            _assembler.SetTarget(_player.transform);
            _currentChunk = intro;
            HookChunk(intro);

            _camera = Camera.main.gameObject.AddComponent<CameraFollow>();
            _camera.Target = _player.transform;
            _camera.MinX = intro.StartX + 8f;
            _camera.Snap();

            // first Director decision: no telemetry yet -> neutral observation
            _lastTelemetry = SegmentTelemetry.Neutral(_db.Segments, _player.MaxHealthStat, _db.ParSecondsPerSegment);
            _director.ResetRun();
            _decision = _director.Decide(_lastTelemetry);
            LogDecision(-1);
            _segment = 0;
            AppendSegment(_segment, _decision);

            _runStart = Time.time;
            RefreshHud();
            _player.Input.Enabled = false;
            _dialogue.Play(_db.Opening, () =>
            {
                _player.Input.Enabled = true;
                BeginTelemetry();
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
            _player.Init(_db.Player, _registry);
            _player.Attacked += _telemetry.ReportAttack;
            _player.HitLanded += (t, info) => _telemetry.ReportHit(info.Amount);
            _player.DamageTaken += amount => { _telemetry.ReportDamageTaken(amount); RefreshHud(); };
            _player.Died += OnPlayerDied;
            _player.Health.Model.Changed += (c, m) => _hud.SetHealth(c, m);
            _registry.Changed += () => _hud.SetBoons(_registry);
        }

        private static void CreateBackdrop()
        {
            var root = new GameObject("Backdrop").transform;
            Placeholder.AddSprite(root, "Sea", null, new Color(0.15f, 0.35f, 0.55f), new Vector2(2000f, 14f), new Vector2(900f, -7f), -20);
            Placeholder.AddSprite(root, "Sun", Placeholder.Circle, new Color(1f, 0.92f, 0.7f), new Vector2(3f, 3f), new Vector2(14f, 11f), -30)
                .gameObject.AddComponent<ParallaxFollow>().Factor = 0.9f;
        }

        // ------------------------------------------------------------------ segments
        private void AppendSegment(int index, DirectorDecision d)
        {
            var def = ChunkSelector.Pick(_db.Chunks.Combat, d.Difficulty, _rng, _lastCombatDef);
            _lastCombatDef = def;
            var inst = _assembler.Append(def, d.Difficulty, d.Density, _rng, index);
            HookChunk(inst);
            _pendingChunk = inst;
        }
        private ChunkInstance _pendingChunk;

        private void HookChunk(ChunkInstance inst)
        {
            if (inst.Gate != null) inst.Gate.Reached += OnGateReached;
            if (inst.Exit != null) inst.Exit.Reached += OnVictory;
            if (inst.Trigger != null) inst.Trigger.Fired += seq =>
            {
                _player.Input.Enabled = false;
                _dialogue.Play(seq, () => _player.Input.Enabled = true);
            };
            if (inst.Boss != null && inst.Exit != null)
            {
                var exit = inst.Exit;
                inst.Boss.Died += (e, info) => { exit.Unlock(); _hud.Toast("Polyphemus falls! Run to the ship!"); };
            }
        }

        private void BeginTelemetry()
        {
            _currentChunk = _pendingChunk ?? _currentChunk;
            var c = _currentChunk;
            _telemetry.BeginSegment(_segment, _db.Segments, _player.MaxHealthStat, _db.ParSecondsPerSegment,
                _registry.Picked.Count, _decision.Difficulty, _decision.Density, c.EnemyHpSpawned);
            RefreshHud();
        }

        private void OnGateReached(SegmentGate gate)
        {
            if (_ending) return;
            var t = _telemetry.EndSegment(_player.Health.Model.Current, _player.Health.Model.Max);
            _lastTelemetry = t;
            int next = _segment + 1;

            // shrine: heal, then the Director decides the next segment (incl. which boon pool to offer)
            _player.Health.Model.Heal(_player.Health.Model.Max * _db.ShrineHeal);
            bool strong = false;
            if (next < _db.Segments)
            {
                _decision = _director.Decide(t);
                LogDecision(_segment);
                strong = _decision.StrongBoons;
            }

            var offer = BoonOffer.Draw(_db.Boons, _registry, strong, 3, _rng);
            string title = $"Shrine of the Gods — segment {gate.SegmentIndex + 1} cleared";
            if (offer.Count == 0) { ContinueAfterShrine(gate, next); return; }
            _boonPanel.Show(title, offer, boon =>
            {
                _registry.Add(boon);
                _hud.Toast($"{boon.God} grants {boon.DisplayName}");
                ContinueAfterShrine(gate, next);
            });
        }

        private void ContinueAfterShrine(SegmentGate gate, int next)
        {
            _segment = next;
            if (next < _db.Segments)
            {
                AppendSegment(next, _decision);
                BeginTelemetry();
            }
            else
            {
                var finale = _assembler.Append(_db.Chunks.Finale, 2, 0, _rng, next);
                HookChunk(finale);
                _pendingChunk = finale;
                _currentChunk = finale;
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
            _hud.Toast($"Fallen! {_db.Lives - _deaths} {(_db.Lives - _deaths == 1 ? "life" : "lives")} left");
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
            StartCoroutine(EndRunWithEpilogue());
        }

        private IEnumerator EndRunWithEpilogue()
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
                Boons = _registry.Picked.Count, SegmentsCleared = Mathf.Min(_segment, _db.Segments), DirectorName = _directorLabel
            };
            yield return new WaitForSecondsRealtime(delay);
            GameSession.Load(GameSession.EndScene);
        }

        // ------------------------------------------------------------------ per-frame
        private void Update()
        {
            if (_player == null) return;
            if (_player.transform.position.y < -8f && _player.Health.IsAlive) // fell into the sea
                _player.Health.TakeDamage(new DamageInfo(9999f, Vector2.zero, Team.Environment, null));

            var kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame) _hud.ShowDirector = !_hud.ShowDirector;
            if (_hud.ShowDirector) _hud.SetDirector(DirectorText());
        }

        private void LogDecision(int afterSegment)
        {
            _decisionLog.Add($"{(afterSegment < 0 ? "start" : $"after seg {afterSegment + 1}")}: {_decision}");
            if (_decisionLog.Count > 5) _decisionLog.RemoveAt(0);
            Debug.Log($"[Director:{_directorLabel}] {_decisionLog[_decisionLog.Count - 1]}  | obs {_lastTelemetry}");
        }

        private string DirectorText()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"AI Director: {_directorLabel}");
            if (_telemetry.IsOpen)
            {
                var c = _telemetry.Current;
                sb.AppendLine($"live: hp lost {(c.MaxHp > 0 ? c.HpLost / c.MaxHp : 0):P0}  hits {c.Hits}/{c.Attacks}  deaths {c.Deaths}");
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
            _hud.SetBoons(_registry);
        }
    }
}
