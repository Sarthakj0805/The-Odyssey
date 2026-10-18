using Odyssey.Combat;
using Odyssey.FSM;
using UnityEngine;

namespace Odyssey.Enemies.States
{
    public abstract class EnemyStateBase : StateBase
    {
        protected readonly EnemyController E;
        protected EnemyStateBase(EnemyController e) { E = e; }
    }

    /// <summary>Walk back and forth, turning at ledges and walls.</summary>
    public sealed class EnemyPatrolState : EnemyStateBase
    {
        public EnemyPatrolState(EnemyController e) : base(e) { }
        public override void FixedTick(float dt)
        {
            if (E.Config.Kind == EnemyKind.Ranged) { E.SetHorizontalSpeed(0f); return; } // throwers hold position
            if (!E.CanWalkForward(E.Facing)) E.Facing = -E.Facing;
            E.SetHorizontalSpeed(E.Facing * E.Config.PatrolSpeed);
        }
    }

    /// <summary>Melee: close distance (without walking off ledges). Ranged: stand and face the target.</summary>
    public sealed class EnemyChaseState : EnemyStateBase
    {
        public EnemyChaseState(EnemyController e) : base(e) { }
        public override void FixedTick(float dt)
        {
            E.Facing = E.DirectionToTarget();
            if (E.Config.Kind == EnemyKind.Ranged || E.InAttackRange() || !E.CanWalkForward(E.Facing))
                E.SetHorizontalSpeed(0f);
            else
                E.SetHorizontalSpeed(E.Facing * E.Config.ChaseSpeed);
        }
    }

    /// <summary>Shared windup -> active -> recovery timing for enemy attacks (telegraphed by the red eye).</summary>
    public abstract class EnemyAttackStateBase : EnemyStateBase
    {
        protected float T;
        public bool Finished => T >= E.Config.Windup + E.Config.Active + E.Config.Recovery;
        protected bool InActive => T >= E.Config.Windup && T < E.Config.Windup + E.Config.Active;
        protected EnemyAttackStateBase(EnemyController e) : base(e) { }

        public override void Enter()
        {
            base.Enter();
            T = 0f;
            E.Facing = E.DirectionToTarget();
            E.SetHorizontalSpeed(0f);
        }

        public override void Exit() => E.LastAttackTime = Time.time;
    }

    public sealed class EnemyMeleeAttackState : EnemyAttackStateBase
    {
        public EnemyMeleeAttackState(EnemyController e) : base(e) { }
        public override void Tick(float dt)
        {
            base.Tick(dt);
            bool wasActive = InActive;
            T += dt;
            if (!wasActive && InActive)
                E.Hitbox.Open(() => new DamageInfo(E.DamageStat, E.Config.Knockback, Team.Enemy, E.gameObject), E.Facing);
            if (wasActive && !InActive) E.Hitbox.Close();
        }
        public override void FixedTick(float dt)
        {
            E.SetHorizontalSpeed(InActive ? E.Facing * 2f : 0f); // small lunge on the active frames
            E.Hitbox.Sweep();
        }
        public override void Exit() { base.Exit(); E.Hitbox.Close(); }
    }

    public sealed class EnemyRangedAttackState : EnemyAttackStateBase
    {
        private bool _thrown;
        public EnemyRangedAttackState(EnemyController e) : base(e) { }
        public override void Enter() { base.Enter(); _thrown = false; }
        public override void Tick(float dt)
        {
            base.Tick(dt);
            T += dt;
            if (!_thrown && T >= E.Config.Windup) { _thrown = true; E.SpawnProjectile(); }
        }
        public override void FixedTick(float dt) => E.SetHorizontalSpeed(0f);
    }

    public sealed class EnemyHurtState : EnemyStateBase
    {
        private float _t;
        public bool Finished => _t >= E.Config.HurtStunSeconds;
        public EnemyHurtState(EnemyController e) : base(e) { }
        public override void Enter()
        {
            base.Enter();
            E.ClearHurt();
            E.Hitbox.Close();
            _t = 0f;
            var kb = E.LastHit.Knockback * (1f - E.Config.KnockbackResistance);
            E.Body.linearVelocity = kb;
        }
        public override void Tick(float dt) { base.Tick(dt); _t += dt; }
    }

    public sealed class EnemyDeadState : EnemyStateBase
    {
        public EnemyDeadState(EnemyController e) : base(e) { }
        public override void Enter()
        {
            base.Enter();
            E.Hitbox.Close();
            E.Body.linearVelocity = Vector2.zero;
            E.Body.simulated = false;
            Object.Destroy(E.gameObject, 0.6f);
        }
    }
}
