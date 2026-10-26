using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    // The grounded motor's mode contract (docs/grounded_motor_and_corpse_layer.md):
    // exactly one owner of the victim's displacement per bite phase. During
    // the grab the motor must stand down entirely (ScriptedPin) so its
    // gravity/snap can never compose on top of the grapple pin; the push-off
    // runs the clip verbatim (AuthoredY); on exit the motor resumes with a
    // reset request so no stale vertical state crosses the interaction.
    public class MotorModePlayTests
    {
        private class NoOpState : State<CharacterState, CharacterStateContext>
        {
            public void EnterState(StateMachine<CharacterState, CharacterStateContext> stateMachine) { }
            public void UpdateState(StateMachine<CharacterState, CharacterStateContext> stateMachine) { }
            public void ExitState(StateMachine<CharacterState, CharacterStateContext> stateMachine) { }
            public void CheckTransitions(StateMachine<CharacterState, CharacterStateContext> stateMachine) { }
        }

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
            _attackerGo.transform.position = new Vector3(0f, 940f, 61f);
            _attacker = _attackerGo.AddComponent<FakePinnedVictim>();
            _attacker.IsPreparing = true;
            _attacker.IsBiting = true;

            _victimGo = new GameObject("MotorModeVictim");
            _victimGo.transform.position = new Vector3(0f, 940f, 60f);
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
        public IEnumerator TakeBite_DrivesMotorModes_PinThenAuthoredYThenResetOnExit()
        {
            yield return null;
            _machine.ChangeState(CharacterState.TakingBite);
            yield return null;
            yield return null;

            Assert.AreEqual(MotorMode.ScriptedPin, _machine._context.motorMode,
                "During the grab the motor must stand down: the pin owns displacement.");

            _attacker.IsPreparing = false;
            yield return null;
            yield return null;

            Assert.AreEqual(MotorMode.AuthoredY, _machine._context.motorMode,
                "The push-off phase runs her clip verbatim, authored Y included.");

            _machine._context.isBeingAttacked = false;
            yield return null;
            yield return null;
            yield return null;

            Assert.AreEqual(MotorMode.Locomotion, _machine._context.motorMode,
                "Exit hands displacement back to the grounded motor.");
            Assert.IsTrue(_machine._context.motorResetRequested,
                "Exit must request a motor reset so no stale vertical state crosses the bite.");
        }
    }

    // Corpse teardown (docs/grounded_motor_and_corpse_layer.md): on death the
    // whole hierarchy moves to the Corpse layer, the motor's own body stops
    // colliding (zombie root capsule off + root rigidbody kinematic; player
    // CharacterController off), the root stays where the actor died, and the
    // ragdoll bones freeze kinematic once settled.
    public class CorpseTeardownPlayTests
    {
        private const float LaneY = 940f;
        private const float LaneZ = 80f;

        private GameObject _floor;
        private GameObject _host;
        private TestBrain _brain;
        private InteractableRegistry _registry;
        private readonly List<GameObject> _cleanup = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _registry = ScriptableObject.CreateInstance<InteractableRegistry>();

            _floor = new GameObject("CorpseLaneFloor");
            _floor.transform.position = new Vector3(0f, LaneY - 0.5f, LaneZ);
            var floorCollider = _floor.AddComponent<BoxCollider>();
            floorCollider.size = new Vector3(12f, 1f, 12f);
            _cleanup.Add(_floor);
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_registry);
            foreach (var go in _cleanup)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
        }

        private TestBrain MakeHost(bool playerShape)
        {
            _host = new GameObject(playerShape ? "CorpseHostPlayer" : "CorpseHostZombie");
            _host.transform.position = new Vector3(0f, LaneY + 1f, LaneZ);
            _brain = _host.AddComponent<TestBrain>();
            var so = new UnityEditor.SerializedObject(_brain);
            so.FindProperty("_registry").objectReferenceValue = _registry;
            so.ApplyModifiedProperties();

            if (playerShape)
            {
                _host.AddComponent<CharacterController>();
            }
            else
            {
                var capsule = _host.AddComponent<CapsuleCollider>();
                capsule.radius = 0.3f;
                capsule.height = 1.8f;
                capsule.center = new Vector3(0f, 0.9f, 0f);
                var body = _host.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
            }

            var bone = new GameObject("Hips");
            bone.transform.SetParent(_host.transform, false);
            bone.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            bone.AddComponent<Rigidbody>();
            var boneCollider = bone.AddComponent<CapsuleCollider>();
            boneCollider.radius = 0.15f;
            boneCollider.height = 0.5f;

            _brain.CallSetupDeathHook();
            return _brain;
        }

        [UnityTest]
        public IEnumerator ZombieCorpse_RootBodyOffCorpseLayerRootStaysAndBonesFreeze()
        {
            MakeHost(playerShape: false);
            yield return null;

            _brain.MirrorHitPoints(0f);
            yield return null;

            int corpseLayer = LayerMask.NameToLayer(LayerUtils.CorpseLayerName);
            Assert.GreaterOrEqual(corpseLayer, 0, "Precondition: the Corpse layer must exist.");
            Assert.AreEqual(corpseLayer, _host.layer, "Corpse root must move to the Corpse layer.");
            var bone = _host.transform.Find("Hips");
            Assert.AreEqual(corpseLayer, bone.gameObject.layer, "Corpse bones must move to the Corpse layer.");

            Assert.IsFalse(_host.GetComponent<CapsuleCollider>().enabled,
                "The zombie's root capsule must stop colliding at death.");
            Assert.IsTrue(_host.GetComponent<Rigidbody>().isKinematic,
                "The zombie's root rigidbody must go kinematic at death so the root stays put.");

            // Bones settle on the floor, fall asleep, and the corpse freezes.
            float waited = 0f;
            var boneBody = bone.GetComponent<Rigidbody>();
            while (!boneBody.isKinematic && waited < 8f)
            {
                yield return new WaitForSeconds(0.25f);
                waited += 0.25f;
            }
            Assert.IsTrue(boneBody.isKinematic, "Settled corpse bones must freeze kinematic.");
            Assert.AreEqual(LaneY + 1f, _host.transform.position.y, 0.05f,
                "The corpse root must stay where the actor died.");
        }

        [UnityTest]
        public IEnumerator PlayerCorpse_CharacterControllerDisabledAndCorpseLayer()
        {
            MakeHost(playerShape: true);
            yield return null;

            _brain.MirrorHitPoints(0f);
            yield return null;

            int corpseLayer = LayerMask.NameToLayer(LayerUtils.CorpseLayerName);
            Assert.AreEqual(corpseLayer, _host.layer, "Corpse root must move to the Corpse layer.");
            Assert.IsFalse(_host.GetComponent<CharacterController>().enabled,
                "A dead player's CharacterController must stop blocking the other controllers.");
        }
    }
}
