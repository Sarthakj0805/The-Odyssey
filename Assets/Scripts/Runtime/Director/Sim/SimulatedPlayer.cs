using System;
using Odyssey.Telemetry;

namespace Odyssey.Director.Sim
{
    /// <summary>A scripted "bot" player used to train and evaluate the Director.</summary>
    [Serializable]
    public struct SkillProfile
    {
        public string Name;
        public float Skill;   // 0..1, how good the bot is
        public float Noise;   // randomness of each segment's outcome

        public SkillProfile(string name, float skill, float noise) { Name = name; Skill = skill; Noise = noise; }

        public static readonly SkillProfile Novice = new SkillProfile("Novice", 0.35f, 0.08f);
        public static readonly SkillProfile Average = new SkillProfile("Average", 0.60f, 0.08f);
        public static readonly SkillProfile Expert = new SkillProfile("Expert", 0.85f, 0.08f);
        public static readonly SkillProfile[] All = { Novice, Average, Expert };
    }

    /// <summary>
    /// Fast model of one run (no rendering, no physics) so the Director can train on thousands of runs
    /// in minutes. One formula drives it:
    ///
    ///     challenge = difficultyFactor[d] / (relativeSkill * power)
    ///     HP lost   = 0.35 * challenge^1.5 + noise      (fraction of max HP)
    ///
    /// It mirrors the game's rules: HP carries over between segments, the shrine before each segment
    /// heals 15% and gives a boon (+5% power), dying respawns you at full HP, 3 deaths end the run.
    /// The constants are hand-picked assumptions, not fitted to human data (see README limitations).
    /// </summary>
    public sealed class RunSimulator
    {
        public const int SegmentsPerRun = 3;
        public const int MaxDeaths = 3;
        public const float ShrineHeal = 0.15f;
        public const float BoonPower = 0.05f;
        public const float MaxHp = 100f;
        public const float ParSeconds = 45f;

        private static readonly float[] DifficultyFactor = { 0.6f, 1.0f, 1.5f };

        private readonly Random _rng;
        public SkillProfile Profile { get; private set; }
        public int Segment { get; private set; }
        public int DeathsTotal { get; private set; }
        public float HpFrac { get; private set; }
        public float Power { get; private set; }
        public bool Finished => Segment >= SegmentsPerRun || DeathsTotal >= MaxDeaths;
        public bool Completed => Segment >= SegmentsPerRun && DeathsTotal < MaxDeaths;

        public RunSimulator(int seed) { _rng = new Random(seed); Reset(SkillProfile.Average); }

        public void Reset(SkillProfile profile)
        {
            Profile = profile; Segment = 0; DeathsTotal = 0; HpFrac = 1f; Power = 1f;
        }

        public SegmentTelemetry Neutral() => SegmentTelemetry.Neutral(SegmentsPerRun, MaxHp, ParSeconds);

        /// <summary>Simulate one segment played at difficulty <paramref name="d"/> (0..2).</summary>
        public SegmentTelemetry Step(int d)
        {
            if (Finished) throw new InvalidOperationException("Run finished; call Reset.");
            d = Difficulty.Clamp(d);
            if (Segment > 0) { HpFrac = Math.Min(1f, HpFrac + ShrineHeal); Power += BoonPower; }   // shrine

            float challenge = DifficultyFactor[d] / ((Profile.Skill / 0.6f) * Power);
            float hpLost = Math.Max(0f, 0.35f * (float)Math.Pow(challenge, 1.5) + Gauss(Profile.Noise));
            float seconds = ParSeconds * Math.Max(0.3f, 0.7f + 0.3f * challenge + Gauss(0.1f));
            int deaths = 0;

            if (hpLost >= HpFrac)
            {
                // died: respawn at the checkpoint with full HP and play the segment again
                deaths = 1;
                DeathsTotal++;
                float retry = Math.Max(0f, 0.5f * hpLost + Gauss(Profile.Noise));
                hpLost = HpFrac + retry;
                HpFrac = Math.Max(0.05f, 1f - retry);
                seconds *= 1.5f;
            }
            else HpFrac -= hpLost;

            var t = new SegmentTelemetry
            {
                SegmentIndex = Segment, TotalSegments = SegmentsPerRun, Difficulty = d,
                HpLost = hpLost * MaxHp, MaxHp = MaxHp, Seconds = seconds, ParSeconds = ParSeconds, Deaths = deaths
            };
            Segment++;
            return t;
        }

        private float Gauss(float sd)
        {
            double u1 = 1.0 - _rng.NextDouble(), u2 = _rng.NextDouble();   // Box-Muller
            return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2) * sd);
        }
    }
}
