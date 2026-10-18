using System;
using System.Collections.Generic;
using UnityEngine;

namespace Odyssey.Combat
{
    /// <summary>
    /// Attack volume. An attack state opens it for the "active" frames of the attack
    /// (frame data: windup -> active -> recovery). Each target is hit at most once per swing.
    /// Uses an overlap query instead of trigger callbacks so timing is deterministic.
    /// </summary>
    public class Hitbox : MonoBehaviour
    {
        public Team Team = Team.Player;
        public Vector2 Size = new Vector2(1.2f, 1f);
        public Vector2 LocalOffset = new Vector2(0.8f, 0f);

        public bool IsOpen { get; private set; }

        /// <summary>Raised for each target that accepted damage.</summary>
        public event Action<IDamageable, DamageInfo> Landed;

        private readonly HashSet<IDamageable> _alreadyHit = new HashSet<IDamageable>();
        private readonly Collider2D[] _buffer = new Collider2D[16];
        private Func<DamageInfo> _damageFactory;
        private float _facing = 1f;

        public void Open(Func<DamageInfo> damageFactory, float facing)
        {
            _damageFactory = damageFactory;
            _facing = Mathf.Sign(facing == 0 ? 1 : facing);
            _alreadyHit.Clear();
            IsOpen = true;
        }

        public void Close() { IsOpen = false; _damageFactory = null; }

        public Vector2 WorldCenter =>
            (Vector2)transform.position + new Vector2(LocalOffset.x * _facing, LocalOffset.y);

        /// <summary>Call every physics tick while open.</summary>
        public void Sweep()
        {
            if (!IsOpen || _damageFactory == null) return;
            var filter = ContactFilter2D.noFilter;
            int n = Physics2D.OverlapBox(WorldCenter, Size, 0f, filter, _buffer);
            for (int i = 0; i < n; i++)
            {
                var target = _buffer[i].GetComponentInParent<IDamageable>();
                if (target == null || target.Team == Team || !target.IsAlive) continue;
                if (!_alreadyHit.Add(target)) continue;
                var info = _damageFactory();
                info.Knockback = new Vector2(Mathf.Abs(info.Knockback.x) * _facing, info.Knockback.y);
                if (target.TakeDamage(info)) Landed?.Invoke(target, info);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = IsOpen ? new Color(1, 0, 0, 0.5f) : new Color(1, 1, 0, 0.2f);
            Gizmos.DrawWireCube(WorldCenter, Size);
        }
    }
}
