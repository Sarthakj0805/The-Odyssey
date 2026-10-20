using System;
using Odyssey.Telemetry;

namespace Odyssey.Director
{
    public enum DirectorKind { Fixed, Heuristic, RL }

    /// <summary>Common interface for every AI Director (Fixed, Heuristic, RL).</summary>
    public interface IDirector
    {
        string Name { get; }
        /// <summary>Called at every segment boundary. Returns the next segment's difficulty: 0 easy, 1 medium, 2 hard.</summary>
        int Decide(in SegmentTelemetry last);
        void ResetRun();
    }

    public static class Difficulty
    {
        public static readonly string[] Names = { "Easy", "Medium", "Hard" };
        public static int Clamp(int d) => d < 0 ? 0 : d > 2 ? 2 : d;
    }

    /// <summary>
    /// The target challenge band and the reward used for training (documented in the README).
    /// A segment is "in band" when the player lost 15-50% of max HP and did not die.
    /// </summary>
    public static class ChallengeBand
    {
        public const float HpLostMin = 0.15f, HpLostMax = 0.50f, TrivialHp = 0.05f;

        public static bool InBand(in SegmentTelemetry t) => t.Deaths == 0 && t.HpLostFrac >= HpLostMin && t.HpLostFrac <= HpLostMax;
        public static bool IsTrivial(in SegmentTelemetry t) => t.Deaths == 0 && t.HpLostFrac < TrivialHp;

        /// <summary>+1 in band, -1 per death, -0.5 if the segment was trivially easy.</summary>
        public static float Reward(in SegmentTelemetry t)
        {
            if (t.Deaths > 0) return -1f * t.Deaths;
            if (InBand(t)) return 1f;
            if (IsTrivial(t)) return -0.5f;
            return 0f;
        }
    }

    /// <summary>The 4 features the RL Director observes. All values are in [0, 1].</summary>
    public static class DirectorObservation
    {
        public const int Size = 4;

        public static void Write(in SegmentTelemetry t, float[] dst)
        {
            if (dst == null || dst.Length < Size) throw new ArgumentException("dst too small");
            dst[0] = Clamp01(t.HpLostFrac);          // how much HP the player lost
            dst[1] = Clamp01(t.TimeRatio / 3f);      // how long it took (vs par)
            dst[2] = t.Deaths > 0 ? 1f : 0f;         // did the player die
            dst[3] = t.Difficulty / 2f;              // the difficulty they just played
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
