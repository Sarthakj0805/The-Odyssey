using System;
using UnityEngine;

namespace Odyssey.Modifiers
{
    public enum StatType { MaxHealth, Damage, MoveSpeed, AttackSpeed, Knockback, HealOnKill }

    /// <summary>Flat values are summed; PercentAdd values are summed then applied once: (base + flat) * (1 + pct).</summary>
    public enum ModifierOp { Flat, PercentAdd }

    [Serializable]
    public struct StatModifier
    {
        public StatType Stat;
        public ModifierOp Op;
        public float Value;

        public StatModifier(StatType stat, ModifierOp op, float value) { Stat = stat; Op = op; Value = value; }
    }

    /// <summary>
    /// A "Boon of the Gods" rogue-lite modifier. Pure data: a new boon is a new asset, not new code.
    /// Each boon changes one or more player stats; gameplay code reads stats through the registry.
    /// </summary>
    [CreateAssetMenu(menuName = "Odyssey/Boon", fileName = "Boon")]
    public class BoonDefinition : ScriptableObject
    {
        public string Id = "boon";
        public string DisplayName = "Boon";
        public string God = "Athena";
        [TextArea] public string Description = "";
        [Min(1)] public int MaxStacks = 3;
        public Color Tint = Color.white;
        public StatModifier[] Modifiers = Array.Empty<StatModifier>();
    }
}
