using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public class PlayerDataTests
    {
        [Test]
        public void PlayerData_HealthRegenDefaults_ArePositive()
        {
            PlayerData data = ScriptableObject.CreateInstance<PlayerData>();
            Assert.Greater(data.healthRegenDelay, 0f, "Regen delay must be positive so a hit always restarts the timer.");
            Assert.Greater(data.healthRegenRate, 0f, "Regen rate must be positive or regeneration is disabled.");
            Object.DestroyImmediate(data);
        }
    }
}
