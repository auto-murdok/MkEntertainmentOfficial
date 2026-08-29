using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    // The SO catalog replaces the PrefabManager singleton: same lookup
    // semantics, but the catalog is an authored asset referenced directly by
    // the consumer prefab.
    public class ItemCatalogTests
    {
        private ItemCatalog _catalog;
        private GameObject _host;
        private GameObject _itemHost;

        [SetUp]
        public void SetUp()
        {
            _catalog = ScriptableObject.CreateInstance<ItemCatalog>();
            _host = new GameObject("ItemCatalogHost");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_host);
            UnityEngine.Object.DestroyImmediate(_itemHost);
            UnityEngine.Object.DestroyImmediate(_catalog);
        }

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
            Assert.IsNull(_catalog.GetItemPrefab("anything"));
        }

        [Test]
        public void GetItemPrefab_RegisteredId_ReturnsItem()
        {
            var item = CreateItem("handgun_ammo");
            _catalog.items = new Item[] { item };
            Assert.AreEqual(item, _catalog.GetItemPrefab("handgun_ammo"));
        }

        [Test]
        public void GetItemPrefab_UnknownId_ReturnsNull()
        {
            var item = CreateItem("handgun_ammo");
            _catalog.items = new Item[] { item };
            Assert.IsNull(_catalog.GetItemPrefab("rifle_ammo"));
        }

        [Test]
        public void GetItemPrefab_EmptyArray_ReturnsNull()
        {
            _catalog.items = new Item[] { };
            Assert.IsNull(_catalog.GetItemPrefab("x"));
        }

        [Test]
        public void FindItemById_NullEntries_AreSkipped()
        {
            var item = CreateItem("bandage");
            _catalog.items = new Item[] { null, item, null };
            Assert.AreEqual(item, _catalog.GetItemPrefab("bandage"));
            Assert.IsNull(_catalog.GetItemPrefab("null-id"));
        }

        [Test]
        public void Item_IdReturnsSerializedId()
        {
            var item = CreateItem("serial_42");
            Assert.AreEqual("serial_42", item.id);
        }
    }
}
