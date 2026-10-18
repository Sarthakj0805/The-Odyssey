namespace Odyssey.Player.States
{
    public sealed class PlayerFallState : PlayerStateBase
    {
        public PlayerFallState(PlayerController p) : base(p) { }
        public override void FixedTick(float dt)
        {
            P.MoveHorizontal(P.Input.Move, dt, 0.8f);
            P.ApplyGravityShaping();
        }
    }
}
