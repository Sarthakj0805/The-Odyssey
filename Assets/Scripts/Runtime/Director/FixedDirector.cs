using Odyssey.Telemetry;

namespace Odyssey.Director
{
    /// <summary>Baseline 1: always medium.</summary>
    public sealed class FixedDirector : IDirector
    {
        private readonly int _difficulty;
        public FixedDirector(int difficulty = 1) { _difficulty = Difficulty.Clamp(difficulty); }
        public string Name => "Fixed";
        public int Decide(in SegmentTelemetry last) => _difficulty;
        public void ResetRun() { }
    }
}
