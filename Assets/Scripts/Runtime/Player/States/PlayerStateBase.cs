using Odyssey.FSM;

namespace Odyssey.Player.States
{
    public abstract class PlayerStateBase : StateBase
    {
        protected readonly PlayerController P;
        protected PlayerStateBase(PlayerController p) { P = p; }
    }
}
