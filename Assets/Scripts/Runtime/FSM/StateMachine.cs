using System;
using System.Collections.Generic;

namespace Odyssey.FSM
{
    /// <summary>
    /// Generic finite state machine shared by the player and every enemy.
    /// All transitions are registered in one place (the owner's setup method) as
    /// (from, to, condition) triples; states themselves never switch state directly.
    /// "Any" transitions (e.g. -> Hurt, -> Dead) are checked first and win ties.
    /// </summary>
    public sealed class StateMachine
    {
        private readonly struct Transition
        {
            public readonly IState To;
            public readonly Func<bool> Condition;
            public Transition(IState to, Func<bool> condition) { To = to; Condition = condition; }
        }

        private readonly Dictionary<IState, List<Transition>> _transitions = new Dictionary<IState, List<Transition>>();
        private readonly List<Transition> _anyTransitions = new List<Transition>();
        private static readonly List<Transition> Empty = new List<Transition>(0);

        public IState Current { get; private set; }
        public IState Previous { get; private set; }

        /// <summary>Raised after every state change: (from, to).</summary>
        public event Action<IState, IState> StateChanged;

        public void AddTransition(IState from, IState to, Func<bool> condition)
        {
            if (from == null || to == null || condition == null) throw new ArgumentNullException();
            if (!_transitions.TryGetValue(from, out var list))
            {
                list = new List<Transition>();
                _transitions[from] = list;
            }
            list.Add(new Transition(to, condition));
        }

        /// <summary>Transition that can fire from any state (except into itself).</summary>
        public void AddAnyTransition(IState to, Func<bool> condition)
        {
            if (to == null || condition == null) throw new ArgumentNullException();
            _anyTransitions.Add(new Transition(to, condition));
        }

        /// <summary>Force a state change (used for the initial state and for respawns).</summary>
        public void SetState(IState next)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));
            if (next == Current) return;
            Current?.Exit();
            Previous = Current;
            Current = next;
            Current.Enter();
            StateChanged?.Invoke(Previous, Current);
        }

        /// <summary>Evaluate transitions, then tick the active state.</summary>
        public void Tick(float dt)
        {
            var next = FindTransition();
            if (next != null) SetState(next);
            Current?.Tick(dt);
        }

        public void FixedTick(float dt) => Current?.FixedTick(dt);

        private IState FindTransition()
        {
            foreach (var t in _anyTransitions)
                if (t.To != Current && t.Condition()) return t.To;

            var list = Current != null && _transitions.TryGetValue(Current, out var l) ? l : Empty;
            foreach (var t in list)
                if (t.Condition()) return t.To;

            return null;
        }
    }
}
