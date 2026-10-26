using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    // Victim side of the bite timeline: the take-bite ends when her own
    // push completes — takeBiteDuration, authored to her clip — NOT when
    // the attacker's shorter FSM bite ends. Drives CharacterTakeBiteState
    // through a bare state machine with a fake attacker whose phase flags
    // are set by hand (the FakePinnedVictim double from
    // ZombieHandAttackPlayTests).
    public class CharacterTakeBitePlayTests
    {
        private class NoOpState : State<CharacterState, CharacterStateContext>
        {
            public void EnterState(StateMachine<CharacterState, CharacterStateContext> stateMachine) { }
            public void UpdateState(StateMachine<CharacterState, CharacterStateContext> stateMachine) { }
            public void ExitState(StateMachine<CharacterState, CharacterStateContext> stateMachine) { }
            public void CheckTransitions(StateMachine<CharacterState, CharacterStateContext> stateMachine) { }
        }

        // Unity cannot AddComponent an open generic StateMachine<,> — the
        // codebase pattern is a concrete subclass (cf. TestFsm in
        // StateMachineLifecyclePlayTests, Handgun in production).
        private class TestMachine : StateMachine<CharacterState, CharacterStateContext> { }

        private GameObject _victimGo;
        private GameObject _attackerGo;
        private FakePinnedVictim _attacker;
        private TestMachine _machine;
        private PlayerData _data;

        [SetUp]
        public void SetUp()
        {
            _attackerGo = new GameObject("FakeBiter");
            _attackerGo.transform.position = new Vector3(0f, 820f, 1f);
            _attacker = _attackerGo.AddComponent<FakePinnedVictim>();
            _attacker.IsPreparing = true;
            _attacker.IsBiting = true;

            _victimGo = new GameObject("TakeBiteVictim");
            _victimGo.transform.position = new Vector3(0f, 820f, 0f);
            _machine = _victimGo.AddComponent<TestMachine>();
            _machine.states[CharacterState.TakingBite] = new CharacterTakeBiteState();
            _machine.states[CharacterState.Idle] = new NoOpState();

            _data = ScriptableObject.CreateInstance<PlayerData>();
            _machine._context.data = _data;
            _machine._context.attacker = _attacker;
            _machine._context.isBeingAttacked = true;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_victimGo);
            Object.DestroyImmediate(_attackerGo);
            Object.DestroyImmediate(_data);
        }

        [UnityTest]
        public IEnumerator VictimFreedAtPushCompletion_WhenDurationElapses()
        {
            // Start enters Idle; request TakingBite and let the deferred
            // transition land, then tick the state once.
            yield return null;
            _machine.ChangeState(CharacterState.TakingBite);
            yield return null;
            yield return null;

            // Grab phase, blend just started: she is being pulled toward the
            // hook (z=1) but must NOT be teleported there on the first frames.
            Assert.IsTrue(_machine._context.isBeingAttacked);
            Assert.Less(_victimGo.transform.position.z, 0.5f,
                "victim should ease into the hook, not snap to it");

            // Blend complete (0.3s): locked at the hook, still in the grab.
            yield return new WaitForSeconds(0.45f);
            Assert.IsTrue(_machine._context.isBeingAttacked);
            Assert.Greater(_victimGo.transform.position.z, 0.9f,
                "victim should be locked to the attacker's hook once the blend completes");

            // Release phase (push-off): bite still in progress, victim held.
            _attacker.IsPreparing = false;
            yield return null;
            Assert.IsTrue(_machine._context.isBeingAttacked);

            // The attacker's bite is over — but she is NOT free yet: her
            // release is her push, which completes on her clip's clock
            // (takeBiteDuration), not on his FSM clock.
            _attacker.IsBiting = false;
            yield return null;
            Assert.IsTrue(_machine._context.isBeingAttacked,
                "victim stays in take-bite after the attacker's bite ends; her push has not completed");

            // Duration elapsed (3s from entry; ~0.5s already spent above):
            // the push is done and she is free again.
            yield return new WaitForSeconds(2.8f);
            Assert.IsFalse(_machine._context.isBeingAttacked);
        }
    }
}
