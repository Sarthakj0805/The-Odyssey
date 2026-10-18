using System;

namespace Odyssey.Telemetry
{
    /// <summary>
    /// Player-performance summary for one level segment. Produced by the in-game
    /// <see cref="TelemetryTracker"/> and, during training, by the simulated player,
    /// so the AI Director sees the same features in both worlds.
    /// </summary>
    [Serializable]
    public struct SegmentTelemetry
    {
        public int SegmentIndex;      // 0-based index of the segment just played
        public int TotalSegments;
        public float HpLost;          // raw HP lost during the segment (including HP lost before a death)
        public float MaxHp;
        public float HpAtEnd;         // HP when the segment ended
        public float Seconds;         // time spent in the segment
        public float ParSeconds;      // designer "par" time for the segment
        public int Deaths;            // deaths during the segment
        public int Attacks;           // swings
        public int Hits;              // swings that connected
        public float DamageDealt;
        public float EnemyHpSpawned;  // total enemy HP spawned in the segment
        public int BoonsHeld;
        public int LastDifficulty;    // 0..2 difficulty the segment was played on
        public int LastDensity;       // 0..2

        public float HpLostFrac => MaxHp > 0 ? HpLost / MaxHp : 0f;
        public float HpEndFrac => MaxHp > 0 ? HpAtEnd / MaxHp : 0f;
        public float TimeRatio => ParSeconds > 0 ? Seconds / ParSeconds : 1f;
        public float HitRate => Attacks > 0 ? (float)Hits / Attacks : 0f;
        public float ClearRatio => EnemyHpSpawned > 0 ? Math.Min(1f, DamageDealt / EnemyHpSpawned) : 1f;

        /// <summary>Observation used before the first segment (no data yet): "on target".</summary>
        public static SegmentTelemetry Neutral(int totalSegments, float maxHp, float par) => new SegmentTelemetry
        {
            SegmentIndex = -1, TotalSegments = totalSegments, HpLost = 0.3f * maxHp, MaxHp = maxHp,
            HpAtEnd = maxHp, Seconds = par, ParSeconds = par, Deaths = 0, Attacks = 10, Hits = 6,
            DamageDealt = 1f, EnemyHpSpawned = 1f, BoonsHeld = 0, LastDifficulty = 1, LastDensity = 1
        };

        public override string ToString() =>
            $"seg {SegmentIndex + 1}/{TotalSegments} hpLost {HpLostFrac:P0} time x{TimeRatio:0.00} deaths {Deaths} hit {HitRate:P0}";
    }
}
