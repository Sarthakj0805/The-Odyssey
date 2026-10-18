using System;
using Odyssey.Telemetry;

namespace Odyssey.Director.Sim
{
    /// <summary>Parameters of a scripted "bot" player used to train and evaluate the Director.</summary>
    [Serializable]
    public struct SkillProfile
    {
        public string Name;
        public float Skill;   // 0..1, how well the bot fights and dodges
        public float Noise;   // per-segment outcome noise (std-dev)

        public SkillProfile(string name, float skill, float noise) { Name = name; Skill = skill; Noise = noise; }

        public static readonly SkillProfile Novice = new SkillProfile("Novice", 0.35f, 0.08f);
        public static readonly SkillProfile Average = new SkillProfile("Average", 0.60f, 0.08f);
        public static readonly SkillProfile Expert = new SkillProfile("Expert", 0.85f, 0.08f);
        public static readonly SkillProfile[] All = { Novice, Average, Expert };
    }

    /// <summary>
    /// Fast, stochastic model of one rogue-lite run (no rendering, no physics) so the
    /// Director can be trained on hundreds of thousands of segments in minutes.
    ///
    /// challenge c = baseDifficulty[d] * densityFactor[n] / ((skill / 0.6) * power)
    ///   HP lost  ~ 0.35 * c^1.5 + noise   (fraction of max HP)
    ///   time     ~ (0.6 + 0.4c + noise) / (0.8 + 0.4 * skill / 0.6 ... )  (x par time)
    /// HP carries over between segments; shrines (before segments 2..N) heal 15%; a death costs a life and
    /// respawns at full HP; 3 deaths end the run. Boons raise "power" (+5% common, +10% rare).
    /// The constants are hand-tuned assumptions, not fitted to human data (see README limitations).
    /// </summary>
    public sealed class RunSimulator
    {
        public const int SegmentsPerRun = 5;
        public const int MaxDeaths = 3;
        public const float ShrineHeal = 0.15f;
        public const float MaxHp = 100f;
        public const float ParSeconds = 45f;

        private static readonly float[] DifficultyBase = { 0.6f, 1.0f, 1.45f };
        private static readonly float[] DensityFactor = { 0.7f, 1.0f, 1.35f };

        private readonly Random _rng;
        public SkillProfile Profile { get; private set; }
        public int Segment { get; private set; }
        public int DeathsTotal { get; private set; }
        public float HpFrac { get; private set; }
        public float Power { get; private set; }
        public int Boons { get; private set; }
        public bool Finished => Segment >= SegmentsPerRun || DeathsTotal >= MaxDeaths;
        public bool Completed => Segment >= SegmentsPerRun && DeathsTotal < MaxDeaths;

        public RunSimulator(int seed) { _rng = new Random(seed); Reset(SkillProfile.Average); }

        public void Reset(SkillProfile profile)
        {
            Profile = profile; Segment = 0; DeathsTotal = 0; HpFrac = 1f; Power = 1f; Boons = 0;
        }

        public SegmentTelemetry Neutral() => SegmentTelemetry.Neutral(SegmentsPerRun, MaxHp, ParSeconds);

        /// <summary>
        /// Simulate one segment with the Director's decision. Mirrors the game loop: from the second
        /// segment on, the player first visits a shrine (heal 15% + one boon from the pool the Director
        /// chose), then plays the segment at the chosen difficulty / enemy density.
        /// </summary>
        public SegmentTelemetry Step(DirectorDecision d)
        {
            if (Finished) throw new InvalidOperationException("Run finished; call Reset.");
            if (Segment > 0)
            {
                HpFrac = Math.Min(1f, HpFrac + ShrineHeal);
                Power += d.StrongBoons ? 0.10f : 0.05f;
                Boons++;
            }

            float skillRel = Profile.Skill / 0.6f;
            float c = DifficultyBase[d.Difficulty] * DensityFactor[d.Density] / (skillRel * Power);

            float hpLost = Math.Max(0f, 0.35f * (float)Math.Pow(c, 1.5) + Gauss(Profile.Noise));
            float time = Math.Max(0.3f, (0.6f + 0.4f * c + Gauss(0.1f)) / (0.8f + 0.2f * skillRel)) * ParSeconds;
            int deaths = 0;
            float hpLostTotal = hpLost;
            if (hpLost >= HpFrac)
            {
                // died: lose remaining HP, respawn at the segment checkpoint at full HP and retry
                deaths = 1;
                DeathsTotal++;
                float retry = Math.Max(0f, 0.5f * hpLost + Gauss(Profile.Noise));
                hpLostTotal = HpFrac + retry;
                HpFrac = Math.Max(0.05f, 1f - retry);
                time *= 1.5f;
            }
            else HpFrac -= hpLost;

            int attacks = 8 + (int)(6 * DensityFactor[d.Density]) + _rng.Next(4);
            float hitRate = Clamp01(0.35f + 0.6f * Profile.Skill - 0.1f * (c - 1f) + Gauss(0.05f));
            int hits = (int)Math.Round(attacks * hitRate);
            float enemyHp = 30f * (2f + 3f * DensityFactor[d.Density]) * (0.8f + 0.25f * d.Difficulty);
            float clear = Clamp01(1f - Math.Max(0f, c - 1.3f) * 0.5f + Gauss(0.05f));

            var t = new SegmentTelemetry
            {
                SegmentIndex = Segment, TotalSegments = SegmentsPerRun,
                HpLost = hpLostTotal * MaxHp, MaxHp = MaxHp, HpAtEnd = HpFrac * MaxHp,
                Seconds = time, ParSeconds = ParSeconds, Deaths = deaths,
                Attacks = attacks, Hits = hits, DamageDealt = clear * enemyHp, EnemyHpSpawned = enemyHp,
                BoonsHeld = Boons, LastDifficulty = d.Difficulty, LastDensity = d.Density
            };
            Segment++;
            return t;
        }

        private float Gauss(float sd)
        {
            // Box-Muller
            double u1 = 1.0 - _rng.NextDouble(), u2 = _rng.NextDouble();
            return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2) * sd);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
