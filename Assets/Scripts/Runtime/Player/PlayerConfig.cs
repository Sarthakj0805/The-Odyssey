using UnityEngine;

namespace Odyssey.Player
{
    /// <summary>Tunable player data (movement + attack frame data). Boons modify these at runtime.</summary>
    [CreateAssetMenu(menuName = "Odyssey/Player Config", fileName = "PlayerConfig")]
    public class PlayerConfig : ScriptableObject
    {
        [Header("Health")]
        public float MaxHealth = 100f;
        public float HurtIFrames = 0.8f;
        public float HurtStunSeconds = 0.3f;

        [Header("Movement")]
        public float MoveSpeed = 7f;
        public float Acceleration = 70f;
        public float JumpVelocity = 13f;
        public float GravityScale = 3.2f;
        public float FallGravityMultiplier = 1.6f;
        public float MaxFallSpeed = 20f;
        public float CoyoteTime = 0.1f;
        public float JumpBuffer = 0.12f;
        [Range(0f, 1f)] public float JumpCutMultiplier = 0.5f;

        [Header("Attack (frame data, seconds at 1.0 attack speed)")]
        public float AttackDamage = 12f;
        public float AttackWindup = 0.08f;
        public float AttackActive = 0.12f;
        public float AttackRecovery = 0.18f;
        public Vector2 AttackKnockback = new Vector2(5f, 3f);
        public Vector2 HitboxSize = new Vector2(1.6f, 1.2f);
        public Vector2 HitboxOffset = new Vector2(0.95f, 0.1f);

        [Header("Dodge")]
        public float DodgeSpeed = 16f;
        public float DodgeDuration = 0.18f;
        public float DodgeCooldown = 0.6f;

        [Header("Visuals (optional - placeholder used if empty)")]
        public Sprite Sprite;
        public Color Tint = new Color(0.95f, 0.8f, 0.35f);
    }
}
