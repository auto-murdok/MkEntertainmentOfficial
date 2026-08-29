using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    internal class TestFsm : StateMachine<PlayKey, PlayContext> { }
    internal class PlayStateC : State<PlayKey, PlayContext>
    {
        public int Enters;

        public void EnterState(StateMachine<PlayKey, PlayContext> fsm) => Enters++;
        public void ExitState(StateMachine<PlayKey, PlayContext> fsm) { }
        public void UpdateState(StateMachine<PlayKey, PlayContext> fsm) { }
        public void CheckTransitions(StateMachine<PlayKey, PlayContext> fsm) { }
    }

    public class StateMachineLifecyclePlayTests
    {
        private GameObject _host;
        private StateMachine<PlayKey, PlayContext> _fsm;
        private PlayStateA _stateA;
        private PlayStateB _stateB;
        private PlayStateC _stateC;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("PlayFsmLifecycleHost");
            _fsm = _host.AddComponent<TestFsm>();
            _stateA = new PlayStateA();
            _stateB = new PlayStateB();
            _stateC = new PlayStateC();
            _fsm.states[PlayKey.A] = _stateA;
            _fsm.states[PlayKey.B] = _stateB;
            _fsm.states[PlayKey.C] = _stateC;
            _fsm.states[PlayKey.Dead] = new PlayStateDead();
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_host);
        }

        [Test]
        public void Start_WithNoStates_ThrowsAssertion()
        {
            var emptyHost = new GameObject("EmptyFsmHost");
            var empty = emptyHost.AddComponent<TestFsm>();
            try
            {
                var ex = Assert.Throws<TargetInvocationException>(() =>
                {
                    typeof(StateMachine<PlayKey, PlayContext>)
                        .GetMethod("Start", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        .Invoke(empty, null);
                });
                Assert.IsInstanceOf<UnityEngine.Assertions.AssertionException>(ex.InnerException);
            }
            finally
            {
                Object.DestroyImmediate(emptyHost);
            }
        }

        [UnityTest]
        public IEnumerator ChangeState_UnregisteredState_LogsErrorAndDoesNotTransition()
        {
            yield return null;
            LogAssert.Expect(LogType.Error, new Regex("Requested state '.*' is not registered"));
            _fsm.ChangeState((PlayKey)999);
            yield return null;
            Assert.AreEqual(PlayKey.A, _fsm.CurrentStateName);
        }

        [UnityTest]
        public IEnumerator ChangeState_FirstRequestWinsWithinFrame()
        {
            yield return null;
            bool requestMultiple = false;
            _fsm.OnCommonUpdate += key =>
            {
                if (!requestMultiple) return;
                _fsm.ChangeState(PlayKey.B);
                _fsm.ChangeState(PlayKey.C);
                requestMultiple = false;
            };
            requestMultiple = true;
            yield return null;
            Assert.AreEqual(PlayKey.B, _fsm.CurrentStateName);
            Assert.AreEqual(0, _stateC.Enters);
        }

        [UnityTest]
        public IEnumerator GlobalTransition_OverridesPendingTransition()
        {
            // A normal transition is requested during OnCommonUpdate; the
            // global guard (death) evaluated later the same frame must win.
            yield return null;
            _fsm.CheckGlobalTransition = current => PlayKey.Dead;
            _fsm.OnCommonUpdate += key => _fsm.ChangeState(PlayKey.B);
            yield return null;
            Assert.AreEqual(PlayKey.Dead, _fsm.CurrentStateName);
            Assert.AreEqual(0, _stateB.Enters);
        }

        [UnityTest]
        public IEnumerator Update_RunsCommonUpdateBeforeStatePipeline()
        {
            var order = new List<PlayKey>();
            _fsm.OnCommonUpdate += key => order.Add(key);
            yield return null;
            Assert.AreEqual(PlayKey.A, order[0]);
        }

        [UnityTest]
        public IEnumerator OnStateChanged_FiresAfterTransition()
        {
            yield return null;
            PlayKey observed = default;
            int fireCount = 0;
            _fsm.OnStateChanged += key =>
            {
                observed = key;
                fireCount++;
            };
            _fsm.ChangeState(PlayKey.B);
            yield return null;
            Assert.AreEqual(1, fireCount);
            Assert.AreEqual(PlayKey.B, observed);
        }

        [UnityTest]
        public IEnumerator Context_IsCreatedAutomatically()
        {
            Assert.IsNotNull(_fsm._context);
            Assert.IsInstanceOf<PlayContext>(_fsm._context);
            yield return null;
        }

        [UnityTest]
        public IEnumerator InitialState_CanStartFromNonDefaultEnum()
        {
            Object.Destroy(_host);
            _host = new GameObject("NonDefaultFsmHost");
            var fsm = _host.AddComponent<TestFsm>();
            fsm.states[PlayKey.A] = new PlayStateA();
            fsm.states[PlayKey.B] = _stateB;
            fsm.states[PlayKey.Dead] = new PlayStateDead();
            var so = new UnityEditor.SerializedObject(fsm);
            so.FindProperty("currentStateEnum").intValue = (int)PlayKey.B;
            so.ApplyModifiedProperties();
            yield return null;
            Assert.AreEqual(PlayKey.B, fsm.CurrentStateName);
            Assert.AreEqual(1, _stateB.Enters);
        }
    }
}
