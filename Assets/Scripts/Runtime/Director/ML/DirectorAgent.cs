using Odyssey.Director.Sim;
using Odyssey.Telemetry;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Odyssey.Director.ML
{
    /// <summary>
    /// ML-Agents agent behind the RL AI Director.
    ///  Observation: 10 normalised telemetry features (<see cref="DirectorObservation"/>).
    ///  Actions:     3 discrete branches [difficulty 3, enemy density 3, boon pool 2].
    ///  Reward:      <see cref="ChallengeBand.Reward"/> per segment.
    /// One decision is requested per segment boundary.
    ///
    /// Training mode: the agent owns a <see cref="RunSimulator"/> (simulated player with a random
    /// skill profile per episode) and is stepped by the Academy via a DecisionRequester.
    /// Gameplay / evaluation mode: the caller sets the observation and steps the Academy
    /// manually (<see cref="DecideNow"/>), so inference is synchronous.
    /// </summary>
    public class DirectorAgent : Agent
    {
        public enum Mode { Gameplay, Training }

        [SerializeField] private Mode mode = Mode.Gameplay;
        [SerializeField] private int seed = 1;
        [Tooltip("Training only: probability of each skill profile (Novice, Average, Expert).")]
        [SerializeField] private Vector3 skillMix = new Vector3(1f, 1f, 1f);

        private readonly float[] _obs = new float[DirectorObservation.Size];
        private SegmentTelemetry _current;
        private RunSimulator _sim;
        private System.Random _rng;
        private readonly HeuristicDirector _fallback = new HeuristicDirector();

        public DirectorDecision LastDecision { get; private set; }
        public bool HasDecision { get; private set; }
        public Mode AgentMode => mode;

        public void Configure(Mode m, int s) { mode = m; seed = s; }

        public override void Initialize()
        {
            _rng = new System.Random(seed);
            _sim = new RunSimulator(seed * 7919 + 13);
            _current = _sim.Neutral();
            if (mode == Mode.Gameplay) MaxStep = 0;
        }

        public override void OnEpisodeBegin()
        {
            if (mode != Mode.Training) return;
            _sim.Reset(PickProfile());
            _current = _sim.Neutral();
        }

        private SkillProfile PickProfile()
        {
            float total = skillMix.x + skillMix.y + skillMix.z;
            double r = _rng.NextDouble() * total;
            var p = r < skillMix.x ? SkillProfile.Novice : r < skillMix.x + skillMix.y ? SkillProfile.Average : SkillProfile.Expert;
            // jitter skill so the policy generalises between the three anchor profiles
            p.Skill = Mathf.Clamp(p.Skill + (float)(_rng.NextDouble() - 0.5) * 0.1f, 0.2f, 0.95f);
            return p;
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            DirectorObservation.Write(_current, _obs);
            for (int i = 0; i < _obs.Length; i++) sensor.AddObservation(_obs[i]);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            var a = actions.DiscreteActions;
            LastDecision = new DirectorDecision(a[0], a[1], a[2] == 1);
            HasDecision = true;

            if (mode != Mode.Training) return;

            var t = _sim.Step(LastDecision);
            float reward = ChallengeBand.Reward(t, LastDecision.StrongBoons && t.SegmentIndex > 0);
            AddReward(reward);
            _current = t;

            var stats = Academy.Instance.StatsRecorder;
            stats.Add("Director/InBand", ChallengeBand.InBand(t) ? 1f : 0f);
            stats.Add("Director/HpLostFrac", t.HpLostFrac);
            stats.Add("Director/Deaths", t.Deaths);

            if (_sim.Finished)
            {
                if (!_sim.Completed) AddReward(-1f);  // run failed: extra frustration penalty
                stats.Add("Director/RunCompleted", _sim.Completed ? 1f : 0f);
                EndEpisode();
            }
        }

        /// <summary>No model and no trainer: behave like the heuristic director (graceful fallback).</summary>
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var d = _fallback.Decide(_current);
            var a = actionsOut.DiscreteActions;
            a[0] = d.Difficulty; a[1] = d.Density; a[2] = d.StrongBoons ? 1 : 0;
        }

        /// <summary>Gameplay/eval: synchronous decision for the given telemetry.</summary>
        public DirectorDecision DecideNow(in SegmentTelemetry last)
        {
            _current = last;
            if (last.SegmentIndex < 0) _fallback.ResetRun();
            HasDecision = false;
            RequestDecision();
            Academy.Instance.EnvironmentStep();   // runs observation -> inference -> OnActionReceived now
            return HasDecision ? LastDecision : _fallback.Decide(last);
        }
    }
}
