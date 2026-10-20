using System;
using Odyssey.Combat;
using Odyssey.Core;
using Odyssey.FSM;
using Odyssey.Modifiers;
using Odyssey.Player.States;
using UnityEngine;

namespace Odyssey.Player
{
    /// <summary>
    /// Odysseus. Owns the physics body, input intent and the state machine; all behaviour
    /// lives in state classes and all transitions are declared in <see cref="BuildStateMachine"/>.
    /// Stats are read through the run's <see cref="RunModifierRegistry"/> so boons apply instantly.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(Health))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private PlayerConfig config;

        public PlayerConfig Config => config;
        public Rigidbody2D Body { get; private set; }
        public BoxCollider2D Collider { get; private set; }
        public Health Health { get; private set; }
        public Hitbox Hitbox { get; private set; }
        public PlayerInputReader Input { get; } = new PlayerInputReader();
        public RunModifierRegistry Modifiers { get; private set; } = new RunModifierRegistry();
        public StateMachine Machine { get; private set; }
        public SpriteRenderer Visual { get; private set; }

        public float Facing { get; set; } = 1f;
        public bool Grounded { get; private set; }
        public float LastGroundedTime { get; private set; } = -99f;
        public bool HurtPending { get; private set; }
        public DamageInfo LastHit { get; private set; }
        public string StateName => Machine?.Current?.Name ?? "-";

        // derived stats (base config value run through boon modifiers)
        public float MaxHealthStat => Modifiers.Evaluate(StatType.MaxHealth, config.MaxHealth);
        public float DamageStat => Modifiers.Evaluate(StatType.Damage, config.AttackDamage);
        public float MoveSpeedStat => Modifiers.Evaluate(StatType.MoveSpeed, config.MoveSpeed);
        public float AttackSpeedStat => Mathf.Max(0.25f, Modifiers.Evaluate(StatType.AttackSpeed, 1f));
        public float KnockbackStat => Modifiers.Evaluate(StatType.Knockback, 1f);
        public float HealOnKillStat => Modifiers.Evaluate(StatType.HealOnKill, 0f);

        public event Action<float> DamageTaken;             // applied amount
        public event Action Died;

        private PlayerSpear _spear;

        public void Init(PlayerConfig cfg, RunModifierRegistry registry)
        {
            config = cfg;
            Modifiers = registry ?? new RunModifierRegistry();
            Modifiers.Changed += ApplyStatChanges;
            Setup();
        }

        private bool _setUp;
        private void Awake() { if (config != null) Setup(); }

        private void Setup()
        {
            if (_setUp) return;
            _setUp = true;
            gameObject.layer = Layers.Player;
            Body = GetComponent<Rigidbody2D>();
            Body.gravityScale = config.GravityScale;
            Body.constraints = RigidbodyConstraints2D.FreezeRotation;
            Body.interpolation = RigidbodyInterpolation2D.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            Collider = GetComponent<BoxCollider2D>();
            Collider.size = new Vector2(0.7f, 1.5f);
            var mat = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };
            Collider.sharedMaterial = mat;

            Health = GetComponent<Health>();
            Health.Configure(Team.Player, MaxHealthStat, config.HurtIFrames);
            Health.Hit += OnHit;
            Health.Died += _ => Died?.Invoke();

            Visual = Placeholder.AddSprite(transform, "Body", config.Sprite, config.Tint, new Vector2(0.8f, 1.5f), Vector2.zero, 10);
            _spear = PlayerSpear.Create(transform);

            var hb = new GameObject("Hitbox");
            hb.transform.SetParent(transform, false);
            Hitbox = hb.AddComponent<Hitbox>();
            Hitbox.Team = Team.Player;
            Hitbox.Size = config.HitboxSize;
            Hitbox.LocalOffset = config.HitboxOffset;

            BuildStateMachine();
        }

        // ---------------------------------------------------------------- state machine
        private void BuildStateMachine()
        {
            Machine = new StateMachine();
            var idle = new PlayerIdleState(this);
            var run = new PlayerRunState(this);
            var jump = new PlayerJumpState(this);
            var fall = new PlayerFallState(this);
            var attack = new PlayerAttackState(this);
            var hurt = new PlayerHurtState(this);
            var dead = new PlayerDeadState(this);

            bool CanCoyoteJump() => Input.HasJump(Time.time, config.JumpBuffer) && Time.time - LastGroundedTime <= config.CoyoteTime;
            bool WantsAttack() => Input.HasAttack(Time.time, 0.15f);
            bool Airborne() => !Grounded && Time.time - LastGroundedTime > config.CoyoteTime;

            // global
            Machine.AddAnyTransition(dead, () => !Health.IsAlive && Machine.Current != dead);
            Machine.AddAnyTransition(hurt, () => HurtPending && Health.IsAlive);

            foreach (var s in new IState[] { idle, run, jump, fall })
                Machine.AddTransition(s, attack, WantsAttack);
            foreach (var s in new IState[] { idle, run })
            {
                Machine.AddTransition(s, jump, CanCoyoteJump);
                Machine.AddTransition(s, fall, Airborne);
            }
            Machine.AddTransition(idle, run, () => Mathf.Abs(Input.Move) > 0.01f);
            Machine.AddTransition(run, idle, () => Mathf.Abs(Input.Move) <= 0.01f);
            Machine.AddTransition(fall, jump, CanCoyoteJump);
            Machine.AddTransition(jump, fall, () => Body.linearVelocity.y <= 0f);
            Machine.AddTransition(fall, idle, () => Grounded);

            Machine.AddTransition(attack, idle, () => attack.Finished && Grounded);
            Machine.AddTransition(attack, fall, () => attack.Finished && !Grounded);
            Machine.AddTransition(hurt, idle, () => hurt.Finished);

            _idle = idle;
            Machine.SetState(idle);
        }
        private IState _idle;

        private void Update()
        {
            Input.Poll(Time.time);
            Machine.Tick(Time.deltaTime);
            UpdateVisuals();
        }

        private void FixedUpdate()
        {
            CheckGround();
            Machine.FixedTick(Time.fixedDeltaTime);
            var v = Body.linearVelocity;
            if (v.y < -config.MaxFallSpeed) Body.linearVelocity = new Vector2(v.x, -config.MaxFallSpeed);
        }

        private void CheckGround()
        {
            var b = Collider.bounds;
            var origin = new Vector2(b.center.x, b.min.y + 0.05f);
            var hit = Physics2D.BoxCast(origin, new Vector2(b.size.x * 0.9f, 0.1f), 0f, Vector2.down, 0.08f, Layers.GroundMask);
            Grounded = hit.collider != null && hit.collider.attachedRigidbody != Body && Body.linearVelocity.y <= 0.05f;
            if (Grounded) LastGroundedTime = Time.time;
        }

        // ---------------------------------------------------------------- helpers used by states
        public void MoveHorizontal(float input, float dt, float control = 1f)
        {
            float target = input * MoveSpeedStat;
            var v = Body.linearVelocity;
            v.x = Mathf.MoveTowards(v.x, target, config.Acceleration * control * dt);
            Body.linearVelocity = v;
            if (Mathf.Abs(input) > 0.01f) Facing = Mathf.Sign(input);
        }

        public void ApplyGravityShaping()
        {
            var v = Body.linearVelocity;
            bool falling = v.y < 0f || !Input.JumpHeld;
            Body.gravityScale = config.GravityScale * (falling ? config.FallGravityMultiplier : 1f);
        }

        public void ClearHurt() => HurtPending = false;

        private void OnHit(DamageInfo info, float applied)
        {
            LastHit = info;
            HurtPending = true;
            DamageTaken?.Invoke(applied);
        }

        private void ApplyStatChanges()
        {
            if (Health?.Model == null) return;
            Health.Model.SetMax(MaxHealthStat);
        }

        public void Respawn(Vector2 position)
        {
            transform.position = position;
            Body.linearVelocity = Vector2.zero;
            Body.simulated = true;
            Health.Model.ResetFull();
            Health.Model.GrantInvulnerability(1.5f);
            HurtPending = false;
            Machine.SetState(_idle);
        }

        private void UpdateVisuals()
        {
            if (Visual == null) return;
            var c = config.Tint;
            if (Health.Model.IsInvulnerable && Health.IsAlive) c.a = (Mathf.Sin(Time.time * 40f) > 0f) ? 0.35f : 1f;
            if (!Health.IsAlive) c = new Color(0.4f, 0.4f, 0.4f, 0.6f);
            Visual.color = c;
            Visual.flipX = Facing < 0f;
            _spear?.Pose(Facing, Machine.Current is PlayerAttackState a ? a.Phase : -1);
        }
    }

    /// <summary>Tiny visual for the spear so attacks and facing read clearly with placeholder art.</summary>
    internal sealed class PlayerSpear
    {
        private Transform _t;
        private SpriteRenderer _sr;
        public static PlayerSpear Create(Transform parent)
        {
            var s = new PlayerSpear();
            s._sr = Placeholder.AddSprite(parent, "Spear", null, new Color(0.85f, 0.85f, 0.9f), new Vector2(1.1f, 0.12f), new Vector2(0.45f, 0.1f), 11);
            s._t = s._sr.transform;
            return s;
        }
        /// <param name="phase">-1 idle, 0 windup, 1 active, 2 recovery</param>
        public void Pose(float facing, int phase)
        {
            float reach = phase == 0 ? 0.2f : phase == 1 ? 1.0f : phase == 2 ? 0.6f : 0.4f;
            _t.localPosition = new Vector3(reach * facing, 0.1f, 0f);
            _sr.color = phase == 1 ? Color.white : new Color(0.75f, 0.75f, 0.8f);
        }
    }
}
