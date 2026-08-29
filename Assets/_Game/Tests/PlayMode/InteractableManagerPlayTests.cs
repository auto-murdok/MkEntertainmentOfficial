using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public class InteractableManagerPlayTests
    {
        private GameObject _firstHost;
        private GameObject _secondHost;

        [TearDown]
        public void TearDown()
        {
            if (_firstHost != null) Object.DestroyImmediate(_firstHost);
            if (_secondHost != null) Object.DestroyImmediate(_secondHost);
        }

        [UnityTest]
        public IEnumerator Awake_SetsStaticInstance()
        {
            _firstHost = new GameObject("ManagerAwake");
            var manager = _firstHost.AddComponent<InteractableManager>();
            yield return null;
            Assert.AreEqual(manager, InteractableManager.Instance);
        }

        [UnityTest]
        public IEnumerator DuplicateManager_ComponentDestroyedButGameObjectSurvives()
        {
            _firstHost = new GameObject("ManagerFirst");
            var first = _firstHost.AddComponent<InteractableManager>();

            _secondHost = new GameObject("ManagerSecond");
            _secondHost.AddComponent<BoxCollider>();
            var second = _secondHost.AddComponent<InteractableManager>();

            yield return null;

            Assert.AreEqual(first, InteractableManager.Instance);
            Assert.IsTrue(second == null, "Duplicate manager component should be destroyed.");
            Assert.IsFalse(_secondHost == null, "Sibling components must survive â€” only the component is destroyed.");
            Assert.IsNotNull(_secondHost.GetComponent<BoxCollider>());
        }

        [UnityTest]
        public IEnumerator DestroyedManager_ClearsStaticInstance()
        {
            _firstHost = new GameObject("ManagerSolo");
            _firstHost.AddComponent<InteractableManager>();
            yield return null;
            Assert.IsNotNull(InteractableManager.Instance);

            Object.Destroy(_firstHost);
            yield return null;
            Assert.IsNull(InteractableManager.Instance);
        }
    }
}
