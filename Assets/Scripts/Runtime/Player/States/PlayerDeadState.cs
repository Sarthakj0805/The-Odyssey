using UnityEngine;

namespace Odyssey.Player.States
{
    /// <summary>Terminal state; the RunManager respawns the player (or ends the run).</summary>
    public sealed class PlayerDeadState : PlayerStateBase
    {
        public PlayerDeadState(PlayerController p) : base(p) { }
        public override void Enter()
        {
            base.Enter();
            P.Hitbox.Close();
            P.Body.linearVelocity = new Vector2(0f, P.Body.linearVelocity.y);
        }
        public override void FixedTick(float dt) => P.MoveHorizontal(0f, dt);
    }
}
