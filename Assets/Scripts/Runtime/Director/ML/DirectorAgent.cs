using Odyssey.Director.Sim;
using Odyssey.Telemetry;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Odyssey.Director.ML
{
    /// <summary>
    /// The RL AI Director as an ML-Agents agent.
    ///   Observation: 4 telemetry features (<see cref="DirectorObservation"/>).
    ///   Action:      1 discrete choice - next segment difficulty (easy / medium / hard).
    ///   Reward:      <see cref="ChallengeBand.Reward"/> (+1 in band, -1 per death, -0.5 trivial).
    ///
    /// Training mode: plays against a <see cref="RunSimulator"/> (random skill level per episode,
    /// one episode = one run). Gameplay mode: the game passes in real telemetry and asks for a
    /// decision with <see cref="DecideNow"/>, which runs the ONNX policy immediately.
    /// </summary>
    public class DirectorAgent : Agent
    {
        public enum Mode { Gameplay, Training }

        [SerializeField] private Mode mode = Mode.Gameplay;
        [SerializeField] private int seed = 1;

        private readonly float[] _obs = new float[DirectorObservation.Size];
        private readonly HeuristicDirector _fallback = new HeuristicDirector();
        private SegmentTelemetry _last;
        private RunSimulator _sim;
        private System.Random _rng;

        public int LastDecision { get; private set; } = 1;
        public bool HasDecision { get; private set; }

        public void Configure(Mode m, int s) { mode = m; seed = s; }

        public override void Initialize()
        {
            _rng = new System.Random(seed);
            _sim = new RunSimulator(seed * 7919 + 13);
            _last = _sim.Neutral();
            if (mode == Mode.Gameplay) MaxStep = 0;
        }

        public override void OnEpisodeBegin()
        {
            if (mode != Mode.Training) return;
            // pick a random skill level (with a little jitter) so the policy must work for everyone
            var p = SkillProfile.All[_rng.Next(SkillProfile.All.Length)];
            p.Skill = Mathf.Clamp(p.Skill + (float)(_rng.NextDouble() - 0.5) * 0.1f, 0.2f, 0.95f);
            _sim.Reset(p);
            _last = _sim.Neutral();
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            DirectorObservation.Write(_last, _obs);
            foreach (var v in _obs) sensor.AddObservation(v);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            LastDecision = Difficulty.Clamp(actions.DiscreteActions[0]);
            HasDecision = true;
            if (mode != Mode.Training) return;

            _last = _sim.Step(LastDecision);
            AddReward(ChallengeBand.Reward(_last));

            var stats = Academy.Instance.StatsRecorder;
            stats.Add("Director/InBand", ChallengeBand.InBand(_last) ? 1f : 0f);
            stats.Add("Director/Deaths", _last.Deaths);
            stats.Add("Director/Difficulty", LastDecision);

            if (_sim.Finished) EndEpisode();
        }

        /// <summary>Used when there is no trained model and no trainer: act like the rule-based director.</summary>
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var a = actionsOut.DiscreteActions;
            a[0] = _fallback.Decide(_last);
        }

        /// <summary>Gameplay/evaluation: decide synchronously for the given telemetry.</summary>
        public int DecideNow(in SegmentTelemetry last)
        {
            _last = last;
            HasDecision = false;
            RequestDecision();
            Academy.Instance.EnvironmentStep();   // observation -> policy -> OnActionReceived, right now
            return HasDecision ? LastDecision : _fallback.Decide(last);
        }
    }
}
