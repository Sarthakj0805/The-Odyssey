using NUnit.Framework;
using Odyssey.Director;
using Odyssey.Director.Sim;
using Odyssey.Telemetry;

namespace Odyssey.Tests
{
    public class DirectorTests
    {
        private static SegmentTelemetry Seg(float hpLostFrac, int deaths, int difficulty = 1, float timeRatio = 1f) => new SegmentTelemetry
        {
            SegmentIndex = 0, TotalSegments = 3, Difficulty = difficulty, MaxHp = 100, HpLost = hpLostFrac * 100,
            Seconds = timeRatio * 45, ParSeconds = 45, Deaths = deaths
        };

        [Test]
        public void Observation_Has_4_Features_In_Range()
        {
            var obs = DirectorObservation.Build(Seg(2.5f, 5, 2, 9f));
            Assert.AreEqual(4, obs.Length);
            foreach (var v in obs) Assert.That(v, Is.InRange(0f, 1f));
            Assert.AreEqual(1f, obs[2], "died flag");
            Assert.AreEqual(1f, obs[3], "hard = 1");
        }

        [Test]
        public void Reward_Is_Plus1_In_Band_Minus1_On_Death_Minus_Half_When_Trivial()
        {
            Assert.AreEqual(1f, ChallengeBand.Reward(Seg(0.3f, 0)));
            Assert.AreEqual(-1f, ChallengeBand.Reward(Seg(0.9f, 1)));
            Assert.AreEqual(-0.5f, ChallengeBand.Reward(Seg(0.01f, 0)));
            Assert.AreEqual(0f, ChallengeBand.Reward(Seg(0.7f, 0)), "too hard but survived");
        }

        [Test]
        public void Heuristic_Starts_Medium_Then_Steps_Down_On_Death_And_Up_When_Easy()
        {
            var h = new HeuristicDirector();
            Assert.AreEqual(1, h.Decide(SegmentTelemetry.Neutral(3, 100, 45)));
            Assert.AreEqual(0, h.Decide(Seg(1.0f, 1, difficulty: 1)));
            Assert.AreEqual(2, h.Decide(Seg(0.05f, 0, difficulty: 1)));
            Assert.AreEqual(1, h.Decide(Seg(0.3f, 0, difficulty: 1)), "in band: keep");
            Assert.AreEqual(0, h.Decide(Seg(0.9f, 1, difficulty: 0)), "can't go below easy");
        }

        [Test]
        public void Simulator_Is_Deterministic_For_A_Seed()
        {
            var a = new RunSimulator(5); a.Reset(SkillProfile.Average);
            var b = new RunSimulator(5); b.Reset(SkillProfile.Average);
            while (!a.Finished)
            {
                var ta = a.Step(1); var tb = b.Step(1);
                Assert.AreEqual(ta.HpLost, tb.HpLost);
                Assert.AreEqual(ta.Seconds, tb.Seconds);
            }
        }

        [Test]
        public void Harder_Difficulty_Costs_More_Hp_In_Simulation()
        {
            var easy = DirectorEvaluation.Run(new FixedDirector(0), SkillProfile.Average, 100, 1);
            var hard = DirectorEvaluation.Run(new FixedDirector(2), SkillProfile.Average, 100, 1);
            Assert.Less(easy.DeathsPerRun, hard.DeathsPerRun);
            Assert.GreaterOrEqual(easy.CompletionRate, hard.CompletionRate);
        }

        [Test]
        public void Evaluation_Is_Reproducible_With_The_Same_Seeds()
        {
            var r1 = DirectorEvaluation.Run(new HeuristicDirector(), SkillProfile.Novice, 50, 1000);
            var r2 = DirectorEvaluation.Run(new HeuristicDirector(), SkillProfile.Novice, 50, 1000);
            Assert.AreEqual(r1.InBandRate, r2.InBandRate);
            Assert.AreEqual(r1.DeathsPerRun, r2.DeathsPerRun);
        }
    }
}
