namespace Odyssey.Player.States
{
    public sealed class PlayerRunState : PlayerStateBase
    {
        public PlayerRunState(PlayerController p) : base(p) { }
        public override void FixedTick(float dt)
        {
            P.ApplyGravityShaping();
            P.MoveHorizontal(P.Input.Move, dt);
        }
    }
}
