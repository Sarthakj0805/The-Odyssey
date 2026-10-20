using System;

namespace Odyssey.Telemetry
{
    /// <summary>
    /// How the player did in one level segment. Produced by the in-game <see cref="TelemetryTracker"/>
    /// and, during training, by the simulated player, so the Director sees the same data in both.
    /// </summary>
    [Serializable]
    public struct SegmentTelemetry
    {
        public int SegmentIndex;    // 0-based index of the segment just played (-1 = none yet)
        public int TotalSegments;
        public int Difficulty;      // 0 easy, 1 medium, 2 hard - what the segment was played on
        public float HpLost;        // HP lost during the segment (including HP lost before a death)
        public float MaxHp;
        public float Seconds;       // time spent in the segment
        public float ParSeconds;    // designer "par" time for a segment
        public int Deaths;          // deaths during the segment

        public float HpLostFrac => MaxHp > 0 ? HpLost / MaxHp : 0f;
        public float TimeRatio => ParSeconds > 0 ? Seconds / ParSeconds : 1f;

        /// <summary>Observation used before the first segment (no data yet): medium, on target.</summary>
        public static SegmentTelemetry Neutral(int totalSegments, float maxHp, float par) => new SegmentTelemetry
        {
            SegmentIndex = -1, TotalSegments = totalSegments, Difficulty = 1,
            HpLost = 0.3f * maxHp, MaxHp = maxHp, Seconds = par, ParSeconds = par, Deaths = 0
        };

        public override string ToString() =>
            $"seg {SegmentIndex + 1}/{TotalSegments} diff {Difficulty} hpLost {HpLostFrac:P0} time x{TimeRatio:0.00} deaths {Deaths}";
    }
}
