using Odyssey.Combat;
using UnityEngine;

namespace Odyssey.Player.States
{
    /// <summary>
    /// Spear thrust driven by frame data: windup -> active (hitbox open) -> recovery.
    /// Durations are divided by the attack-speed stat (Apollo boon).
    /// </summary>
    public sealed class PlayerAttackState : PlayerStateBase
    {
        private float _windup, _active, _recovery, _t;
        public int Phase { get; private set; }   // 0 windup, 1 active, 2 recovery
        public bool Finished => _t >= _windup + _active + _recovery;

        public PlayerAttackState(PlayerController p) : base(p) { }

        public override void Enter()
        {
            base.Enter();
            P.Input.ClearAttack();
            float speed = P.AttackSpeedStat;
            _windup = P.Config.AttackWindup / speed;
            _active = P.Config.AttackActive / speed;
            _recovery = P.Config.AttackRecovery / speed;
            _t = 0f; Phase = 0;
            if (Mathf.Abs(P.Input.Move) > 0.01f) P.Facing = Mathf.Sign(P.Input.Move);
            P.NotifyAttackStarted();
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            _t += dt;
            int phase = _t < _windup ? 0 : _t < _windup + _active ? 1 : 2;
            if (phase != Phase)
            {
                if (phase == 1) P.Hitbox.Open(MakeDamage, P.Facing);
                if (phase == 2) P.Hitbox.Close();
                Phase = phase;
            }
        }

        public override void FixedTick(float dt)
        {
            // keep a little air control, mostly plant the feet on the ground
            P.MoveHorizontal(P.Grounded ? 0f : P.Input.Move, dt, P.Grounded ? 0.5f : 0.6f);
            P.ApplyGravityShaping();
            P.Hitbox.Sweep();
        }

        public override void Exit() => P.Hitbox.Close();

        private DamageInfo MakeDamage() =>
            new DamageInfo(P.DamageStat, P.Config.AttackKnockback * P.KnockbackStat, Team.Player, P.gameObject);
    }
}
