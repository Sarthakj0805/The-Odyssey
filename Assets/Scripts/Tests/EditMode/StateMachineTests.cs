using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.FSM;

namespace Odyssey.Tests
{
    public class StateMachineTests
    {
        private sealed class Probe : StateBase
        {
            private readonly string _name; private readonly List<string> _log;
            public Probe(string name, List<string> log) { _name = name; _log = log; }
            public override string Name => _name;
            public override void Enter() { base.Enter(); _log.Add("enter " + _name); }
            public override void Exit() { _log.Add("exit " + _name); }
        }

        [Test]
        public void Transition_Fires_When_Condition_True_And_Calls_Exit_Then_Enter()
        {
            var log = new List<string>();
            var a = new Probe("A", log); var b = new Probe("B", log);
            bool go = false;
            var fsm = new StateMachine();
            fsm.AddTransition(a, b, () => go);
            fsm.SetState(a);

            fsm.Tick(0.1f);
            Assert.AreSame(a, fsm.Current);

            go = true;
            fsm.Tick(0.1f);
            Assert.AreSame(b, fsm.Current);
            Assert.AreSame(a, fsm.Previous);
            CollectionAssert.AreEqual(new[] { "enter A", "exit A", "enter B" }, log);
        }

        [Test]
        public void AnyTransition_Has_Priority_Over_Local_Transitions()
        {
            var log = new List<string>();
            var idle = new Probe("Idle", log); var run = new Probe("Run", log); var hurt = new Probe("Hurt", log);
            var fsm = new StateMachine();
            fsm.AddTransition(idle, run, () => true);
            fsm.AddAnyTransition(hurt, () => true);
            fsm.SetState(idle);
            fsm.Tick(0f);
            Assert.AreSame(hurt, fsm.Current);
        }

        [Test]
        public void AnyTransition_Does_Not_Reenter_Current_State()
        {
            var log = new List<string>();
            var dead = new Probe("Dead", log);
            var fsm = new StateMachine();
            fsm.AddAnyTransition(dead, () => true);
            fsm.SetState(dead);
            fsm.Tick(0f); fsm.Tick(0f);
            Assert.AreEqual(1, log.FindAll(x => x == "enter Dead").Count);
        }

        [Test]
        public void StateChanged_Event_Reports_From_And_To()
        {
            var log = new List<string>();
            var a = new Probe("A", log); var b = new Probe("B", log);
            var fsm = new StateMachine();
            IState from = null, to = null;
            fsm.StateChanged += (f, t) => { from = f; to = t; };
            fsm.SetState(a);
            fsm.SetState(b);
            Assert.AreSame(a, from); Assert.AreSame(b, to);
        }

        [Test]
        public void TimeInState_Accumulates_And_Resets_On_Enter()
        {
            var a = new Probe("A", new List<string>());
            var fsm = new StateMachine();
            fsm.SetState(a);
            fsm.Tick(0.25f); fsm.Tick(0.25f);
            Assert.AreEqual(0.5f, a.TimeInState, 1e-5);
            a.Enter();
            Assert.AreEqual(0f, a.TimeInState);
        }
    }
}
