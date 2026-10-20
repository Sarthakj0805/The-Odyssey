using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Odyssey.Telemetry
{
    /// <summary>
    /// Records live telemetry for the current segment. The run manager opens and closes segments;
    /// the player reports damage taken and deaths. Each closed segment is also appended to
    /// telemetry.csv in persistentDataPath (real playtest data).
    /// </summary>
    public sealed class TelemetryTracker
    {
        private SegmentTelemetry _seg;
        private float _startTime;

        public bool IsOpen { get; private set; }
        public SegmentTelemetry Current => _seg;
        public string CsvPath { get; } = Path.Combine(Application.persistentDataPath, "telemetry.csv");
        public string DirectorName { get; set; } = "";

        public void BeginSegment(int index, int total, int difficulty, float maxHp, float par)
        {
            _seg = new SegmentTelemetry { SegmentIndex = index, TotalSegments = total, Difficulty = difficulty, MaxHp = maxHp, ParSeconds = par };
            _startTime = Time.time;
            IsOpen = true;
        }

        public void ReportDamageTaken(float amount) { if (IsOpen) _seg.HpLost += amount; }
        public void ReportDeath() { if (IsOpen) _seg.Deaths++; }

        public SegmentTelemetry EndSegment(float maxHp)
        {
            _seg.Seconds = Time.time - _startTime;
            _seg.MaxHp = maxHp;
            IsOpen = false;
            AppendCsv(_seg);
            return _seg;
        }

        private void AppendCsv(in SegmentTelemetry t)
        {
            try
            {
                bool header = !File.Exists(CsvPath);
                using var w = new StreamWriter(CsvPath, true);
                if (header) w.WriteLine("utc,director,segment,difficulty,hp_lost_frac,time_ratio,deaths");
                w.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0:o},{1},{2},{3},{4:0.000},{5:0.000},{6}",
                    DateTime.UtcNow, DirectorName, t.SegmentIndex, t.Difficulty, t.HpLostFrac, t.TimeRatio, t.Deaths));
            }
            catch (Exception e) { Debug.LogWarning("Telemetry CSV write failed: " + e.Message); }
        }
    }
}
