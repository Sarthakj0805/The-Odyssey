using Odyssey.Telemetry;
using Unity.MLAgents;
using Unity.MLAgents.Policies;

namespace Odyssey.Director.ML
{
    /// <summary><see cref="IDirector"/> adapter over the trained ML-Agents policy (ONNX via Inference Engine).</summary>
    public sealed class RLDirector : IDirector
    {
        private readonly DirectorAgent _agent;
        public RLDirector(DirectorAgent agent)
        {
            _agent = agent;
            // segment decisions are event-driven, so the Academy is stepped manually
            Academy.Instance.AutomaticSteppingEnabled = false;
        }

        public string Name => "RL";

        /// <summary>True when an ONNX model is assigned (otherwise the agent falls back to the heuristic).</summary>
        public bool HasModel
        {
            get
            {
                var bp = _agent.GetComponent<BehaviorParameters>();
                return bp != null && bp.Model != null;
            }
        }

        public int Decide(in SegmentTelemetry last) => _agent.DecideNow(last);
        public void ResetRun() { }
    }
}
