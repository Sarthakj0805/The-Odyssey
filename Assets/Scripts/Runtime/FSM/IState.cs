namespace Odyssey.FSM
{
    /// <summary>
    /// A single behaviour state. States are plain C# classes (no MonoBehaviour) so the
    /// state machine can be unit-tested in edit mode.
    /// </summary>
    public interface IState
    {
        string Name { get; }
        void Enter();
        void Tick(float dt);        // called from Update
        void FixedTick(float dt);   // called from FixedUpdate (physics)
        void Exit();
    }

    /// <summary>Convenience base class with no-op defaults.</summary>
    public abstract class StateBase : IState
    {
        public virtual string Name => GetType().Name;
        /// <summary>Seconds since this state was entered.</summary>
        public float TimeInState { get; private set; }
        public virtual void Enter() { TimeInState = 0f; }
        public virtual void Tick(float dt) { TimeInState += dt; }
        public virtual void FixedTick(float dt) { }
        public virtual void Exit() { }
    }
}
