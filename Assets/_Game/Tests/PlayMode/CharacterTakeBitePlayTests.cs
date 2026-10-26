using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    // Victim side of the bite phase fix: the take-bite ends when the
    // attacker's bite ends (asymmetric release), not only when the flat
    // takeBiteDuration timer elapses. Drives CharacterTakeBiteState through a
    // bare state machine with a fake attacker whose phase flags are set by
    // hand (the FakePinnedVictim double from ZombieHandAttackPlayTests).
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
        public IEnumerator VictimFreedWhenAttackerBiteEnds_LongBeforeDurationCap()
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

            // Bite over: the victim breaks free immediately — nowhere near the
            // 3s takeBiteDuration cap — while the zombie would still be in its
            // recovery / attack cooldown.
            _attacker.IsBiting = false;
            yield return null;
            Assert.IsFalse(_machine._context.isBeingAttacked);
        }
    }
}
