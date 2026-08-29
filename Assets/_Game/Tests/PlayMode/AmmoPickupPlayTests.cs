using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    // Ammo-pickup consumption driven directly through TryPickup (no physics
    // simulation): the pickup must grant reserve to a target carrying a Weapon
    // and be consumed exactly once; targets without a Weapon are ignored.
    public class AmmoPickupPlayTests
    {
        private GameObject _pickupHost;
        private GameObject _player;
        private GameObject _gun;
        private AmmoPickup _pickup;
        private Handgun _handgun;
        private readonly List<Object> _cleanup = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _pickupHost = new GameObject("AmmoPickup");
            _pickupHost.AddComponent<BoxCollider>();
            _pickup = _pickupHost.AddComponent<AmmoPickup>();

            // Player root with a Weapon+Handgun rig child (mirrors the real
            // hierarchy: the gun sits deep under the player root).
            _player = new GameObject("Player");
            _gun = new GameObject("Gun");
            _gun.transform.SetParent(_player.transform, false);
            _gun.AddComponent<Handgun>();
            _gun.AddComponent<Weapon>();
            _handgun = _gun.GetComponent<Handgun>();
            _handgun.Prepare(5, 10);
        }

        [TearDown]
        public void TearDown()
        {
            if (_pickupHost != null) Object.DestroyImmediate(_pickupHost);
            if (_player != null) Object.DestroyImmediate(_player);
            foreach (var obj in _cleanup)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
        }

        [Test]
        public void TryPickup_WeaponTarget_GrantsReserveAndConsumes()
        {
            bool consumed = _pickup.TryPickup(_player);
            Assert.IsTrue(consumed);
            Assert.AreEqual(25, _handgun.reserveAmmo);
        }

        [Test]
        public void TryPickup_SecondTime_IsRejected()
        {
            Assert.IsTrue(_pickup.TryPickup(_player));
            Assert.IsFalse(_pickup.TryPickup(_player));
            Assert.AreEqual(25, _handgun.reserveAmmo);
        }

        [Test]
        public void TryPickup_TargetWithoutWeapon_IsIgnored()
        {
            var zombie = new GameObject("Zombie");
            _cleanup.Add(zombie);
            bool consumed = _pickup.TryPickup(zombie);
            Assert.IsFalse(consumed);
            Assert.AreEqual(10, _handgun.reserveAmmo);
        }

        [Test]
        public void TryPickup_NullTarget_IsSafe()
        {
            Assert.DoesNotThrow(() => _pickup.TryPickup(null));
        }

        [UnityTest]
        public IEnumerator Pickup_PrefabAssignment_ExistsOnZombieData()
        {
            // The Walker/Runner assets must reference the drop prefab so the
            // economy is live in the arena (verified via the real assets).
            ZombieData walker = UnityEditor.AssetDatabase.LoadAssetAtPath<ZombieData>(
                "Assets/_Game/Data/Enemies/ZombieData_Walker.asset");
            ZombieData runner = UnityEditor.AssetDatabase.LoadAssetAtPath<ZombieData>(
                "Assets/_Game/Data/Enemies/ZombieData_Runner.asset");
            Assert.IsNotNull(walker, "ZombieData_Walker asset missing.");
            Assert.IsNotNull(runner, "ZombieData_Runner asset missing.");
            Assert.IsNotNull(walker.ammoDropPrefab, "Walker must drop ammo.");
            Assert.IsNotNull(runner.ammoDropPrefab, "Runner must drop ammo.");
            yield return null;
        }
    }
}
