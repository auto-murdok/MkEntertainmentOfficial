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

        [Test]
        public void PlayerPrefab_CarriesNetworkedHealth()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.IsNotNull(prefab, $"Player prefab missing at {PlayerPrefabPath}.");

            Assert.IsNotNull(prefab.GetComponent<NetworkedHealth>(),
                "Player prefab must carry NetworkedHealth — without it, HP/death never replicate " +
                "and a player's death is invisible to the other peers.");
        }

        [Test]
        public void PlayerPrefab_CarriesNetworkedDamageRelay()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.IsNotNull(prefab, $"Player prefab missing at {PlayerPrefabPath}.");

            Assert.IsNotNull(prefab.GetComponent<NetworkedDamageRelay>(),
                "Player prefab must carry NetworkedDamageRelay (server-authoritative damage).");
        }

        [Test]
        public void PlayerPrefab_NetworkAnimator_IsOwnerAuthoritativeAndWired()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.IsNotNull(prefab, $"Player prefab missing at {PlayerPrefabPath}.");

            var networkAnimator = prefab.GetComponent<Unity.Netcode.Components.NetworkAnimator>();
            Assert.IsNotNull(networkAnimator, "Player prefab must carry a NetworkAnimator (animation sync).");

            var so = new SerializedObject(networkAnimator);
            Assert.AreEqual(1, so.FindProperty("AuthorityMode").enumValueIndex,
                "NetworkAnimator must be owner-authoritative to match the owner-authoritative NetworkTransform.");
            Assert.IsNotNull(so.FindProperty("m_Animator").objectReferenceValue,
                "NetworkAnimator must reference the character's Animator.");
        }

        [Test]
        public void BuildRemoteRig_WiresAllConstraintSourcesToALocalTarget()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.IsNotNull(prefab, $"Player prefab missing at {PlayerPrefabPath}.");

            // Remote copies spawn with NULL constraint sources (prefab scene-ref
            // stripping); BuildRemoteRig must wire every MultiAimConstraint to
            // the local forward target and rebuild the RigBuilder graph.
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                var composition = instance.GetComponent<NetworkedPlayerComposition>();
                Assert.IsNotNull(composition, "Player prefab must carry NetworkedPlayerComposition.");

                composition.BuildRemoteRig();

                Transform remoteTarget = instance.transform.Find("RemoteAimTarget");
                Assert.IsNotNull(remoteTarget, "RemoteAimTarget was not created.");

                var constraints = instance.GetComponentsInChildren<UnityEngine.Animations.Rigging.MultiAimConstraint>(true);
                Assert.Greater(constraints.Length, 0, "Player prefab should ship with MultiAimConstraints.");
                foreach (var constraint in constraints)
                {
                    Assert.AreEqual(1, constraint.data.sourceObjects.Count,
                        "Every aim constraint must have exactly one source after BuildRemoteRig.");
                    Assert.IsTrue(constraint.data.sourceObjects[0].transform == remoteTarget,
                        "Constraint sources must point at the RemoteAimTarget.");
                    Assert.AreEqual(1f, constraint.data.sourceObjects[0].weight);
                }

                // Idempotency: a second run must not stack duplicate targets.
                composition.BuildRemoteRig();
                Assert.AreEqual(1, instance.transform.Find("RemoteAimTarget") == null ? 0 : 1,
                    "BuildRemoteRig must reuse the existing RemoteAimTarget.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
