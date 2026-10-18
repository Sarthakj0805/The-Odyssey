namespace Odyssey.Player.States
{
    public sealed class PlayerIdleState : PlayerStateBase
    {
        public PlayerIdleState(PlayerController p) : base(p) { }
        public override void FixedTick(float dt)
        {
            P.ApplyGravityShaping();
            P.MoveHorizontal(0f, dt);
        }
    }
}
