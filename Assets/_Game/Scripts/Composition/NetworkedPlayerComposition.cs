using Unity.Netcode;
using UnityEngine;
using UnityEngine.Animations.Rigging;

// Client/server player composition for the networked arena.
//
// NGO spawns the player prefab on every peer (NetworkManager.PlayerPrefab).
// The prefab cannot carry scene references, so the owner composes the
// local-only rig parts (input handler, core components/UI, aim target,
// cameras, HUDs) in OnNetworkSpawn via the shared PlayerRigging helper.
//
// Non-owner copies run a reduced runtime: no gameplay FSM (it would fight the
// NetworkAnimator-replicated state — aim visuals died after ~1s), no root
// motion (pose comes from the NetworkTransform). Visual-only flags the FSM
// drives on the owner (aim pose layer + rig weight) are replicated here via
// NetworkVariable and applied with the same damped writers the FSM uses.
public class NetworkedPlayerComposition : NetworkBehaviour
{
    private const int AimAnimatorLayerIndex = 1;
    private const float AimLayerWeightSpeed = 20f;

    [Header("Local-only rig prefabs (instantiated on the owner)")]
    [SerializeField] private GameObject _inputHandlerPrefab;
    [SerializeField] private GameObject _playerCorePrefab;

    // Owner-write: the player decides its own aim pose; everyone reads it.
    private readonly NetworkVariable<bool> _isAiming = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private CharacterLocomotion _locomotion;
    private Animator _animator;
    private Rig _rig;

    public override void OnNetworkSpawn()
    {
        _locomotion = GetComponent<CharacterLocomotion>();
        _animator = GetComponent<Animator>();
        _rig = GetComponentInChildren<Rig>();

        if (!IsOwner)
        {
            // Remote player: the pose arrives via the owner-authoritative
            // NetworkTransform, so root motion must NOT also drive the
            // transform here (double application / drift). The gameplay FSM
            // is disabled — the owner's FSM + NetworkAnimator own the visuals.
            _locomotion.enabled = false;
            if (_animator != null)
            {
                _animator.applyRootMotion = false;
            }
            BuildRemoteRig();
            return;
        }

        InputHandler inputHandler = Instantiate(_inputHandlerPrefab).GetComponent<InputHandler>();
        GameObject playerCore = Instantiate(_playerCorePrefab);
        PlayerRigging.WireLocalRig(gameObject, inputHandler, playerCore);

        Debug.Log($"[NetworkedPlayerComposition] Local rig composed for {(IsHost ? "host" : "client")} player object (OwnerId={OwnerClientId}).");
    }

    // Remote copies instantiate with the prefab's NULL constraint sources
    // (prefab -> scene refs are stripped), so their rig graph aims at nothing.
    // Give the constraints a local forward-mounted target (it rotates with the
    // replicated transform, so remote aim tracks where the character faces)
    // and rebuild the graph — RigBuilder bakes during Instantiate, so sources
    // wired after that are ignored until Clear()+Build() (see
    // docs/spawnable_player_rigging_fixes.md).
    private void BuildRemoteRig()
    {
        var rigBuilder = GetComponent<RigBuilder>();
        if (rigBuilder == null)
        {
            return;
        }

        Transform remoteAimTarget = new GameObject("RemoteAimTarget").transform;
        remoteAimTarget.SetParent(transform, false);
        remoteAimTarget.localPosition = new Vector3(0f, 1.5f, 8f); // chest height, ahead

        foreach (MultiAimConstraint constraint in GetComponentsInChildren<MultiAimConstraint>())
        {
            var data = constraint.data;
            data.sourceObjects = new WeightedTransformArray { new WeightedTransform(remoteAimTarget, 1f) };
            constraint.data = data;
        }

        rigBuilder.Clear();
        rigBuilder.Build();
    }

    private void Update()
    {
        if (!IsSpawned)
        {
            return;
        }

        if (IsOwner)
        {
            // Replicate the aim flag for remote visual writers.
            if (_locomotion != null && _locomotion.isAiming != _isAiming.Value)
            {
                _isAiming.Value = _locomotion.isAiming;
            }
            return;
        }

        UpdateRemoteAimVisuals();
    }

    // Mirrors the FSM's aim writers (CharacterAimState): aim layer weight up
    // while aiming OR reloading (the isReloading param is already replicated
    // by the NetworkAnimator), rig weight up while aiming only. Without this,
    // remote copies keep the layer/rig at 0 and never show the aim pose.
    private void UpdateRemoteAimVisuals()
    {
        if (_animator == null)
        {
            return;
        }

        bool aiming = _isAiming.Value;
        bool reloading = _animator.GetBool(AnimatorUtils.IsReloadingHash);

        float layerTarget = aiming || reloading ? 1f : 0f;
        AnimatorUtils.SetLayerWeight(_animator, AimAnimatorLayerIndex, layerTarget, AimLayerWeightSpeed);
        if (_rig != null)
        {
            if (aiming)
            {
                RigUtils.HandleIncreaseRigWeight(_rig);
            }
            else
            {
                RigUtils.HandleDecreaseRigWeight(_rig);
            }
        }
    }
}
