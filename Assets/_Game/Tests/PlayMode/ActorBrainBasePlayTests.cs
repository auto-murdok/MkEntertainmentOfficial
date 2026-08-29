using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    internal class TestBrain : ActorBrainBase
    {
        public readonly ActorBlackboard SharedContext = new ActorBlackboard();
        public Transform Hook;
        public int ExternalInteractions;

        private void Awake()
        {
            Context = SharedContext;
            _hitPoints = 100f;
        }

        public override Transform victimHook => Hook != null ? Hook : transform;
        public override bool isPreparing => false;

        public override void OnExternalInteraction(IInteractable interactable)
        {
            ExternalInteractions++;
        }

        public void CallApplyDamage(float amount) => ApplyDamage(amount);
        public void CallSetupDeathHook() => SetupDeathHook();
        public void CallDestroyActorCore() => DestroyActorCore();
        public bool HasDeathHook => Context.onDeath != null;
        public bool IsAlive => Context.isAlive;
    }

    public class ActorBrainBasePlayTests
    {
        private GameObject _host;
        private TestBrain _brain;
        private GameObject _managerHost;
        private readonly List<GameObject> _cleanup = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _managerHost = new GameObject("ManagerHost");
            _managerHost.AddComponent<InteractableManager>();
            _host = new GameObject("BrainHost");
            _brain = _host.AddComponent<TestBrain>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_managerHost);
            foreach (var go in _cleanup)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TakeDamage_ReducesHitPoints()
        {
            _brain.CallApplyDamage(25f);
            Assert.AreEqual(75f, _brain.remainingHitPoints);
        }

        [Test]
        public void TakeDamage_AccumulatesAcrossHits()
        {
            _brain.CallApplyDamage(10f);
            _brain.CallApplyDamage(15f);
            Assert.AreEqual(75f, _brain.remainingHitPoints);
        }

        [Test]
        public void TakeDamage_ClampsAtZero()
        {
            _brain.CallApplyDamage(500f);
            Assert.AreEqual(0f, _brain.remainingHitPoints);
        }

        [Test]
        public void TakeDamage_DeathFlagRaisedAtZeroHitPoints()
        {
            Assert.IsTrue(_brain.IsAlive);
            _brain.CallApplyDamage(100f);
            Assert.IsFalse(_brain.IsAlive);
        }

        [Test]
        public void TakeDamage_SublethalDamage_KeepsActorAlive()
        {
            _brain.CallApplyDamage(99f);
            Assert.IsTrue(_brain.IsAlive);
        }

        [Test]
        public void TakeDamage_ReportsToCombatLog()
        {
            _brain.CallApplyDamage(30f);
            var buffer = new string[8];
            int written = CombatLog.CopyRecent(buffer);
            StringAssert.Contains(_host.name, string.Join("|", buffer, 0, written));
        }

        [Test]
        public void SetupDeathHook_AssignsContextOnDeath()
        {
            Assert.IsFalse(_brain.HasDeathHook);
            _brain.CallSetupDeathHook();
            Assert.IsTrue(_brain.HasDeathHook);
        }

        [Test]
        public void DeathHook_EnablesRagdoll()
        {
            var body = _host.AddComponent<Rigidbody>();
            _brain.CallSetupDeathHook();
            _brain.SharedContext.onDeath();
            Assert.IsFalse(body.isKinematic);
        }

        [Test]
        public void RagdollUtils_DisableRagdoll_MakesBodiesKinematic()
        {
            var child = new GameObject("Limb");
            child.transform.SetParent(_host.transform, false);
            var limbBody = child.AddComponent<Rigidbody>();
            _cleanup.Add(child);
            RagdollUtils.DisableRagdoll(_host.transform);
            Assert.IsTrue(limbBody.isKinematic);
        }

        [Test]
        public void RagdollUtils_EnableRagdoll_UnfreezesBodiesAndInvokesCallback()
        {
            var child = new GameObject("Limb");
            child.transform.SetParent(_host.transform, false);
            var limbBody = child.AddComponent<Rigidbody>();
            _cleanup.Add(child);
            int callbacks = 0;
            RagdollUtils.EnableRagdoll(_host.transform, () => callbacks++);
            Assert.IsFalse(limbBody.isKinematic);
            Assert.AreEqual(1, callbacks);
        }

        [Test]
        public void RagdollUtils_SetRagdollState_NullCallback_IsSafe()
        {
            _host.AddComponent<Rigidbody>();
            Assert.DoesNotThrow(() => RagdollUtils.SetRagdollState(_host.transform, true, null));
        }

        [Test]
        public void RagdollUtils_EmptyHierarchy_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => RagdollUtils.EnableRagdoll(_host.transform));
        }

        [Test]
        public void InteractableId_MatchesGameObjectInstance()
        {
            Assert.AreEqual(_host.GetInstanceID(), _brain.id);
        }

        [Test]
        public void Position_MatchesTransform()
        {
            _host.transform.position = new Vector3(1f, 2f, 3f);
            Assert.AreEqual(_host.transform.position, _brain.position);
        }

        [Test]
        public void VictimHook_DefaultsToOwnTransform()
        {
            Assert.AreEqual(_host.transform, _brain.victimHook);
        }

        [UnityTest]
        public IEnumerator DestroyActorCore_RemovesNavMeshAgentAndAnimator()
        {
            var agent = _host.AddComponent<UnityEngine.AI.NavMeshAgent>();
            _host.AddComponent<Animator>();
            _brain.CallDestroyActorCore();
            yield return null;
            Assert.IsTrue(agent == null);
            Assert.IsTrue(_host.GetComponent<Animator>() == null);
        }

        [Test]
        public void OnRagdollEnabled_RemovesFromInteractableManager()
        {
            var partner = new StubInteractable(_host.GetInstanceID() + 1);
            InteractableManager.Instance.AddInteractable(_brain);
            InteractableManager.Instance.AddInteractable(partner);

            InteractableManager.Instance.Interact(_brain.id, partner.id);
            Assert.AreEqual(1, _brain.ExternalInteractions);

            _brain.CallSetupDeathHook();
            _brain.SharedContext.onDeath();

            partner.InteractionCount = 0;
            _brain.ExternalInteractions = 0;
            InteractableManager.Instance.Interact(_brain.id, partner.id);
            Assert.AreEqual(0, _brain.ExternalInteractions, "Dead actor must no longer receive interactions.");
            Assert.AreEqual(0, partner.InteractionCount, "Pair interaction requires both sides to be registered.");
        }
    }
}
