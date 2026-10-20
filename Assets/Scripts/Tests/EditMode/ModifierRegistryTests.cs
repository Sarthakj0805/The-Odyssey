using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Modifiers;
using UnityEngine;

namespace Odyssey.Tests
{
    public class ModifierRegistryTests
    {
        private readonly List<Object> _created = new List<Object>();

        private BoonDefinition Boon(string id, string god, int maxStacks, params StatModifier[] mods)
        {
            var b = ScriptableObject.CreateInstance<BoonDefinition>();
            b.Id = id; b.God = god; b.MaxStacks = maxStacks; b.Modifiers = mods;
            _created.Add(b);
            return b;
        }

        [TearDown]
        public void Cleanup() { foreach (var o in _created) Object.DestroyImmediate(o); _created.Clear(); }

        [Test]
        public void Flat_And_Percent_Combine_As_Base_Plus_Flat_Times_One_Plus_Pct()
        {
            var reg = new RunModifierRegistry();
            reg.Add(Boon("hera", "Hera", 3, new StatModifier(StatType.MaxHealth, ModifierOp.Flat, 20f)));
            reg.Add(Boon("x", "X", 3, new StatModifier(StatType.MaxHealth, ModifierOp.PercentAdd, 0.5f)));
            Assert.AreEqual((100f + 20f) * 1.5f, reg.Evaluate(StatType.MaxHealth, 100f), 1e-4);
        }

        [Test]
        public void Percent_Modifiers_Stack_Additively()
        {
            var reg = new RunModifierRegistry();
            var athena = Boon("athena", "Athena", 3, new StatModifier(StatType.Damage, ModifierOp.PercentAdd, 0.2f));
            reg.Add(athena); reg.Add(athena);
            Assert.AreEqual(2, reg.StacksOf(athena));
            Assert.AreEqual(10f * 1.4f, reg.Evaluate(StatType.Damage, 10f), 1e-4);
        }

        [Test]
        public void MaxStacks_Is_Enforced()
        {
            var reg = new RunModifierRegistry();
            var b = Boon("b", "B", 2, new StatModifier(StatType.MoveSpeed, ModifierOp.Flat, 1f));
            Assert.IsTrue(reg.Add(b));
            Assert.IsTrue(reg.Add(b));
            Assert.IsFalse(reg.Add(b));
            Assert.AreEqual(2, reg.StacksOf(b));
            Assert.AreEqual(7f, reg.Evaluate(StatType.MoveSpeed, 5f), 1e-4);
        }

        [Test]
        public void Unrelated_Stats_Are_Unaffected_And_Clear_Resets()
        {
            var reg = new RunModifierRegistry();
            reg.Add(Boon("ares", "Ares", 3, new StatModifier(StatType.HealOnKill, ModifierOp.Flat, 4f)));
            Assert.AreEqual(4f, reg.Evaluate(StatType.HealOnKill, 0f));
            Assert.AreEqual(7f, reg.Evaluate(StatType.MoveSpeed, 7f));
            reg.Clear();
            Assert.AreEqual(0f, reg.Evaluate(StatType.HealOnKill, 0f));
            Assert.AreEqual(0, reg.TotalStacks);
        }

        [Test]
        public void Offer_Has_One_Boon_Per_God_And_Skips_Maxed_Boons()
        {
            var maxed = Boon("a1", "Athena", 1);
            var all = new List<BoonDefinition>
            {
                maxed, Boon("a2", "Athena", 3), Boon("h1", "Hermes", 3), Boon("h2", "Hermes", 3), Boon("p1", "Apollo", 3),
            };
            var reg = new RunModifierRegistry();
            reg.Add(maxed);
            var offer = BoonOffer.Draw(all, reg, 3, new System.Random(42));
            Assert.AreEqual(3, offer.Count);
            CollectionAssert.DoesNotContain(offer, maxed);
            var gods = new HashSet<string>();
            foreach (var b in offer) Assert.IsTrue(gods.Add(b.God), "one boon per god");
        }
    }
}
