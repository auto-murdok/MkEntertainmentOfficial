using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    // The zombie prefab's networking contract (milestone: server-simulated AI).
    // Zombies are simulated on the host only; clients receive pose + animation
    // through server-authoritative replication. See docs/networking_notes.md.
    public class NetworkedZombiePrefabTests
    {
        private const string ZombiePrefabPath = "Assets/_Game/Prefabs/Characters/Zombie/Zombie.prefab";
        private const string NetworkPrefabsListPath = "Assets/_Game/Data/Network/NetworkPrefabs_Arena.asset";

        [Test]
        public void ZombiePrefab_CarriesServerAuthoritativeNetworkingComponents()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ZombiePrefabPath);
            Assert.IsNotNull(prefab, $"Zombie prefab missing at {ZombiePrefabPath}.");

            Assert.IsNotNull(prefab.GetComponent<Unity.Netcode.NetworkObject>(),
                "Zombie prefab must carry a NetworkObject (host-side spawn).");

            var networkTransform = prefab.GetComponent<Unity.Netcode.Components.NetworkTransform>();
            Assert.IsNotNull(networkTransform, "Zombie prefab must carry a NetworkTransform.");
            Assert.IsTrue(networkTransform.IsServerAuthoritative(),
                "Zombie NetworkTransform must stay SERVER-authoritative — the host simulates AI; only players use owner authority.");

            var networkAnimator = prefab.GetComponent<Unity.Netcode.Components.NetworkAnimator>();
            Assert.IsNotNull(networkAnimator, "Zombie prefab must carry a NetworkAnimator.");
            Assert.IsTrue(networkAnimator.IsServerAuthoritative(),
                "Zombie NetworkAnimator must stay SERVER-authoritative.");
            Assert.IsNotNull(new SerializedObject(networkAnimator).FindProperty("m_Animator").objectReferenceValue,
                "NetworkAnimator must reference the zombie's Animator.");

            Assert.IsNotNull(prefab.GetComponent<NetworkedHealth>(),
                "Zombie prefab must carry NetworkedHealth (damage replication).");
            Assert.IsNotNull(prefab.GetComponent<NetworkedZombieController>(),
                "Zombie prefab must carry NetworkedZombieController (client simulation gate + death despawn).");
        }

        [Test]
        public void ZombiePrefab_IsRegisteredInNetworkPrefabsList()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ZombiePrefabPath);
            Assert.IsNotNull(prefab, $"Zombie prefab missing at {ZombiePrefabPath}.");

            var list = AssetDatabase.LoadAssetAtPath<Unity.Netcode.NetworkPrefabsList>(NetworkPrefabsListPath);
            Assert.IsNotNull(list, $"NetworkPrefabs list missing at {NetworkPrefabsListPath}.");

            bool registered = false;
            foreach (var entry in list.PrefabList)
            {
                if (entry.Prefab == prefab)
                {
                    registered = true;
                    break;
                }
            }
            Assert.IsTrue(registered, "Zombie.prefab must be registered in the NetworkPrefabs list — " +
                                      "unregistered prefabs break client-side spawning.");
        }
    }
}
