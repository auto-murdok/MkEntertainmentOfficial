using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode
{
    public class LayerUtilsTests
    {
        private GameObject _root;
        private readonly List<GameObject> _cleanup = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("LayerRoot");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_root);
            foreach (var go in _cleanup)
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private GameObject AddChild(string name, bool active)
        {
            var child = new GameObject(name);
            child.transform.SetParent(_root.transform, false);
            child.SetActive(active);
            return child;
        }

        [Test]
        public void SetLayer_ByLayerName_AppliesToAllChildren()
        {
            AddChild("A", true);
            AddChild("B", false);
            LayerUtils.SetLayer(_root.transform, "UI");
            foreach (Transform child in _root.GetComponentsInChildren<Transform>(true))
            {
                Assert.AreEqual(LayerMask.NameToLayer("UI"), child.gameObject.layer);
            }
        }

        [Test]
        public void SetLayer_ByIndex_AppliesToRootAndChildren()
        {
            int uiLayer = LayerMask.NameToLayer("UI");
            AddChild("A", true);
            LayerUtils.SetLayer(_root.transform, uiLayer);
            Assert.AreEqual(uiLayer, _root.layer);
            Assert.AreEqual(uiLayer, _root.transform.Find("A").gameObject.layer);
        }

        [Test]
        public void SetLayer_UnknownLayerName_LogsWarningAndKeepsLayers()
        {
            var child = AddChild("A", true);
            int before = child.layer;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Layer '.*' not found"));
            LayerUtils.SetLayer(_root.transform, "DefinitelyNotALayer");
            Assert.AreEqual(before, child.layer);
        }

        [Test]
        public void LocalPlayerLayerName_Constant()
        {
            Assert.AreEqual("LocalPlayer", LayerUtils.LocalPlayerLayerName);
        }
    }
}
