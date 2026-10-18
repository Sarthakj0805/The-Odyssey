using NUnit.Framework;
using Odyssey.Combat;

namespace Odyssey.Tests
{
    public class HealthModelTests
    {
        [Test]
        public void Damage_Reduces_Health_And_Grants_IFrames()
        {
            var h = new HealthModel(100f);
            Assert.AreEqual(30f, h.ApplyDamage(30f, 0.5f));
            Assert.AreEqual(70f, h.Current);
            Assert.IsTrue(h.IsInvulnerable);
            Assert.AreEqual(0f, h.ApplyDamage(30f), "hits during i-frames are ignored");
            h.Tick(0.6f);
            Assert.IsFalse(h.IsInvulnerable);
            Assert.AreEqual(30f, h.ApplyDamage(30f));
        }

        [Test]
        public void Death_Fires_Once_And_Overkill_Is_Clamped()
        {
            var h = new HealthModel(50f);
            int died = 0;
            h.Died += () => died++;
            Assert.AreEqual(50f, h.ApplyDamage(999f));
            Assert.IsTrue(h.IsDead);
            h.ApplyDamage(10f);
            Assert.AreEqual(1, died);
            Assert.AreEqual(0f, h.Heal(10f), "dead entities can't be healed");
        }

        [Test]
        public void Heal_Is_Clamped_To_Max()
        {
            var h = new HealthModel(100f);
            h.ApplyDamage(10f);
            Assert.AreEqual(10f, h.Heal(50f));
            Assert.AreEqual(100f, h.Current);
        }

        [Test]
        public void SetMax_Keeps_Missing_Health()
        {
            var h = new HealthModel(100f);
            h.ApplyDamage(30f);
            h.SetMax(120f);
            Assert.AreEqual(120f, h.Max);
            Assert.AreEqual(90f, h.Current);
        }
    }
}
