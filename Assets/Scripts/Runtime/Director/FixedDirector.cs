using Odyssey.Telemetry;

namespace Odyssey.Director
{
    /// <summary>Baseline (a): always the same medium setting.</summary>
    public sealed class FixedDirector : IDirector
    {
        private readonly DirectorDecision _decision;
        public FixedDirector() : this(new DirectorDecision(1, 1, false)) { }
        public FixedDirector(DirectorDecision decision) { _decision = decision; }
        public string Name => "Fixed";
        public DirectorDecision Decide(in SegmentTelemetry last) => _decision;
        public void ResetRun() { }
    }
}
