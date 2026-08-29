using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public class PrefabManagerTests
    {
        private GameObject _host;
        private PrefabManager _manager;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("PrefabManagerHost");
            _manager = _host.AddComponent<PrefabManager>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_host);
            UnityEngine.Object.DestroyImmediate(_itemHost);
        }

        private GameObject _itemHost;

        private Item CreateItem(string id)
        {
            _itemHost = new GameObject("Item_" + id);
            var item = _itemHost.AddComponent<Item>();
            var so = new UnityEditor.SerializedObject(item);
            so.FindProperty("_id").stringValue = id;
            so.ApplyModifiedProperties();
            return item;
        }

        [Test]
        public void GetItemPrefab_NullItems_ReturnsNull()
        {
            Assert.IsNull(_manager.GetItemPrefab("anything"));
        }

        [Test]
        public void GetItemPrefab_RegisteredId_ReturnsItem()
        {
            var item = CreateItem("handgun_ammo");
            _manager.items = new Item[] { item };
            Assert.AreEqual(item, _manager.GetItemPrefab("handgun_ammo"));
        }

        [Test]
        public void GetItemPrefab_UnknownId_ReturnsNull()
        {
            var item = CreateItem("handgun_ammo");
            _manager.items = new Item[] { item };
            Assert.IsNull(_manager.GetItemPrefab("rifle_ammo"));
        }

        [Test]
        public void GetItemPrefab_EmptyArray_ReturnsNull()
        {
            _manager.items = new Item[] { };
            Assert.IsNull(_manager.GetItemPrefab("x"));
        }

        [Test]
        public void FindItemById_NullEntries_AreSkipped()
        {
            var item = CreateItem("bandage");
            _manager.items = new Item[] { null, item, null };
            Assert.AreEqual(item, _manager.GetItemPrefab("bandage"));
            Assert.IsNull(_manager.GetItemPrefab("null-id"));
        }

        [Test]
        public void Instance_FindsManagerInScene()
        {
            Assert.AreEqual(_manager, PrefabManager.Instance);
        }

        [Test]
        public void Item_IdReturnsSerializedId()
        {
            var item = CreateItem("serial_42");
            Assert.AreEqual("serial_42", item.id);
        }
    }
}
