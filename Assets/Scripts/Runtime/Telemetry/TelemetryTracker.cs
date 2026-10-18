using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Odyssey.Telemetry
{
    /// <summary>
    /// Collects live player telemetry for the current segment. Gameplay code reports events
    /// (attacks, hits, damage taken, deaths); the run manager opens/closes segments.
    /// Every closed segment is also appended to a CSV in persistentDataPath (real playtest data).
    /// </summary>
    public sealed class TelemetryTracker
    {
        private SegmentTelemetry _seg;
        private bool _open;
        private float _startTime;

        public bool IsOpen => _open;
        public SegmentTelemetry Current => _seg;
        public string CsvPath { get; }
        public string DirectorName { get; set; } = "";

        public TelemetryTracker()
        {
            CsvPath = Path.Combine(Application.persistentDataPath, "telemetry.csv");
        }

        public void BeginSegment(int index, int total, float maxHp, float par, int boonsHeld, int difficulty, int density, float enemyHpSpawned)
        {
            _seg = new SegmentTelemetry
            {
                SegmentIndex = index, TotalSegments = total, MaxHp = maxHp, ParSeconds = par,
                BoonsHeld = boonsHeld, LastDifficulty = difficulty, LastDensity = density, EnemyHpSpawned = enemyHpSpawned
            };
            _startTime = Time.time;
            _open = true;
        }

        public void ReportAttack() { if (_open) _seg.Attacks++; }
        public void ReportHit(float damage) { if (_open) { _seg.Hits++; _seg.DamageDealt += damage; } }
        public void ReportDamageTaken(float amount) { if (_open) _seg.HpLost += amount; }
        public void ReportDeath() { if (_open) _seg.Deaths++; }
        public void AddEnemyHp(float hp) { if (_open) _seg.EnemyHpSpawned += hp; }

        public SegmentTelemetry EndSegment(float hpAtEnd, float maxHp)
        {
            _seg.Seconds = Time.time - _startTime;
            _seg.HpAtEnd = hpAtEnd;
            _seg.MaxHp = maxHp;
            _open = false;
            AppendCsv(_seg);
            return _seg;
        }

        private void AppendCsv(in SegmentTelemetry t)
        {
            try
            {
                bool header = !File.Exists(CsvPath);
                using var w = new StreamWriter(CsvPath, true);
                if (header) w.WriteLine("utc,director,segment,difficulty,density,hp_lost_frac,time_ratio,deaths,hit_rate,clear_ratio,boons");
                var c = CultureInfo.InvariantCulture;
                w.WriteLine(string.Format(c, "{0:o},{1},{2},{3},{4},{5:0.000},{6:0.000},{7},{8:0.000},{9:0.000},{10}",
                    DateTime.UtcNow, DirectorName, t.SegmentIndex, t.LastDifficulty, t.LastDensity, t.HpLostFrac,
                    t.TimeRatio, t.Deaths, t.HitRate, t.ClearRatio, t.BoonsHeld));
            }
            catch (Exception e) { Debug.LogWarning("Telemetry CSV write failed: " + e.Message); }
        }
    }
}
