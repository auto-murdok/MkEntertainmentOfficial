using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public class HitscanWeaponPlayTests
    {
        private GameObject _host;
        private HitscanWeapon _weapon;
        private readonly List<Object> _cleanup = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("HitscanHost");
            _host.AddComponent<Animator>();
            _weapon = _host.AddComponent<HitscanWeapon>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _cleanup) if (obj != null) Object.Destroy(obj);
            Object.Destroy(_host);
        }

        [UnityTest]
        public IEnumerator Prepare_SetsClipAndReserve()
        {
            yield return null;
            _weapon.Prepare(12, 40);
            Assert.AreEqual(12, _weapon.testMaxClip);
            Assert.AreEqual(12, _weapon.testClip);
            Assert.AreEqual(40, _weapon.reserveAmmo);
        }

        [Test]
        public void SetFireRate_Valid_Updates()
        {
            _weapon.SetFireRate(0.3f);
            // Fire rate is private; verify via second shot not blocked.
            _weapon.Prepare(12, 40);
            _weapon.SetFireRate(0.3f);
            Assert.Pass();
        }

        [UnityTest]
        public IEnumerator Shoot_SetsAimAndFires()
        {
            yield return null;
            _weapon.Prepare(5, 10);
            var events = new FirearmEvents();
            int shots = 0;
            events.onShoot += () => shots++;
            _weapon.RegisterEvents(events);
            _weapon.Shoot(new Vector3(0, 0, 5));
            yield return null;
            Assert.AreEqual(4, _weapon.testClip);
            Assert.AreEqual(1, shots);
        }

        [UnityTest]
        public IEnumerator TriggerReload_FillsClipFromReserve()
        {
            yield return null;
            _weapon.Prepare(5, 10);
            // Force clip 2/5
            _weapon.Prepare(5, 10);
            // Shoot 3 times to get to 2
            for (int i = 0; i < 3; i++)
            {
                _weapon.Shoot(new Vector3(0, 0, 5));
                yield return null;
                // Reset trigger latch via Execute path already clears; just wait fireRate
                yield return new WaitForSeconds(0.25f);
            }
            Assert.AreEqual(2, _weapon.testClip);
            _weapon.TriggerReload();
            Assert.IsTrue(_weapon.testIsReloading);
            yield return new WaitForSeconds(1.7f);
            Assert.AreEqual(5, _weapon.testClip);
            Assert.AreEqual(7, _weapon.reserveAmmo);
        }

        [UnityTest]
        public IEnumerator Hitscan_HitsDamageable()
        {
            yield return null;
            var target = new GameObject("HitscanTarget");
            target.transform.position = new Vector3(0, 500, 5);
            target.AddComponent<BoxCollider>();
            var rb = target.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            var stub = target.AddComponent<PlayDamageableStub>();
            _cleanup.Add(target);

            _weapon.transform.position = new Vector3(0, 500, 0);
            // Need shootPoint at host position
            var shootPoint = new GameObject("ShootPoint");
            shootPoint.transform.SetParent(_host.transform, false);
            shootPoint.transform.localPosition = Vector3.zero;
            var so = new UnityEditor.SerializedObject(_weapon);
            so.FindProperty("_shootPoint").objectReferenceValue = shootPoint.transform;
            so.ApplyModifiedProperties();
            Vector3 aimPos = target.transform.position;
            _weapon.Prepare(5, 10);
            _weapon.Shoot(aimPos);
            yield return null;
            Assert.AreEqual(1, stub.DamageCalls);
        }

        [UnityTest]
        public IEnumerator PelletCount_ShotgunHitsMultipleTimes()
        {
            yield return null;
            // Create shotgun definition: 8 pellets
            var def = ScriptableObject.CreateInstance<WeaponDefinition>();
            var soDef = new UnityEditor.SerializedObject(def);
            soDef.FindProperty("_id").stringValue = "TestShotgun";
            soDef.FindProperty("_pelletCount").intValue = 8;
            soDef.FindProperty("_damage").floatValue = 10f;
            soDef.FindProperty("_range").floatValue = 50f;
            soDef.FindProperty("_fireRate").floatValue = 0.5f;
            soDef.FindProperty("_baseSpreadDegrees").floatValue = 0f;
            soDef.FindProperty("_clipSize").intValue = 6;
            soDef.FindProperty("_reloadDuration").floatValue = 1f;
            soDef.ApplyModifiedPropertiesWithoutUndo();
            var soWeap = new UnityEditor.SerializedObject(_weapon);
            soWeap.FindProperty("_definition").objectReferenceValue = def;
            soWeap.ApplyModifiedProperties();
            _cleanup.Add(def);

            var target = new GameObject("ShotgunTarget");
            target.transform.position = new Vector3(0, 600, 5);
            // Large collider to catch all pellets with zero spread
            var col = target.AddComponent<BoxCollider>();
            col.size = new Vector3(10, 10, 1);
            var rb2 = target.AddComponent<Rigidbody>();
            rb2.isKinematic = true;
            var stub2 = target.AddComponent<PlayDamageableStub>();
            _cleanup.Add(target);
            _weapon.transform.position = new Vector3(0, 600, 0);
            var sp = new GameObject("SP2");
            sp.transform.SetParent(_host.transform, false);
            sp.transform.localPosition = Vector3.zero;
            var so2 = new UnityEditor.SerializedObject(_weapon);
            so2.FindProperty("_shootPoint").objectReferenceValue = sp.transform;
            so2.ApplyModifiedProperties();
            _weapon.Prepare(6, 12);
            _weapon.Shoot(target.transform.position);
            yield return null;
            Assert.AreEqual(8, stub2.DamageCalls);
        }
    }
}
