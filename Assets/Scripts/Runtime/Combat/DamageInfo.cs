using UnityEngine;

namespace Odyssey.Combat
{
    public enum Team { Player, Enemy, Environment }

    /// <summary>Everything a hit carries. Passed by value through <see cref="IDamageable"/>.</summary>
    public struct DamageInfo
    {
        public float Amount;
        public Vector2 Knockback;     // world-space impulse direction * strength
        public Team SourceTeam;
        public GameObject Source;     // attacker (may be null for hazards)

        public DamageInfo(float amount, Vector2 knockback, Team sourceTeam, GameObject source)
        {
            Amount = amount; Knockback = knockback; SourceTeam = sourceTeam; Source = source;
        }
    }

    /// <summary>Anything that can be hit. Hitboxes only talk to this interface.</summary>
    public interface IDamageable
    {
        Team Team { get; }
        bool IsAlive { get; }
        /// <returns>true if the hit was applied (not blocked by i-frames / death).</returns>
        bool TakeDamage(DamageInfo info);
    }
}
