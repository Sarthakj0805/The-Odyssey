using UnityEngine;

namespace Odyssey.Combat
{
    /// <summary>Thrown rock: ballistic Rigidbody2D, damages the first opposing IDamageable it touches.</summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class Projectile : MonoBehaviour
    {
        private DamageInfo _damage;
        private float _life = 4f;
        private bool _spent;

        public void Launch(Vector2 velocity, DamageInfo damage, float lifetime = 4f)
        {
            _damage = damage; _life = lifetime;
            GetComponent<Rigidbody2D>().linearVelocity = velocity;
        }

        private void Update()
        {
            _life -= Time.deltaTime;
            if (_life <= 0f) Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_spent) return;
            var target = other.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                if (target.Team == _damage.SourceTeam || !target.IsAlive) return;
                var info = _damage;
                float dir = Mathf.Sign(GetComponent<Rigidbody2D>().linearVelocity.x);
                info.Knockback = new Vector2(Mathf.Abs(info.Knockback.x) * dir, info.Knockback.y);
                target.TakeDamage(info);
                _spent = true; Destroy(gameObject);
                return;
            }
            // hit level geometry
            if (!other.isTrigger) { _spent = true; Destroy(gameObject); }
        }
    }
}
