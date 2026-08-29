using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    internal enum PlayKey
    {
        A = 0,
        B,
        C,
        Dead
    }

    internal class PlayContext : Blackboard
    {
        public bool forceDead;
    }

    internal class PlayStateA : State<PlayKey, PlayContext>
    {
        public int Enters;
        public int Exits;

        public void EnterState(StateMachine<PlayKey, PlayContext> fsm) => Enters++;
        public void ExitState(StateMachine<PlayKey, PlayContext> fsm) => Exits++;
        public void UpdateState(StateMachine<PlayKey, PlayContext> fsm) { }
        public void CheckTransitions(StateMachine<PlayKey, PlayContext> fsm) { }
    }

    internal class PlayStateB : State<PlayKey, PlayContext>
    {
        public int Enters;

        public void EnterState(StateMachine<PlayKey, PlayContext> fsm) => Enters++;
        public void ExitState(StateMachine<PlayKey, PlayContext> fsm) { }
        public void UpdateState(StateMachine<PlayKey, PlayContext> fsm) { }
        public void CheckTransitions(StateMachine<PlayKey, PlayContext> fsm) { }
    }

    internal class PlayStateDead : State<PlayKey, PlayContext>
    {
        public void EnterState(StateMachine<PlayKey, PlayContext> fsm) { }
        public void ExitState(StateMachine<PlayKey, PlayContext> fsm) { }
        public void UpdateState(StateMachine<PlayKey, PlayContext> fsm) { }
        public void CheckTransitions(StateMachine<PlayKey, PlayContext> fsm) { }
    }

    public class StateMachinePlayTests
    {
        private GameObject _host;
        private StateMachine<PlayKey, PlayContext> _fsm;
        private PlayStateA _stateA;
        private PlayStateB _stateB;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("PlayFsmHost");
            _fsm = _host.AddComponent<TestFsm>();
            _stateA = new PlayStateA();
            _stateB = new PlayStateB();
            _fsm.states[PlayKey.A] = _stateA;
            _fsm.states[PlayKey.B] = _stateB;
            _fsm.states[PlayKey.Dead] = new PlayStateDead();
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_host);
        }

        [UnityTest]
        public IEnumerator Start_EnterInitialOnFirstFrame()
        {
            yield return null;
            Assert.AreEqual(PlayKey.A, _fsm.CurrentStateName);
            Assert.AreEqual(1, _stateA.Enters);
        }

        [UnityTest]
        public IEnumerator ChangeState_TransitionAppliedWithinOneFrame()
        {
            yield return null;
            _fsm.ChangeState(PlayKey.B);
            yield return null;
            Assert.AreEqual(PlayKey.B, _fsm.CurrentStateName);
            Assert.AreEqual(1, _stateA.Exits);
            Assert.AreEqual(1, _stateB.Enters);
        }

        [UnityTest]
        public IEnumerator GlobalTransition_ForcesDeathState()
        {
            yield return null;
            _fsm.CheckGlobalTransition = key => _fsm._context.forceDead ? PlayKey.Dead : key;
            _fsm._context.forceDead = true;
            yield return null;
            Assert.AreEqual(PlayKey.Dead, _fsm.CurrentStateName);
        }

        [UnityTest]
        public IEnumerator Update_LoopsCurrentStateEachFrame()
        {
            yield return null;
            yield return null;
            yield return null;
            Assert.AreEqual(PlayKey.A, _fsm.CurrentStateName);
            Assert.AreEqual(1, _stateA.Enters);
            Assert.AreEqual(0, _stateA.Exits);
        }
    }
}
