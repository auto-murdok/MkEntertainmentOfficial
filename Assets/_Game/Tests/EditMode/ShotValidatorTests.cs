using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    // The server-side validation gauntlet (ShotValidator) is pure logic, so
    // the whole authority contract is pinned here in EditMode: fire-rate
    // enforcement against the network clock, the server ammo economy, and
    // the origin sanity check. The reload numbers deliberately mirror the
    // client FSM scenarios pinned by HandgunPlayTests (same refill math,
    // authority side).
    public class ShotValidatorTests
    {
        private static readonly WeaponStats Stats = WeaponStats.Handgun;
        private static readonly Vector3 Muzzle = new Vector3(0f, 1.4f, 0f);

        private static ServerWeaponState FreshState()
        {
            var state = new ServerWeaponState();
            state.Init(Stats);
            return state;
        }

        [Test]
        public void Init_SetsClipAndReserveFromStats()
        {
            ServerWeaponState state = FreshState();

            Assert.AreEqual(5, state.Clip);
            Assert.AreEqual(45, state.Reserve);
            Assert.IsFalse(state.Reloading);
        }

        [Test]
        public void Accept_ValidShot_DecrementsClip()
        {
            ServerWeaponState state = FreshState();

            ShotRejection verdict = ShotValidator.TryAccept(state, Stats, 10d, Muzzle, Muzzle);

            Assert.AreEqual(ShotRejection.None, verdict);
            Assert.AreEqual(4, state.Clip);
        }

        [Test]
        public void Accept_SecondShotInsideInterval_RejectedForFireRate()
        {
            ServerWeaponState state = FreshState();
            ShotValidator.TryAccept(state, Stats, 10d, Muzzle, Muzzle);

            ShotRejection verdict = ShotValidator.TryAccept(state, Stats, 10.1d, Muzzle, Muzzle);

            Assert.AreEqual(ShotRejection.FireRate, verdict);
            Assert.AreEqual(4, state.Clip, "A rejected shot must not consume ammo");
        }

        [Test]
        public void Accept_ShotAfterInterval_Passes()
        {
            ServerWeaponState state = FreshState();
            ShotValidator.TryAccept(state, Stats, 10d, Muzzle, Muzzle);

            ShotRejection verdict = ShotValidator.TryAccept(state, Stats, 10.2d, Muzzle, Muzzle);

            Assert.AreEqual(ShotRejection.None, verdict);
        }

        [Test]
        public void Accept_EmptyClip_Rejected()
        {
            ServerWeaponState state = FreshState();
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(ShotRejection.None,
                    ShotValidator.TryAccept(state, Stats, 10d + i * 0.2d, Muzzle, Muzzle));
            }

            ShotRejection verdict = ShotValidator.TryAccept(state, Stats, 11d, Muzzle, Muzzle);

            Assert.AreEqual(ShotRejection.EmptyClip, verdict);
        }

        [Test]
        public void Accept_OriginFarFromServerMuzzle_Rejected()
        {
            ServerWeaponState state = FreshState();
            Vector3 claimed = Muzzle + new Vector3(10f, 0f, 0f);

            ShotRejection verdict = ShotValidator.TryAccept(state, Stats, 10d, claimed, Muzzle);

            Assert.AreEqual(ShotRejection.OriginTooFar, verdict);
            Assert.AreEqual(5, state.Clip);
        }

        [Test]
        public void Accept_OriginWithinTolerance_Passes()
        {
            ServerWeaponState state = FreshState();
            Vector3 claimed = Muzzle + new Vector3(1f, 0.2f, 0f);

            ShotRejection verdict = ShotValidator.TryAccept(state, Stats, 10d, claimed, Muzzle);

            Assert.AreEqual(ShotRejection.None, verdict);
        }

        [Test]
        public void Reload_StartAndCommit_RefillsFromReserve()
        {
            ServerWeaponState state = FreshState();
            state.Clip = 2;

            Assert.IsTrue(ShotValidator.TryStartReload(state, Stats, 20d));
            Assert.IsFalse(ShotValidator.CommitReloadIfDue(state, 21.9d), "No early commit");
            Assert.IsTrue(ShotValidator.CommitReloadIfDue(state, 22d), "Commit at duration end");

            Assert.AreEqual(5, state.Clip);
            Assert.AreEqual(42, state.Reserve);
            Assert.IsFalse(state.Reloading);
        }

        [Test]
        public void Reload_RefillMath_MatchesClientFsm()
        {
            // HandgunPlayTests pins the client side of this exact scenario:
            // clip 2/5 + reserve 3 -> clip 5, reserve 0.
            ServerWeaponState state = FreshState();
            state.Clip = 2;
            state.Reserve = 3;

            ShotValidator.TryStartReload(state, Stats, 0d);
            ShotValidator.CommitReloadIfDue(state, Stats.ReloadDuration);

            Assert.AreEqual(5, state.Clip);
            Assert.AreEqual(0, state.Reserve);
        }

        [Test]
        public void Reload_FullClipOrNoReserve_NeverStarts()
        {
            ServerWeaponState full = FreshState();
            Assert.IsFalse(ShotValidator.TryStartReload(full, Stats, 0d));

            ServerWeaponState dry = FreshState();
            dry.Clip = 2;
            dry.Reserve = 0;
            Assert.IsFalse(ShotValidator.TryStartReload(dry, Stats, 0d));
        }

        [Test]
        public void Accept_DuringReload_Rejected_ButCommitsWhenDue()
        {
            ServerWeaponState state = FreshState();
            state.Clip = 4;
            ShotValidator.TryStartReload(state, Stats, 30d);

            Assert.AreEqual(ShotRejection.Reloading,
                ShotValidator.TryAccept(state, Stats, 31d, Muzzle, Muzzle));

            // At the commit moment the same call first applies the refill,
            // then judges the shot against the refilled state (5 -> 4).
            Assert.AreEqual(ShotRejection.None,
                ShotValidator.TryAccept(state, Stats, 32d, Muzzle, Muzzle));
            Assert.AreEqual(4, state.Clip);
            Assert.AreEqual(44, state.Reserve);
        }
    }
}
