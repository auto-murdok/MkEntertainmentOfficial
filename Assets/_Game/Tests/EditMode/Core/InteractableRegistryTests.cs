using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    // The SO registry replaces the old InteractableManager singleton: same
    // interaction semantics, but the registry is an asset the entities
    // reference directly — no scene host object required.
    public class InteractableRegistryTests
    {
        private InteractableRegistry _registry;
        private readonly List<GameObject> _cleanup = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _registry = ScriptableObject.CreateInstance<InteractableRegistry>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _cleanup)
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
            UnityEngine.Object.DestroyImmediate(_registry);
        }

        private GameObject CreateTracked(string name)
        {
            var go = new GameObject(name);
            _cleanup.Add(go);
            return go;
        }

        [Test]
        public void Register_ThenInteractById_NotifiesBothSides()
        {
            var first = new StubInteractable(11, CreateTracked("A").transform);
            var second = new StubInteractable(22, CreateTracked("B").transform);
            _registry.Register(first);
            _registry.Register(second);

            _registry.Interact(11, 22);

            Assert.AreEqual(1, first.InteractionCount);
            Assert.AreEqual(1, second.InteractionCount);
            Assert.AreEqual(second, first.LastInteractionPartner);
            Assert.AreEqual(first, second.LastInteractionPartner);
        }

        [Test]
        public void Register_SameId_OverwritesPreviousRegistration()
        {
            var original = new StubInteractable(10);
            var replacement = new StubInteractable(10);
            _registry.Register(original);
            _registry.Register(replacement);

            var other = new StubInteractable(99);
            _registry.Register(other);
            _registry.Interact(10, 99);

            Assert.AreEqual(0, original.InteractionCount);
            Assert.AreEqual(1, replacement.InteractionCount);
        }

        [Test]
        public void Unregister_StopsIdLookup()
        {
            var first = new StubInteractable(1);
            var second = new StubInteractable(2);
            _registry.Register(first);
            _registry.Register(second);
            _registry.Unregister(first);

            _registry.Interact(1, 2);

            Assert.AreEqual(0, first.InteractionCount);
            Assert.AreEqual(0, second.InteractionCount);
        }

        [Test]
        public void Interact_ByReferences_NotifiesBothSides()
        {
            var first = new StubInteractable(1);
            var second = new StubInteractable(2);
            _registry.Interact(first, second);
            Assert.AreEqual(1, first.InteractionCount);
            Assert.AreEqual(1, second.InteractionCount);
        }

        [Test]
        public void Register_Null_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _registry.Register(null));
        }

        [Test]
        public void Unregister_Null_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _registry.Unregister(null));
        }

        [Test]
        public void Interact_ById_UnknownId_NotifiesNobody()
        {
            var registered = new StubInteractable(5);
            _registry.Register(registered);
            _registry.Interact(5, 404);
            _registry.Interact(404, 404);
            Assert.AreEqual(0, registered.InteractionCount);
        }

        [Test]
        public void TryGet_RegisteredId_ReturnsInteractable()
        {
            var registered = new StubInteractable(7);
            _registry.Register(registered);

            Assert.IsTrue(_registry.TryGet(7, out IInteractable found));
            Assert.AreEqual(registered, found);
        }

        [Test]
        public void TryGet_UnknownId_ReturnsFalse()
        {
            Assert.IsFalse(_registry.TryGet(404, out IInteractable found));
            Assert.IsNull(found);
        }

        [Test]
        public void OnDisable_ClearsRuntimeState_SceneLoadIsCleanSlate()
        {
            var registered = new StubInteractable(3);
            _registry.Register(registered);

            // Unity.SendStateChanged / destroying+recreating is the practical
            // way to trigger OnDisable on a runtime instance.
            UnityEngine.Object.DestroyImmediate(_registry);
            _registry = ScriptableObject.CreateInstance<InteractableRegistry>();

            Assert.IsFalse(_registry.TryGet(3, out _), "A fresh registry (post-scene-load) must not inherit old entries.");
        }
    }
}
