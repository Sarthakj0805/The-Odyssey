using System;
using System.Collections.Generic;
using System.Linq;

namespace Odyssey.Modifiers
{
    /// <summary>
    /// Run-level registry of picked boons (pure C#, edit-mode tested).
    /// Rules: each boon stacks up to its MaxStacks; flat modifiers add, percent modifiers
    /// add together and multiply once: final = (base + sum(flat)) * (1 + sum(pct)).
    /// Cleared when the run ends.
    /// </summary>
    public sealed class RunModifierRegistry
    {
        private readonly Dictionary<string, int> _stacks = new Dictionary<string, int>();
        private readonly List<BoonDefinition> _order = new List<BoonDefinition>();

        public event Action Changed;

        public IReadOnlyList<BoonDefinition> Picked => _order;
        public int TotalStacks => _stacks.Values.Sum();

        public int StacksOf(BoonDefinition boon) =>
            boon != null && _stacks.TryGetValue(boon.Id, out var n) ? n : 0;

        public bool CanAdd(BoonDefinition boon) => boon != null && StacksOf(boon) < boon.MaxStacks;

        public bool Add(BoonDefinition boon)
        {
            if (!CanAdd(boon)) return false;
            _stacks[boon.Id] = StacksOf(boon) + 1;
            _order.Add(boon);
            Changed?.Invoke();
            return true;
        }

        public void Clear()
        {
            _stacks.Clear(); _order.Clear();
            Changed?.Invoke();
        }

        public float Evaluate(StatType stat, float baseValue)
        {
            float flat = 0f, pct = 0f;
            foreach (var boon in _order)
            {
                if (boon.Modifiers == null) continue;
                foreach (var m in boon.Modifiers)
                {
                    if (m.Stat != stat) continue;
                    if (m.Op == ModifierOp.Flat) flat += m.Value; else pct += m.Value;
                }
            }
            return (baseValue + flat) * (1f + pct);
        }
    }

    /// <summary>Draws N distinct boons for a shrine offer, from the weak (Common) or strong (Rare) pool.</summary>
    public static class BoonOffer
    {
        public static List<BoonDefinition> Draw(IList<BoonDefinition> all, RunModifierRegistry registry,
            bool strongPool, int count, Random rng)
        {
            var wanted = strongPool ? BoonRarity.Rare : BoonRarity.Common;
            var pool = all.Where(b => b != null && b.Rarity == wanted && registry.CanAdd(b)).ToList();
            if (pool.Count < count) // top up from the other pool so the shrine never shows < count options
                pool.AddRange(all.Where(b => b != null && b.Rarity != wanted && registry.CanAdd(b)));

            var result = new List<BoonDefinition>(count);
            var usedGods = new HashSet<string>();
            while (result.Count < count && pool.Count > 0)
            {
                int i = rng.Next(pool.Count);
                var pick = pool[i];
                pool.RemoveAt(i);
                if (!usedGods.Add(pick.God)) continue; // one offer per god
                result.Add(pick);
            }
            return result;
        }
    }
}
