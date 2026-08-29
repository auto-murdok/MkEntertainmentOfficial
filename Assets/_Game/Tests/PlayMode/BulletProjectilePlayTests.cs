using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    internal class PlayDamageableStub : MonoBehaviour, IDamageable
    {
        public int DamageCalls;
        public float HitPoints = 100f;

        public float remainingHitPoints => HitPoints;

        public void TakeDamage(float amount)
        {
            DamageCalls++;
            HitPoints = Mathf.Max(0f, HitPoints - amount);
        }
    }

    public class BulletProjectilePlayTests
    {
        private GameObject _bullet;
        private GameObject _target;
        private PlayDamageableStub _stub;
        private readonly System.Collections.Generic.List<Object> _cleanup = new System.Collections.Generic.List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var projectile in Object.FindObjectsByType<BulletProjectile>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(projectile.gameObject);
            }
            if (_bullet != null) Object.DestroyImmediate(_bullet);
            if (_target != null) Object.DestroyImmediate(_target);
            foreach (var obj in _cleanup)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
        }

        private void BuildBulletAt(Vector3 position, Vector3 forward)
        {
            _bullet = new GameObject("TestBullet");
            _bullet.transform.position = position;
            _bullet.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            var collider = _bullet.AddComponent<SphereCollider>();
            collider.radius = 0.05f;
            _bullet.AddComponent<Rigidbody>();
            _bullet.AddComponent<BulletProjectile>();
        }

        private void BuildTargetAt(Vector3 position)
        {
            _target = new GameObject("TestTarget");
            _target.transform.position = position;
            var box = _target.AddComponent<BoxCollider>();
            box.size = Vector3.one;
            var body = _target.AddComponent<Rigidbody>();
            body.isKinematic = true;
            _stub = _target.AddComponent<PlayDamageableStub>();
        }

        [UnityTest]
        public IEnumerator Launch_MovingBulletReachesForward()
        {
            BuildBulletAt(new Vector3(0f, 500f, 0f), Vector3.forward);
            var body = _bullet.GetComponent<Rigidbody>();
            _bullet.GetComponent<BulletProjectile>().Launch(new Vector3(0f, 500f, 0f), Quaternion.LookRotation(Vector3.forward), null);
            yield return new WaitForFixedUpdate();
            Assert.Greater(body.position.z, 0f);
            Assert.AreEqual(50f, body.linearVelocity.magnitude, 1f);
        }

        [UnityTest]
        public IEnumerator Hit_ScoresExactlyOneDamageAndReleasesBullet()
        {
            BuildTargetAt(new Vector3(0f, 600f, 3f));
            BuildBulletAt(new Vector3(0f, 600f, 0f), Vector3.forward);
            _bullet.GetComponent<BulletProjectile>().Launch(new Vector3(0f, 600f, 0f), Quaternion.LookRotation(Vector3.forward), null);

            int guard = 0;
            while (_stub.DamageCalls == 0 && guard++ < 600) yield return null;

            Assert.AreEqual(1, _stub.DamageCalls);
            Assert.AreEqual(75f, _stub.HitPoints, 0.01f);

            guard = 0;
            while (_bullet != null && guard++ < 30) yield return null;
            Assert.IsTrue(_bullet == null, "Bullet without a pool should self-destruct after scoring a hit.");
        }

        [Test]
        public void Hit_OwnerCollidersAreIgnored()
        {
            var owner = new GameObject("Owner");
            owner.transform.position = new Vector3(0f, 700f, 0f);
            var ownerCollider = owner.AddComponent<BoxCollider>();
            BuildBulletAt(new Vector3(0f, 700f, 0f), Vector3.forward);
            _bullet.GetComponent<BulletProjectile>().Launch(new Vector3(0f, 700f, 0f), Quaternion.LookRotation(Vector3.forward), owner);

            Assert.IsTrue(Physics.GetIgnoreCollision(ownerCollider, _bullet.GetComponent<Collider>()));
            Object.DestroyImmediate(owner);
        }

        [UnityTest]
        public IEnumerator MaxTravelDistance_BulletIsReleasedIntoEmptySpace()
        {
            BuildBulletAt(new Vector3(0f, 800f, 0f), Vector3.forward);
            _bullet.GetComponent<BulletProjectile>().Launch(new Vector3(0f, 800f, 0f), Quaternion.LookRotation(Vector3.forward), null);

            int guard = 0;
            while (_bullet != null && guard++ < 600) yield return null;
            Assert.IsTrue(_bullet == null, "Bullet should be released after travelling 30m.");
        }

        [UnityTest]
        public IEnumerator PooledBullet_SecondFlight_ScoresDamageAgain()
        {
            ObjectPool<BulletProjectile> pool = null;
            pool = new ObjectPool<BulletProjectile>(
                createFunc: () =>
                {
                    var go = new GameObject("PooledBullet");
                    go.AddComponent<SphereCollider>().radius = 0.05f;
                    go.AddComponent<Rigidbody>();
                    var projectile = go.AddComponent<BulletProjectile>();
                    projectile.objectPool = pool;
                    go.SetActive(false);
                    _cleanup.Add(go);
                    return projectile;
                },
                actionOnGet: null,
                actionOnRelease: projectile => projectile.gameObject.SetActive(false),
                collectionCheck: true);

            var firstTargetGo = new GameObject("Target1");
            firstTargetGo.transform.position = new Vector3(0f, 900f, 3f);
            firstTargetGo.AddComponent<BoxCollider>();
            firstTargetGo.AddComponent<Rigidbody>().isKinematic = true;
            var firstStub = firstTargetGo.AddComponent<PlayDamageableStub>();
            _cleanup.Add(firstTargetGo);

            var bullet = pool.Get();
            bullet.Launch(new Vector3(0f, 900f, 0f), Quaternion.LookRotation(Vector3.forward), null);
            int guard = 0;
            while (firstStub.DamageCalls == 0 && guard++ < 600) yield return null;
            Assert.AreEqual(1, firstStub.DamageCalls);
            yield return null;
            Assert.IsFalse(bullet.gameObject.activeSelf, "Bullet should be back in the pool after scoring.");

            var secondTargetGo = new GameObject("Target2");
            secondTargetGo.transform.position = new Vector3(0f, 900f, 6f);
            secondTargetGo.AddComponent<BoxCollider>();
            secondTargetGo.AddComponent<Rigidbody>().isKinematic = true;
            var secondStub = secondTargetGo.AddComponent<PlayDamageableStub>();
            _cleanup.Add(secondTargetGo);

            var relaunched = pool.Get();
            Assert.AreEqual(bullet, relaunched, "Pool should recycle the same projectile instance.");
            relaunched.Launch(new Vector3(0f, 900f, 4f), Quaternion.LookRotation(Vector3.forward), null);
            guard = 0;
            while (secondStub.DamageCalls == 0 && guard++ < 600) yield return null;
            Assert.AreEqual(1, secondStub.DamageCalls);

            pool.Clear();
        }
    }
}
