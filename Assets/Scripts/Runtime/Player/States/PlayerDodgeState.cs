using UnityEngine;

namespace Odyssey.Player.States
{
    /// <summary>Short dash with invulnerability frames; gravity off for the duration.</summary>
    public sealed class PlayerDodgeState : PlayerStateBase
    {
        private float _t, _dir;
        public bool Finished => _t >= P.Config.DodgeDuration;
        public PlayerDodgeState(PlayerController p) : base(p) { }

        public override void Enter()
        {
            base.Enter();
            P.Input.ClearDodge();
            P.LastDodgeTime = Time.time;
            _t = 0f;
            _dir = Mathf.Abs(P.Input.Move) > 0.01f ? Mathf.Sign(P.Input.Move) : P.Facing;
            P.Facing = _dir;
            P.Health.Model.GrantInvulnerability(P.Config.DodgeDuration + 0.05f);
            P.Body.gravityScale = 0f;
        }

        public override void Tick(float dt) { base.Tick(dt); _t += dt; }

        public override void FixedTick(float dt) =>
            P.Body.linearVelocity = new Vector2(_dir * P.Config.DodgeSpeed, 0f);

        public override void Exit()
        {
            P.Body.gravityScale = P.Config.GravityScale;
            var v = P.Body.linearVelocity;
            P.Body.linearVelocity = new Vector2(v.x * 0.4f, v.y);
        }
    }
}
