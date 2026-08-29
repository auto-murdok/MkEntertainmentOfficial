using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    // Victim that reports itself as already pinned by another attacker
    // (canBeBitten == false), standing in for a player mid-TakingBite.
    internal class FakePinnedVictim : MonoBehaviour, ISurvivor, IInteractable, IBiteTarget, IDamageable
    {
        public int DamageCalls;
        public float TotalDamage;
        public bool CanBeBitten;
        public IInteractable CurrentBiter;

        public Vector3 TargetPosition => transform.position;
        public int id => gameObject.GetInstanceID();
        public Vector3 position => transform.position;
        public Transform victimHook => transform;
        public bool isPreparing => false;
        public bool canBeBitten => CanBeBitten;
        public IInteractable currentBiter => CurrentBiter;
        public float remainingHitPoints => 0f;

        public void TakeDamage(float amount)
        {
            DamageCalls++;
            TotalDamage += amount;
        }

        public void OnExternalInteraction(IInteractable interactable)
        {
        }
    }

    public class ZombieHandAttackPlayTests
    {
        private GameObject _zombie;
        private GameObject _victimGo;
        private ZombieBehavior _behavior;
        private FakePinnedVictim _victim;
        private InteractableRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            // Clear-air lane high above the arena geometry (tests run in the open scene).
            _zombie = new GameObject("HandAttackZombie");
            _zombie.transform.SetPositionAndRotation(new Vector3(0f, 820f, 0f), Quaternion.identity);
            _zombie.AddComponent<Animator>();
            _zombie.AddComponent<NavMeshAgent>();

            _victimGo = new GameObject("PinnedVictim");
            // Intentionally on the Default layer: the zombie's vision scan never
            // auto-targets it, so TryTriggerAttack/OnExternalInteraction are
            // driven deterministically by the test.
            _victimGo.transform.position = new Vector3(0f, 820f, 1f);
            _victim = _victimGo.AddComponent<FakePinnedVictim>();
            _victim.CanBeBitten = false;

            _registry = ScriptableObject.CreateInstance<InteractableRegistry>();

            _behavior = _zombie.AddComponent<ZombieBehavior>();
            ZombieBrain brain = _zombie.AddComponent<ZombieBrain>();

            var so = new UnityEditor.SerializedObject(brain);
            so.FindProperty("_registry").objectReferenceValue = _registry;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_zombie);
            Object.DestroyImmediate(_victimGo);
            Object.DestroyImmediate(_registry);
        }

        [UnityTest]
        public IEnumerator PinnedVictim_AttackSelection_StartsHandAttackNotBite()
        {
            yield return null;
            Assert.AreEqual(ZombieStates.Idle, _behavior.CurrentStateName);

            _behavior._context.target = _victim;
            Assert.IsTrue(_behavior.TryTriggerAttack());
            Assert.IsTrue(_behavior._context.isHandAttacking);
            Assert.IsFalse(_behavior._context.isBiting);
            Assert.AreEqual(0, _victim.DamageCalls);

            yield return null;
            Assert.AreEqual(ZombieStates.HandAttacking, _behavior.CurrentStateName);
        }

        [UnityTest]
        public IEnumerator HandAttack_ScoresOneDamageAndReturnsToIdle()
        {
            yield return null;

            _behavior._context.target = _victim;
            _behavior.TryTriggerAttack();
            yield return null;
            Assert.AreEqual(ZombieStates.HandAttacking, _behavior.CurrentStateName);

            // Default swing is 1.2s with the hit at 40% (0.48s) — damage 15.
            yield return new WaitForSeconds(0.7f);
            Assert.AreEqual(1, _victim.DamageCalls);
            Assert.AreEqual(15f, _victim.TotalDamage, 0.001f);

            // Swing over: back to Idle with the cooldown armed, still exactly one hit.
            yield return new WaitForSeconds(0.8f);
            Assert.AreEqual(ZombieStates.Idle, _behavior.CurrentStateName);
            Assert.IsFalse(_behavior._context.isHandAttacking);
            Assert.Greater(_behavior._context.attackCooldownTimer, 0f);
            Assert.AreEqual(1, _victim.DamageCalls);
        }

        [UnityTest]
        public IEnumerator HandAttack_HandTriggerRedirect_PinnedVictimSwingsInsteadOfBiting()
        {
            yield return null;
            ZombieBrain brain = _zombie.GetComponent<ZombieBrain>();

            brain.OnExternalInteraction(_victim);
            Assert.IsTrue(_behavior._context.isHandAttacking);
            Assert.IsFalse(_behavior._context.isBiting);
            Assert.AreEqual(0, _victim.DamageCalls);

            // Duplicate interactions while swinging must not re-fire the attack.
            brain.OnExternalInteraction(_victim);
            Assert.AreEqual(0, _victim.DamageCalls);

            yield return new WaitForSeconds(0.7f);
            Assert.AreEqual(1, _victim.DamageCalls);
        }

        [UnityTest]
        public IEnumerator PinHeldByThisZombie_OwnBiteContinues()
        {
            yield return null;
            ZombieBrain brain = _zombie.GetComponent<ZombieBrain>();

            // Regression: the victim marks itself attacked synchronously inside
            // the bite interaction — the pinning zombie must still bite and not
            // divert itself into a hand swing.
            _victim.CanBeBitten = false;
            _victim.CurrentBiter = brain;

            brain.OnExternalInteraction(_victim);
            Assert.IsTrue(_behavior._context.isBiting);
            Assert.IsFalse(_behavior._context.isHandAttacking);
        }

        [UnityTest]
        public IEnumerator FreeVictim_KeepsBiteGrabPath()
        {
            yield return null;
            _victim.CanBeBitten = true;

            _zombie.GetComponent<ZombieBrain>().OnExternalInteraction(_victim);
            Assert.IsTrue(_behavior._context.isBiting);
            Assert.IsFalse(_behavior._context.isHandAttacking);

            // Bite damage is attacker-driven and immediate (default bite 30).
            Assert.AreEqual(1, _victim.DamageCalls);
            Assert.AreEqual(30f, _victim.TotalDamage, 0.001f);
        }
    }
}
