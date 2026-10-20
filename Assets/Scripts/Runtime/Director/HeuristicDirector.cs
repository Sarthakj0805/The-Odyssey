using Odyssey.Telemetry;

namespace Odyssey.Director
{
    /// <summary>
    /// Baseline 2: the hand-written rule a designer would ship.
    /// Died or lost too much HP -> one step easier; barely got hurt -> one step harder; otherwise keep.
    /// </summary>
    public sealed class HeuristicDirector : IDirector
    {
        public string Name => "Heuristic";
        public void ResetRun() { }

        public int Decide(in SegmentTelemetry last)
        {
            if (last.SegmentIndex < 0) return 1;   // no data yet: start on medium
            if (last.Deaths > 0 || last.HpLostFrac > ChallengeBand.HpLostMax) return Difficulty.Clamp(last.Difficulty - 1);
            if (last.HpLostFrac < ChallengeBand.HpLostMin) return Difficulty.Clamp(last.Difficulty + 1);
            return last.Difficulty;
        }
    }
}
