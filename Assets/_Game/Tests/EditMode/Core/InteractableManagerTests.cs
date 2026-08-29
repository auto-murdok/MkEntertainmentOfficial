using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public class InteractableManagerTests
    {
        private GameObject _host;
        private InteractableManager _manager;
        private readonly List<GameObject> _cleanup = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("InteractableManagerHost");
            _manager = _host.AddComponent<InteractableManager>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_host);
            foreach (var go in _cleanup)
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private GameObject CreateTracked(string name)
        {
            var go = new GameObject(name);
            _cleanup.Add(go);
            return go;
        }

        [Test]
        public void AddInteractable_ThenInteractById_NotifiesBothSides()
        {
            var first = new StubInteractable(11, CreateTracked("A").transform);
            var second = new StubInteractable(22, CreateTracked("B").transform);
            _manager.AddInteractable(first);
            _manager.AddInteractable(second);

            _manager.Interact(11, 22);

            Assert.AreEqual(1, first.InteractionCount);
            Assert.AreEqual(1, second.InteractionCount);
            Assert.AreEqual(second, first.LastInteractionPartner);
            Assert.AreEqual(first, second.LastInteractionPartner);
        }

        [Test]
        public void AddInteractable_SameId_OverwritesPreviousRegistration()
        {
            var original = new StubInteractable(10);
            var replacement = new StubInteractable(10);
            _manager.AddInteractable(original);
            _manager.AddInteractable(replacement);

            var other = new StubInteractable(99);
            _manager.AddInteractable(other);
            _manager.Interact(10, 99);

            Assert.AreEqual(0, original.InteractionCount);
            Assert.AreEqual(1, replacement.InteractionCount);
        }

        [Test]
        public void RemoveInteractable_StopsIdLookup()
        {
            var first = new StubInteractable(1);
            var second = new StubInteractable(2);
            _manager.AddInteractable(first);
            _manager.AddInteractable(second);
            _manager.RemoveInteractable(first);

            _manager.Interact(1, 2);

            Assert.AreEqual(0, first.InteractionCount);
            Assert.AreEqual(0, second.InteractionCount);
        }

        [Test]
        public void Interact_ByReferences_NotifiesBothSides()
        {
            var first = new StubInteractable(1);
            var second = new StubInteractable(2);
            _manager.Interact(first, second);
            Assert.AreEqual(1, first.InteractionCount);
            Assert.AreEqual(1, second.InteractionCount);
        }

        [Test]
        public void AddInteractable_Null_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _manager.AddInteractable(null));
        }

        [Test]
        public void RemoveInteractable_Null_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _manager.RemoveInteractable(null));
        }

        [Test]
        public void Interact_ById_UnknownId_NotifiesNobody()
        {
            var registered = new StubInteractable(5);
            _manager.AddInteractable(registered);
            _manager.Interact(5, 404);
            _manager.Interact(404, 404);
            Assert.AreEqual(0, registered.InteractionCount);
        }
    }
}
