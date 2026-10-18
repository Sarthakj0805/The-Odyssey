using UnityEngine;

namespace Odyssey.Player.States
{
    /// <summary>Knockback + short stun. Invulnerability frames come from Health (HurtIFrames).</summary>
    public sealed class PlayerHurtState : PlayerStateBase
    {
        private float _t;
        public bool Finished => _t >= P.Config.HurtStunSeconds;
        public PlayerHurtState(PlayerController p) : base(p) { }

        public override void Enter()
        {
            base.Enter();
            P.ClearHurt();
            _t = 0f;
            var kb = P.LastHit.Knockback;
            if (kb.sqrMagnitude < 0.01f) kb = new Vector2(-P.Facing * 4f, 4f);
            P.Body.linearVelocity = kb;
        }

        public override void Tick(float dt) { base.Tick(dt); _t += dt; }
        public override void FixedTick(float dt) => P.ApplyGravityShaping();
    }
}
