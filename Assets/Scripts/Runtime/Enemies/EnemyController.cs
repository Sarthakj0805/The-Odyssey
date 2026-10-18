using System;
using Odyssey.Combat;
using Odyssey.Core;
using Odyssey.Enemies.States;
using Odyssey.FSM;
using UnityEngine;

namespace Odyssey.Enemies
{
    /// <summary>
    /// Shared enemy brain: the same <see cref="StateMachine"/> as the player with
    /// Patrol / Chase / Attack / Hurt / Dead states. Melee vs ranged is chosen by config data.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(Health))]
    public class EnemyController : MonoBehaviour
    {
        public EnemyConfig Config { get; private set; }
        public float Tier { get; private set; } = 1f;
        public Rigidbody2D Body { get; private set; }
        public BoxCollider2D Collider { get; private set; }
        public Health Health { get; private set; }
        public Hitbox Hitbox { get; private set; }
        public Transform Target { get; set; }
        public StateMachine Machine { get; private set; }
        public float Facing { get; set; } = -1f;
        public float LastAttackTime { get; set; } = -99f;
        public bool HurtPending { get; private set; }
        public DamageInfo LastHit { get; private set; }
        public float DamageStat => Config.Damage * Tier;

        public event Action<EnemyController, DamageInfo> Died;

        private SpriteRenderer _visual, _eye;
        private float _flash;

        /// <summary>Spawn-time setup. tier scales HP and damage (Director difficulty).</summary>
        public void Init(EnemyConfig config, float tier, Transform target)
        {
            Config = config; Tier = tier; Target = target;
            gameObject.layer = Layers.Enemy;
            name = config.DisplayName;

            Body = GetComponent<Rigidbody2D>();
            Body.constraints = RigidbodyConstraints2D.FreezeRotation;
            Body.gravityScale = 3f;
            Collider = GetComponent<BoxCollider2D>();
            Collider.size = config.Size;
            Collider.sharedMaterial = new PhysicsMaterial2D("NoFriction") { friction = 0f };

            Health = GetComponent<Health>();
            Health.Configure(Team.Enemy, config.MaxHealth * tier, 0.05f);
            Health.Hit += (info, applied) => { LastHit = info; _flash = 0.12f; if (Config.StaggerOnHit) HurtPending = true; };
            Health.Died += info => Died?.Invoke(this, info);

            _visual = Placeholder.AddSprite(transform, "Body", config.Sprite, config.Tint, config.Size, Vector2.zero, 5);
            _eye = Placeholder.AddSprite(transform, "Eye", Placeholder.Circle, Color.white,
                new Vector2(0.28f, 0.28f) * Mathf.Max(1f, config.Size.y / 1.4f), new Vector2(0.2f, config.Size.y * 0.3f), 6);

            var hb = new GameObject("Hitbox");
            hb.transform.SetParent(transform, false);
            Hitbox = hb.AddComponent<Hitbox>();
            Hitbox.Team = Team.Enemy;
            Hitbox.Size = config.HitboxSize;
            Hitbox.LocalOffset = config.HitboxOffset;

            BuildStateMachine();
        }

        private void BuildStateMachine()
        {
            Machine = new StateMachine();
            var patrol = new EnemyPatrolState(this);
            var chase = new EnemyChaseState(this);
            IState attack = Config.Kind == EnemyKind.Ranged ? new EnemyRangedAttackState(this) : (IState)new EnemyMeleeAttackState(this);
            var hurt = new EnemyHurtState(this);
            var dead = new EnemyDeadState(this);

            bool CooldownReady() => Time.time - LastAttackTime >= Config.AttackCooldown;
            bool AttackDone() => attack is EnemyAttackStateBase a && a.Finished;

            Machine.AddAnyTransition(dead, () => !Health.IsAlive);
            Machine.AddAnyTransition(hurt, () => HurtPending && Health.IsAlive);

            Machine.AddTransition(patrol, chase, () => CanSeeTarget(Config.AggroRange));
            Machine.AddTransition(chase, patrol, () => !CanSeeTarget(Config.AggroRange * 1.5f));
            Machine.AddTransition(chase, attack, () => CooldownReady() && InAttackRange());
            Machine.AddTransition(attack, chase, AttackDone);
            Machine.AddTransition(hurt, chase, () => hurt.Finished);

            Machine.SetState(patrol);
        }

        private void Update()
        {
            if (Machine == null) return;
            Machine.Tick(Time.deltaTime);
            if (_visual != null)
            {
                _flash -= Time.deltaTime;
                _visual.color = _flash > 0f ? Color.white : Health.IsAlive ? Config.Tint : Color.Lerp(Config.Tint, Color.black, 0.6f);
                _visual.flipX = Facing < 0f;
                var p = _eye.transform.localPosition;
                _eye.transform.localPosition = new Vector3(Mathf.Abs(p.x) * Facing, p.y, p.z);
                _eye.color = Machine.Current is EnemyAttackStateBase ? new Color(1f, 0.3f, 0.2f) : Color.white; // telegraph
            }
        }

        private void FixedUpdate() => Machine?.FixedTick(Time.fixedDeltaTime);

        // ---------------------------------------------------------------- senses & helpers
        public bool CanSeeTarget(float range)
        {
            if (Target == null) return false;
            Vector2 d = Target.position - transform.position;
            return Mathf.Abs(d.x) <= range && Mathf.Abs(d.y) <= 3.5f;
        }

        public bool InAttackRange()
        {
            if (Target == null) return false;
            Vector2 d = Target.position - transform.position;
            return Mathf.Abs(d.x) <= Config.AttackRange && Mathf.Abs(d.y) <= 2.5f;
        }

        public float DirectionToTarget() =>
            Target == null ? Facing : Mathf.Sign(Target.position.x - transform.position.x);

        public void SetHorizontalSpeed(float vx) => Body.linearVelocity = new Vector2(vx, Body.linearVelocity.y);

        /// <summary>True if there is ground ahead and no wall, so patrols turn at ledges.</summary>
        public bool CanWalkForward(float dir)
        {
            var b = Collider.bounds;
            var front = new Vector2(b.center.x + dir * (b.extents.x + 0.15f), b.min.y + 0.1f);
            bool ground = Physics2D.Raycast(front, Vector2.down, 0.6f, Layers.GroundMask).collider != null;
            bool wall = Physics2D.Raycast(new Vector2(b.center.x, b.center.y), new Vector2(dir, 0f), b.extents.x + 0.2f, Layers.GroundMask).collider != null;
            return ground && !wall;
        }

        public void ClearHurt() => HurtPending = false;

        public void SpawnProjectile()
        {
            var go = new GameObject("Rock");
            go.layer = Layers.Projectile;
            go.transform.position = transform.position + new Vector3(Facing * 0.5f, Config.Size.y * 0.35f, 0f);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = Config.ProjectileGravity;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = Config.ProjectileRadius;
            Placeholder.AddSprite(go.transform, "Visual", Placeholder.Circle, new Color(0.55f, 0.5f, 0.45f),
                Vector2.one * Config.ProjectileRadius * 2f, Vector2.zero, 7);
            var proj = go.AddComponent<Projectile>();

            // ballistic aim: reach the target's current position in a fixed flight time
            Vector2 to = Target != null ? (Vector2)(Target.position - go.transform.position) : new Vector2(Facing * 6f, 0f);
            float t = Config.ProjectileFlightTime;
            float g = -Physics2D.gravity.y * rb.gravityScale;
            var v = new Vector2(to.x / t, to.y / t + 0.5f * g * t);
            proj.Launch(v, new DamageInfo(DamageStat, Config.Knockback, Team.Enemy, gameObject));
        }
    }
}
