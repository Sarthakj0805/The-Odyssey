using System;
using Odyssey.Combat;
using Odyssey.Core;
using Odyssey.Player;
using UnityEngine;

namespace Odyssey.Level
{
    /// <summary>Spikes: damage + upward knock on contact.</summary>
    public class Hazard : MonoBehaviour
    {
        public float Damage = 20f;
        private void OnTriggerStay2D(Collider2D other)
        {
            var d = other.GetComponentInParent<IDamageable>();
            if (d == null || d.Team != Team.Player) return;
            d.TakeDamage(new DamageInfo(Damage, new Vector2(0f, 12f), Team.Environment, gameObject));
        }
    }
}
