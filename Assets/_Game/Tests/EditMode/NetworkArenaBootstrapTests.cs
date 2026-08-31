using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    // The bootstrap must approve connections with the player object created at
    // the component's own transform. NGO's default spawns the player prefab at
    // the prefab's stored pose (scene origin), which ignores the arena's spawn
    // point (ambulance entrance) entirely.
    public class NetworkArenaBootstrapTests
    {
        private GameObject _host;
        private NetworkArenaBootstrap _bootstrap;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("BootstrapHost");
            _host.transform.position = new Vector3(-22f, 0f, -25f);
            _bootstrap = _host.AddComponent<NetworkArenaBootstrap>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
        }

        [Test]
        public void ApproveConnection_CreatesPlayerObjectAtBootstrapTransform()
        {
            var request = new Unity.Netcode.NetworkManager.ConnectionApprovalRequest
            {
                Payload = new byte[] { 0x01 },
                ClientNetworkId = 7UL,
            };
            var response = new Unity.Netcode.NetworkManager.ConnectionApprovalResponse();

            _bootstrap.ApproveConnection(request, response);

            Assert.IsTrue(response.Approved, "The connection must be approved.");
            Assert.IsTrue(response.CreatePlayerObject, "Approval must still auto-create the player object.");
            Assert.AreEqual(new Vector3(-22f, 0f, -25f), response.Position,
                "The player object must be created at the bootstrap's transform (scene spawn point).");
            Assert.AreEqual(_host.transform.rotation, response.Rotation,
                "The player object must be created with the bootstrap's rotation.");
        }

        [Test]
        public void ApproveConnection_TracksTheComponent_NotTheOrigin()
        {
            _host.transform.position = new Vector3(3f, 0f, 4f);
            var request = new Unity.Netcode.NetworkManager.ConnectionApprovalRequest { ClientNetworkId = 1UL };
            var response = new Unity.Netcode.NetworkManager.ConnectionApprovalResponse();

            _bootstrap.ApproveConnection(request, response);

            Assert.AreEqual(new Vector3(3f, 0f, 4f), response.Position,
                "The approved spawn pose must follow the component's transform, not a hard-coded point.");
        }
    }
}
