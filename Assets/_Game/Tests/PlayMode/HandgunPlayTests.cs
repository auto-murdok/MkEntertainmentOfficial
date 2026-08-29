using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public class HandgunPlayTests
    {
        private GameObject _host;
        private Handgun _handgun;
        private GameObject _bulletSource;
        private readonly List<Object> _cleanup = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("HandgunHost");
            _host.AddComponent<Animator>();
            _handgun = _host.AddComponent<Handgun>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var projectile in Object.FindObjectsByType<BulletProjectile>(FindObjectsSortMode.None))
            {
                if (projectile.gameObject != _bulletSource) Object.Destroy(projectile.gameObject);
            }
            foreach (var obj in _cleanup)
            {
                if (obj != null) Object.Destroy(obj);
            }
            if (_bulletSource != null) Object.Destroy(_bulletSource);
            Object.Destroy(_host);
        }

        private HandgunContext Context => _handgun._context;

        private GameObject CreateBulletSource()
        {
            _bulletSource = new GameObject("BulletSource");
            _bulletSource.AddComponent<Rigidbody>();
            _bulletSource.AddComponent<SphereCollider>();
            _bulletSource.AddComponent<BulletProjectile>();
            var so = new UnityEditor.SerializedObject(_handgun);
            so.FindProperty("_bulletPrefab").objectReferenceValue = _bulletSource;
            so.ApplyModifiedProperties();
            return _bulletSource;
        }

        [UnityTest]
        public IEnumerator Awake_RegistersThreeStatesAndBindsAnimator()
        {
            yield return null;
            Assert.AreEqual(3, _handgun.states.Count);
            Assert.IsTrue(_handgun.states.ContainsKey(HandgunState.Ready));
            Assert.IsTrue(_handgun.states.ContainsKey(HandgunState.Shooting));
            Assert.IsTrue(_handgun.states.ContainsKey(HandgunState.Reloading));
            Assert.AreEqual(_host.GetComponent<Animator>(), Context.animator);
            Assert.AreEqual(0.05f, Context.fireRate);
            Assert.AreEqual(5, Context.gunKick);
        }

        [Test]
        public void Prepare_SetsClipAndReserve()
        {
            _handgun.Prepare(12, 40);
            Assert.AreEqual(12, Context.maxClipSize);
            Assert.AreEqual(12, Context.clipSize);
            Assert.AreEqual(40, Context.reserveAmmo);
        }

        [Test]
        public void SetFireRate_Valid_UpdatesContext()
        {
            _handgun.SetFireRate(0.3f);
            Assert.AreEqual(0.3f, Context.fireRate);
        }

        [Test]
        public void SetFireRate_NonPositive_FallsBackToDefault()
        {
            _handgun.SetFireRate(0f);
            Assert.AreEqual(0.05f, Context.fireRate);
            _handgun.SetFireRate(-1f);
            Assert.AreEqual(0.05f, Context.fireRate);
        }

        [Test]
        public void Shoot_SetsAimDirectionTowardTargetAndPressesTrigger()
        {
            _handgun.transform.position = Vector3.zero;
            _handgun.Shoot(new Vector3(0f, 0f, 10f));
            Assert.IsTrue(Context.isTriggerPressed);
            Assert.AreEqual(Vector3.forward, Context.aimDirection);
        }

        [Test]
        public void Shoot_SamePositionAsGun_UsesMuzzleForwardFallback()
        {
            _handgun.Shoot(_handgun.transform.position);
            Assert.IsTrue(Context.isTriggerPressed);
            Assert.AreEqual(Vector3.forward, Context.aimDirection);
        }

        [Test]
        public void Shoot_IgnoresSecondPressWhileTriggerHeld()
        {
            _handgun.Shoot(new Vector3(0, 0, 5));
            Context.aimDirection = Vector3.left;
            _handgun.Shoot(new Vector3(9, 9, 9));
            Assert.AreEqual(Vector3.left, Context.aimDirection);
        }

        [Test]
        public void Shoot_IgnoresWhileReloading()
        {
            Context.isReloading = true;
            _handgun.Shoot(new Vector3(0, 0, 5));
            Assert.IsFalse(Context.isTriggerPressed);
        }

        [Test]
        public void TriggerReload_ValidCondition_SetsReloadingFlag()
        {
            _handgun.Prepare(5, 10);
            Context.clipSize = 2;
            _handgun.TriggerReload();
            Assert.IsTrue(Context.isReloading);
        }

        [Test]
        public void TriggerReload_ClipFull_DoesNothing()
        {
            _handgun.Prepare(5, 10);
            _handgun.TriggerReload();
            Assert.IsFalse(Context.isReloading);
        }

        [Test]
        public void TriggerReload_NoReserve_DoesNothing()
        {
            _handgun.Prepare(5, 0);
            Context.clipSize = 0;
            _handgun.TriggerReload();
            Assert.IsFalse(Context.isReloading);
        }

        [Test]
        public void ExecuteActualShoot_NoBulletPrefab_ReturnsFalse()
        {
            Assert.IsFalse(_handgun.ExecuteActualShoot());
        }

        [UnityTest]
        public IEnumerator ExecuteActualShoot_WithBulletPrefab_LaunchesPooledBullet()
        {
            CreateBulletSource();
            _handgun.transform.position = new Vector3(1f, 2f, 3f);
            bool fired = _handgun.ExecuteActualShoot();
            Assert.IsTrue(fired);
            yield return null;

            BulletProjectile bullet = null;
            foreach (var candidate in Object.FindObjectsByType<BulletProjectile>(FindObjectsSortMode.None))
            {
                if (candidate.gameObject != _bulletSource) bullet = candidate;
            }
            Assert.IsNotNull(bullet, "A pooled bullet should have been instantiated and launched.");
            Assert.IsTrue(bullet.gameObject.activeSelf);
            Assert.AreEqual(50f, bullet.GetComponent<Rigidbody>().linearVelocity.magnitude, 0.1f);
        }

        [UnityTest]
        public IEnumerator LiveBullets_TracksPooledBulletsWithoutSceneScan()
        {
            yield return null; // Awake runs: pool exists
            CreateBulletSource();
            Assert.AreEqual(0, _handgun.liveBullets);

            Assert.IsTrue(_handgun.ExecuteActualShoot());
            yield return null;
            Assert.AreEqual(1, _handgun.liveBullets);

            // Release the bullet back and confirm the count follows the pool.
            BulletProjectile live = null;
            foreach (var candidate in Object.FindObjectsByType<BulletProjectile>(FindObjectsSortMode.None))
            {
                if (candidate.gameObject != _bulletSource) live = candidate;
            }
            Assert.IsNotNull(live);
            live.ReleaseToPool();
            Assert.AreEqual(0, _handgun.liveBullets);
        }

        [Test]
        public void RegisterEvents_StoresFirearmEvents()
        {
            var events = new FirearmEvents();
            _handgun.RegisterEvents(events);
            Assert.AreEqual(events, _handgun.fireArmEvents);
        }

        [Test]
        public void InjectUIController_ForwardsToContext()
        {
            var uiHost = new GameObject("UIHost");
            _cleanup.Add(uiHost);
            var ui = uiHost.AddComponent<CharacterUIController>();
            _handgun.InjectUIController(ui);
            Assert.AreEqual(ui, Context.UIController);
        }

        [UnityTest]
        public IEnumerator Shoot_DryFire_TransitionsToShootingWithoutSpendOrShootEvent()
        {
            // No bullet prefab -> ExecuteActualShoot fails: the FSM still
            // runs the shooting state, but no round is spent and no onShoot
            // event fires.
            yield return null;
            _handgun.Prepare(5, 10);
            int shootEvents = 0;
            var events = new FirearmEvents();
            events.onShoot += () => shootEvents++;
            _handgun.RegisterEvents(events);

            _handgun.Shoot(new Vector3(0, 0, 5));
            yield return null;

            Assert.AreEqual(HandgunState.Shooting, _handgun.CurrentStateName);
            Assert.AreEqual(5, Context.clipSize);
            Assert.AreEqual(0, shootEvents);
        }

        [UnityTest]
        public IEnumerator Shoot_WithBulletPrefab_FiresOnShootEvent()
        {
            yield return null;
            CreateBulletSource();
            _handgun.Prepare(5, 10);
            int shootEvents = 0;
            var events = new FirearmEvents();
            events.onShoot += () => shootEvents++;
            _handgun.RegisterEvents(events);

            _handgun.Shoot(new Vector3(0, 0, 5));
            yield return null;

            Assert.AreEqual(1, shootEvents);
            Assert.AreEqual(4, Context.clipSize);
        }

        [UnityTest]
        public IEnumerator Shoot_WithEmptyClip_TransitionsToReloading()
        {
            yield return null;
            _handgun.Prepare(5, 10);
            Context.clipSize = 0;
            _handgun.Shoot(new Vector3(0, 0, 5));
            yield return null;
            Assert.AreEqual(HandgunState.Reloading, _handgun.CurrentStateName);
            Assert.IsTrue(Context.isReloading);
        }

        [Test]
        public void ReloadState_ExitRefillsClipFromReserve()
        {
            _handgun.Prepare(5, 3);
            Context.clipSize = 2;
            int started = 0, finished = 0;
            var events = new FirearmEvents();
            events.onReloadStarted += () => started++;
            events.onReloadFinished += () => finished++;
            _handgun.RegisterEvents(events);

            var reloadState = _handgun.states[HandgunState.Reloading];
            reloadState.EnterState(_handgun);
            Assert.AreEqual(1, started);
            Assert.IsTrue(Context.isReloading);
            Assert.IsFalse(Context.isTriggerPressed);

            reloadState.ExitState(_handgun);
            Assert.AreEqual(5, Context.clipSize);
            Assert.AreEqual(0, Context.reserveAmmo);
            Assert.IsFalse(Context.isReloading);
            Assert.AreEqual(1, finished);
        }

        [Test]
        public void ReloadState_ExitWithInfiniteReserve_FillsClipCompletely()
        {
            _handgun.Prepare(5, int.MaxValue);
            Context.clipSize = 0;
            var reloadState = _handgun.states[HandgunState.Reloading];
            reloadState.EnterState(_handgun);
            reloadState.ExitState(_handgun);
            Assert.AreEqual(5, Context.clipSize);
            Assert.AreEqual(int.MaxValue - 5, Context.reserveAmmo);
        }

        [Test]
        public void ReloadState_ExitNotifiesShootUI()
        {
            var uiHost = new GameObject("UIHost");
            _cleanup.Add(uiHost);
            var ui = uiHost.AddComponent<CharacterUIController>();
            _handgun.InjectUIController(ui);
            var observer = new RecordingUIObserver();
            ui.AddObserver(observer);

            _handgun.Prepare(5, 10);
            Context.clipSize = 3;
            var reloadState = _handgun.states[HandgunState.Reloading];
            reloadState.EnterState(_handgun);
            reloadState.ExitState(_handgun);

            Assert.AreEqual(1, observer.Elements.Count);
            Assert.AreEqual(CharacterUIElement.ShootUI, observer.Elements[0]);
            Assert.AreEqual(5, observer.Contexts[0].clipSize);
            Assert.AreEqual(5, observer.Contexts[0].maxClipSize);
        }

        [UnityTest]
        public IEnumerator ShootingState_WhileReloading_TransitionsToReloading()
        {
            yield return null;
            _handgun.Prepare(5, 10);
            _handgun.SetFireRate(0.0001f);
            _handgun.Shoot(new Vector3(0, 0, 5));
            yield return null;
            Assert.AreEqual(HandgunState.Shooting, _handgun.CurrentStateName);

            Context.isReloading = true;
            yield return new WaitForSeconds(0.1f);
            Assert.AreEqual(HandgunState.Reloading, _handgun.CurrentStateName);
            Assert.IsFalse(Context.isTriggerPressed);
        }

        [UnityTest]
        public IEnumerator ShootingState_Rechambered_TransitionsBackToReady()
        {
            yield return null;
            _handgun.Prepare(5, 10);
            _handgun.SetFireRate(0.0001f);
            _handgun.Shoot(new Vector3(0, 0, 5));
            yield return null;
            yield return new WaitForSeconds(0.1f);
            Assert.AreEqual(HandgunState.Ready, _handgun.CurrentStateName);
            Assert.IsFalse(Context.isTriggerPressed);
        }

        [UnityTest]
        public IEnumerator Weapon_Awake_PreparesFirearmWithSerializedConfig()
        {
            _host.AddComponent<Weapon>();
            yield return null;
            Assert.AreEqual(5, Context.clipSize);
            Assert.AreEqual(5, Context.maxClipSize);
            Assert.AreEqual(45, Context.reserveAmmo);
            Assert.AreEqual(0.2f, Context.fireRate);
        }

        [Test]
        public void Weapon_TriggerShoot_ForwardsAimPosition()
        {
            _host.AddComponent<Weapon>();
            _handgun.Prepare(5, 10);
            _host.GetComponent<Weapon>().TriggerShoot(new Vector3(0, 0, 7));
            Assert.IsTrue(Context.isTriggerPressed);
        }

        [Test]
        public void Weapon_TriggerReload_ForwardsToFirearm()
        {
            _host.AddComponent<Weapon>();
            Context.clipSize = 0;
            _host.GetComponent<Weapon>().TriggerReload();
            Assert.IsTrue(Context.isReloading);
        }

        [Test]
        public void Weapon_RegisterEvents_ForwardsToHandgun()
        {
            _host.AddComponent<Weapon>();
            var events = new FirearmEvents();
            _host.GetComponent<Weapon>().RegisterEvents(events);
            Assert.AreEqual(events, _handgun.fireArmEvents);
        }

        [Test]
        public void Weapon_InjectUIController_ForwardsToHandgunContext()
        {
            _host.AddComponent<Weapon>();
            var uiHost = new GameObject("UIHost");
            _cleanup.Add(uiHost);
            var ui = uiHost.AddComponent<CharacterUIController>();
            _host.GetComponent<Weapon>().InjectUIController(ui);
            Assert.AreEqual(ui, Context.UIController);
        }

        [Test]
        public void AddReserveAmmo_PositiveAmount_IncreasesReserve()
        {
            _handgun.Prepare(5, 10);
            _handgun.AddReserveAmmo(15);
            Assert.AreEqual(25, _handgun.reserveAmmo);
        }

        [Test]
        public void AddReserveAmmo_NonPositive_IsNoOp()
        {
            _handgun.Prepare(5, 10);
            _handgun.AddReserveAmmo(0);
            _handgun.AddReserveAmmo(-5);
            Assert.AreEqual(10, _handgun.reserveAmmo);
        }

        [Test]
        public void Weapon_AddReserveAmmo_ForwardsToHandgun()
        {
            _host.AddComponent<Weapon>();
            _handgun.Prepare(5, 10);
            _host.GetComponent<Weapon>().AddReserveAmmo(20);
            Assert.AreEqual(30, _handgun.reserveAmmo);
        }

        [UnityTest]
        public IEnumerator ReadyState_EmptyClipWithNoReserve_StaysReady()
        {
            // Out of ammo entirely: the empty-clip press must NOT start a
            // pointless reload cycle — the weapon stays Ready (dry).
            _handgun.Prepare(0, 0);
            _handgun.SetFireRate(0.05f);
            _handgun.Shoot(new Vector3(0, 0, 5));
            yield return null;
            yield return null;
            Assert.AreEqual(HandgunState.Ready, _handgun.CurrentStateName);
            Assert.AreEqual(0, Context.clipSize);
            Assert.AreEqual(0, Context.reserveAmmo);
        }

        [UnityTest]
        public IEnumerator ShootingState_DryFire_DoesNotConsumeClip()
        {
            // Missing bullet prefab -> ExecuteActualShoot returns false; the
            // clip must not be decremented for a round that was never fired.
            _handgun.Prepare(5, 10);
            _handgun.SetFireRate(0.05f);
            _handgun.Shoot(new Vector3(0, 0, 5));
            yield return new WaitForSeconds(0.1f);
            Assert.AreEqual(HandgunState.Ready, _handgun.CurrentStateName);
            Assert.AreEqual(5, Context.clipSize);
        }
    }
}
