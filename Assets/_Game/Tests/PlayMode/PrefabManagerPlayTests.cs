using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    // Same singleton contract as InteractableManager/GameStateManager: first
    // Awake wins, duplicates lose only their component (siblings survive) and
    // the static reference is cleared on teardown.
    public class PrefabManagerPlayTests
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
            _firstHost = new GameObject("PrefabManagerAwake");
            var manager = _firstHost.AddComponent<PrefabManager>();
            yield return null;
            Assert.AreEqual(manager, PrefabManager.Instance);
        }

        [UnityTest]
        public IEnumerator DuplicateManager_ComponentDestroyedButGameObjectSurvives()
        {
            _firstHost = new GameObject("PrefabManagerFirst");
            var first = _firstHost.AddComponent<PrefabManager>();

            _secondHost = new GameObject("PrefabManagerSecond");
            _secondHost.AddComponent<BoxCollider>();
            var second = _secondHost.AddComponent<PrefabManager>();

            yield return null;

            Assert.AreEqual(first, PrefabManager.Instance);
            Assert.IsTrue(second == null, "Duplicate manager component should be destroyed.");
            Assert.IsFalse(_secondHost == null, "Sibling components must survive — only the component is destroyed.");
            Assert.IsNotNull(_secondHost.GetComponent<BoxCollider>());
        }

        [UnityTest]
        public IEnumerator DestroyedManager_ClearsStaticInstance()
        {
            _firstHost = new GameObject("PrefabManagerSolo");
            _firstHost.AddComponent<PrefabManager>();
            yield return null;
            Assert.IsNotNull(PrefabManager.Instance);

            Object.Destroy(_firstHost);
            yield return null;
            Assert.IsNull(PrefabManager.Instance);
        }
    }
}
