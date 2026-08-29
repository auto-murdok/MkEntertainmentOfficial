using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    // The player prefab's NetworkTransform must be owner-authoritative:
    // with the server-authoritative default the client's own movement is
    // overwritten every tick by the server's stale pose ("client can't
    // move"). See docs/networking_notes.md, lesson 8.
    public class NetworkedPlayerPrefabTests
    {
        private const string PlayerPrefabPath = "Assets/_Game/Prefabs/Characters/Survivor/FemaleCharacter.prefab";

        [Test]
        public void PlayerPrefab_NetworkTransform_IsOwnerAuthoritative()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.IsNotNull(prefab, $"Player prefab missing at {PlayerPrefabPath}.");

            var networkTransform = prefab.GetComponent<Unity.Netcode.Components.NetworkTransform>();
            Assert.IsNotNull(networkTransform, "Player prefab must carry a NetworkTransform.");

            Assert.IsFalse(networkTransform.IsServerAuthoritative(),
                "The player's NetworkTransform must be owner-authoritative (AuthorityMode=Owner) — " +
                "server authority makes client movement snap back to the server's stale pose.");
        }

        [Test]
        public void PlayerPrefab_HasNetworkObjectAndComposition()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.IsNotNull(prefab, $"Player prefab missing at {PlayerPrefabPath}.");

            Assert.IsNotNull(prefab.GetComponent<Unity.Netcode.NetworkObject>(),
                "Player prefab must carry a NetworkObject (NGO auto player spawn).");
            Assert.IsNotNull(prefab.GetComponent<NetworkedPlayerComposition>(),
                "Player prefab must carry NetworkedPlayerComposition (owner-side rig composition on spawn).");
        }
    }
}
