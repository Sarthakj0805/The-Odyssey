using Odyssey.Telemetry;

namespace Odyssey.Director
{
    /// <summary>
    /// Baseline (b): hand-written rules, the kind of dynamic difficulty a designer would ship.
    /// Struggling (death or too much HP lost) -> step down + strong boons; cruising -> step up.
    /// </summary>
    public sealed class HeuristicDirector : IDirector
    {
        private int _level = 2;   // 0..4 combined intensity ladder, starts medium/medium
        public string Name => "Heuristic";
        public void ResetRun() => _level = 2;

        public DirectorDecision Decide(in SegmentTelemetry last)
        {
            bool first = last.SegmentIndex < 0;
            bool struggling = last.Deaths > 0 || last.HpLostFrac > ChallengeBand.HpLostMax || last.HpEndFrac < 0.25f;
            bool cruising = last.Deaths == 0 && last.HpLostFrac < ChallengeBand.HpLostMin && last.TimeRatio < 1.1f;

            if (!first)
            {
                if (struggling) _level -= last.Deaths > 0 ? 2 : 1;
                else if (cruising) _level += 1;
            }
            if (_level < 0) _level = 0;
            if (_level > 4) _level = 4;

            // ladder: (diff, density) = (0,0) (0,1) (1,1) (1,2) (2,2)
            int diff = _level <= 1 ? 0 : _level <= 3 ? 1 : 2;
            int dens = _level == 0 ? 0 : _level <= 2 ? 1 : 2;
            return new DirectorDecision(diff, dens, !first && struggling);
        }
    }
}
