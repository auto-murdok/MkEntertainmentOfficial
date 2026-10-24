using NUnit.Framework;

namespace Game.Tests.EditMode
{
    // WeaponStats.Handgun is the single source the server validates
    // against; it must equal the handgun tuning the game shipped with
    // (previously scattered across Weapon fields, the projectile prefab and
    // code constants). If a tuning change is ever intended, it lands in
    // WeaponStats.Handgun and this test is updated in the same commit —
    // never silently.
    public class WeaponStatsTests
    {
        [Test]
        public void HandgunPreset_MatchesShippedTuning()
        {
            WeaponStats stats = WeaponStats.Handgun;

            Assert.AreEqual(25f, stats.Damage, 0.001f, "Was BulletProjectile._damage");
            Assert.AreEqual(0.2f, stats.FireInterval, 0.001f, "Was Weapon._fireRate");
            Assert.AreEqual(5, stats.ClipSize, "Was Weapon._clipSize");
            Assert.AreEqual(45, stats.ReserveAmmo, "Was Weapon._reserveAmmo");
            Assert.AreEqual(2f, stats.ReloadDuration, 0.001f, "Was HandgunReloadingState.ReloadDuration");
            Assert.AreEqual(30f, stats.Range, 0.001f, "Was BulletProjectile._maxTravelDistance");
        }
    }
}
