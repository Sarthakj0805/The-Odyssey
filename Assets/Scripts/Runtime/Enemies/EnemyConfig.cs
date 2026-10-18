using UnityEngine;

namespace Odyssey.Enemies
{
    public enum EnemyKind { Melee, Ranged }

    /// <summary>Data-only enemy definition: a new enemy type is a new asset, not new code.</summary>
    [CreateAssetMenu(menuName = "Odyssey/Enemy Config", fileName = "EnemyConfig")]
    public class EnemyConfig : ScriptableObject
    {
        public string DisplayName = "Enemy";
        public EnemyKind Kind = EnemyKind.Melee;

        [Header("Body")]
        public Vector2 Size = new Vector2(0.9f, 1.4f);
        public Color Tint = new Color(0.7f, 0.3f, 0.25f);
        public Sprite Sprite;

        [Header("Stats (scaled by Director tier)")]
        public float MaxHealth = 30f;
        public float Damage = 12f;
        public float PatrolSpeed = 1.5f;
        public float ChaseSpeed = 3.2f;
        public bool StaggerOnHit = true;
        public float HurtStunSeconds = 0.25f;
        [Range(0f, 1f)] public float KnockbackResistance = 0f;

        [Header("Senses")]
        public float AggroRange = 7f;
        public float AttackRange = 1.4f;
        public float AttackCooldown = 1.2f;

        [Header("Attack frame data")]
        public float Windup = 0.45f;
        public float Active = 0.15f;
        public float Recovery = 0.4f;
        public Vector2 Knockback = new Vector2(6f, 4f);
        public Vector2 HitboxSize = new Vector2(1.4f, 1.2f);
        public Vector2 HitboxOffset = new Vector2(0.9f, 0f);

        [Header("Ranged only")]
        public float ProjectileFlightTime = 1.0f;
        public float ProjectileGravity = 1.2f;
        public float ProjectileRadius = 0.25f;
    }
}
