using Unity.Netcode;
using UnityEngine;

// Client/server player composition for the networked arena.
//
// NGO spawns the player prefab on every peer (NetworkManager.PlayerPrefab).
// The prefab cannot carry scene references, so the owner composes the
// local-only rig parts (input handler, core components/UI, aim target,
// cameras, HUDs) in OnNetworkSpawn via the shared PlayerRigging helper.
// Remote players do nothing here — their root transform streams in through
// the NetworkTransform.
public class NetworkedPlayerComposition : NetworkBehaviour
{
    [Header("Local-only rig prefabs (instantiated on the owner)")]
    [SerializeField] private GameObject _inputHandlerPrefab;
    [SerializeField] private GameObject _playerCorePrefab;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            // Remote player: the pose arrives via the owner-authoritative
            // NetworkTransform, so root motion must NOT also drive the
            // transform here (double application / drift). The animator still
            // plays the replicated state for the visuals.
            if (TryGetComponent<Animator>(out Animator animator))
            {
                animator.applyRootMotion = false;
            }
            return;
        }

        InputHandler inputHandler = Instantiate(_inputHandlerPrefab).GetComponent<InputHandler>();
        GameObject playerCore = Instantiate(_playerCorePrefab);
        PlayerRigging.WireLocalRig(gameObject, inputHandler, playerCore);

        Debug.Log($"[NetworkedPlayerComposition] Local rig composed for {(IsHost ? "host" : "client")} player object (OwnerId={OwnerClientId}).");
    }
}
