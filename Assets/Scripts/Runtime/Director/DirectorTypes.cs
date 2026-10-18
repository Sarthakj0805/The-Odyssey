using System;
using Odyssey.Telemetry;

namespace Odyssey.Director
{
    public enum DirectorKind { Fixed, Heuristic, RL }

    /// <summary>What the Director controls for the next segment.</summary>
    [Serializable]
    public struct DirectorDecision
    {
        public int Difficulty;   // 0 easy, 1 medium, 2 hard  -> chunk pool + enemy tier
        public int Density;      // 0 low, 1 medium, 2 high   -> fraction of enemy slots filled
        public bool StrongBoons; // shrine offers Rare (true) or Common (false) boons

        public DirectorDecision(int difficulty, int density, bool strongBoons)
        {
            Difficulty = Clamp(difficulty); Density = Clamp(density); StrongBoons = strongBoons;
        }

        private static int Clamp(int v) => v < 0 ? 0 : v > 2 ? 2 : v;

        public static readonly string[] DifficultyNames = { "Easy", "Medium", "Hard" };
        public static readonly string[] DensityNames = { "Low", "Medium", "High" };

        public override string ToString() =>
            $"{DifficultyNames[Difficulty]} / {DensityNames[Density]} enemies / {(StrongBoons ? "Rare" : "Common")} boons";
    }

    public interface IDirector
    {
        string Name { get; }
        /// <summary>Called at every segment boundary with the telemetry of the segment just played.</summary>
        DirectorDecision Decide(in SegmentTelemetry last);
        void ResetRun();
    }

    /// <summary>
    /// The challenge band the Director tries to keep the player inside, plus the reward
    /// function used for training and the metric used for evaluation. Documented in the README.
    /// </summary>
    public static class ChallengeBand
    {
        public const float HpLostMin = 0.15f, HpLostMax = 0.50f;   // fraction of max HP lost per segment
        public const float TimeMin = 0.75f, TimeMax = 1.50f;       // segment time / par time
        public const float TrivialHp = 0.05f, TrivialTime = 0.75f;

        public static bool HpInBand(in SegmentTelemetry t) => t.Deaths == 0 && t.HpLostFrac >= HpLostMin && t.HpLostFrac <= HpLostMax;
        public static bool TimeInBand(in SegmentTelemetry t) => t.TimeRatio >= TimeMin && t.TimeRatio <= TimeMax;
        public static bool InBand(in SegmentTelemetry t) => HpInBand(t) && TimeInBand(t);
        public static bool IsTrivial(in SegmentTelemetry t) => t.Deaths == 0 && t.HpLostFrac < TrivialHp && t.TimeRatio < TrivialTime;

        /// <summary>
        /// Reward for the decision that produced segment <paramref name="t"/>:
        ///   +0.6 if HP loss is in band, +0.4 if time is in band,
        ///   -1.0 per death (frustration spike), -0.5 if the segment was trivial,
        ///   -0.1 if the Director handed out the strong boon pool (generosity cost).
        /// </summary>
        public static float Reward(in SegmentTelemetry t, bool strongBoonsOffered)
        {
            float r = 0f;
            if (HpInBand(t)) r += 0.6f;
            if (TimeInBand(t)) r += 0.4f;
            r -= 1.0f * t.Deaths;
            if (IsTrivial(t)) r -= 0.5f;
            if (strongBoonsOffered) r -= 0.1f;
            return r;
        }
    }

    /// <summary>Feature vector shared by training and inference. All values in [0, 1].</summary>
    public static class DirectorObservation
    {
        public const int Size = 10;

        public static void Write(in SegmentTelemetry t, float[] dst)
        {
            if (dst == null || dst.Length < Size) throw new ArgumentException("dst too small");
            dst[0] = Clamp01(t.HpLostFrac / 1.5f);
            dst[1] = Clamp01(t.HpEndFrac);
            dst[2] = Clamp01(t.TimeRatio / 3f);
            dst[3] = Clamp01(t.Deaths / 3f);
            dst[4] = Clamp01(t.HitRate);
            dst[5] = Clamp01(t.ClearRatio);
            dst[6] = Clamp01(t.BoonsHeld / 8f);
            dst[7] = t.TotalSegments > 0 ? Clamp01((t.SegmentIndex + 1f) / t.TotalSegments) : 0f;
            dst[8] = t.LastDifficulty / 2f;
            dst[9] = t.LastDensity / 2f;
        }

        public static float[] Build(in SegmentTelemetry t)
        {
            var a = new float[Size];
            Write(t, a);
            return a;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
