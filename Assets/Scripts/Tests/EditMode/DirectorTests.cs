using NUnit.Framework;
using Odyssey.Director;
using Odyssey.Director.Sim;
using Odyssey.Telemetry;

namespace Odyssey.Tests
{
    public class DirectorTests
    {
        private static SegmentTelemetry Seg(float hpLostFrac, float timeRatio, int deaths, int index = 1) => new SegmentTelemetry
        {
            SegmentIndex = index, TotalSegments = 5, MaxHp = 100, HpLost = hpLostFrac * 100, HpAtEnd = 100 - hpLostFrac * 100,
            Seconds = timeRatio * 45, ParSeconds = 45, Deaths = deaths, Attacks = 10, Hits = 5, DamageDealt = 50, EnemyHpSpawned = 100,
            LastDifficulty = 1, LastDensity = 1
        };

        [Test]
        public void Observation_Has_Fixed_Size_And_Is_Normalised()
        {
            var obs = DirectorObservation.Build(Seg(2.5f, 9f, 5));
            Assert.AreEqual(DirectorObservation.Size, obs.Length);
            foreach (var v in obs) Assert.That(v, Is.InRange(0f, 1f));
        }

        [Test]
        public void Reward_Prefers_In_Band_Over_Death_And_Trivial()
        {
            float inBand = ChallengeBand.Reward(Seg(0.3f, 1.0f, 0), false);
            float death = ChallengeBand.Reward(Seg(1.0f, 1.4f, 1), false);
            float trivial = ChallengeBand.Reward(Seg(0.0f, 0.5f, 0), false);
            Assert.AreEqual(1.0f, inBand, 1e-5);
            Assert.Less(death, 0f);
            Assert.Less(trivial, inBand);
            Assert.Less(ChallengeBand.Reward(Seg(0.3f, 1.0f, 0), true), inBand, "strong boons cost a little");
        }

        [Test]
        public void Heuristic_Steps_Down_After_Death_And_Up_When_Cruising()
        {
            var h = new HeuristicDirector();
            var start = h.Decide(SegmentTelemetry.Neutral(5, 100, 45));
            var afterDeath = h.Decide(Seg(1.0f, 1.5f, 1));
            Assert.Less(afterDeath.Difficulty + afterDeath.Density, start.Difficulty + start.Density);
            Assert.IsTrue(afterDeath.StrongBoons);

            h.ResetRun();
            h.Decide(SegmentTelemetry.Neutral(5, 100, 45));
            var cruising = h.Decide(Seg(0.02f, 0.6f, 0));
            Assert.Greater(cruising.Difficulty + cruising.Density, start.Difficulty + start.Density);
        }

        [Test]
        public void Simulator_Is_Deterministic_For_A_Seed()
        {
            var a = new RunSimulator(5); a.Reset(SkillProfile.Average);
            var b = new RunSimulator(5); b.Reset(SkillProfile.Average);
            var d = new DirectorDecision(1, 1, false);
            for (int i = 0; i < RunSimulator.SegmentsPerRun && !a.Finished; i++)
            {
                var ta = a.Step(d); var tb = b.Step(d);
                Assert.AreEqual(ta.HpLost, tb.HpLost);
                Assert.AreEqual(ta.Seconds, tb.Seconds);
            }
        }

        [Test]
        public void Harder_Settings_Cost_More_Hp_In_Simulation()
        {
            var easy = DirectorEvaluation.Run(new FixedDirector(new DirectorDecision(0, 0, false)), SkillProfile.Average, 100, 1);
            var hard = DirectorEvaluation.Run(new FixedDirector(new DirectorDecision(2, 2, false)), SkillProfile.Average, 100, 1);
            Assert.Less(easy.MeanHpLost, hard.MeanHpLost);
            Assert.GreaterOrEqual(easy.CompletionRate, hard.CompletionRate);
        }

        [Test]
        public void Evaluation_Uses_Identical_Seeds_For_Every_Director()
        {
            var r1 = DirectorEvaluation.Run(new FixedDirector(), SkillProfile.Novice, 50, 1000);
            var r2 = DirectorEvaluation.Run(new FixedDirector(), SkillProfile.Novice, 50, 1000);
            Assert.AreEqual(r1.TimeInBand, r2.TimeInBand);
            Assert.AreEqual(r1.MeanReturn, r2.MeanReturn);
        }
    }
}
