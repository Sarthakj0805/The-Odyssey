using UnityEngine;

namespace Odyssey.Player.States
{
    /// <summary>Rising part of a jump. Releasing jump early cuts the upward velocity (variable height).</summary>
    public sealed class PlayerJumpState : PlayerStateBase
    {
        private bool _cut;
        public PlayerJumpState(PlayerController p) : base(p) { }

        public override void Enter()
        {
            base.Enter();
            P.Input.ClearJump();
            _cut = false;
            var v = P.Body.linearVelocity;
            P.Body.linearVelocity = new Vector2(v.x, P.Config.JumpVelocity);
        }

        public override void FixedTick(float dt)
        {
            P.MoveHorizontal(P.Input.Move, dt, 0.8f);
            var v = P.Body.linearVelocity;
            if (!_cut && !P.Input.JumpHeld && v.y > 0f)
            {
                P.Body.linearVelocity = new Vector2(v.x, v.y * P.Config.JumpCutMultiplier);
                _cut = true;
            }
            P.ApplyGravityShaping();
        }
    }
}
