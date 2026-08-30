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
public class NetworkedPlayerComposition : NetworkBehaviour, IPlayerBiteRelay
{
    private const int AimAnimatorLayerIndex = 1;
    private const float AimLayerWeightSpeed = 20f;
    private const float RemoteAimTargetHeight = 1.5f;   // chest height
    private const float RemoteAimTargetDistance = 8f;   // ahead of the character

    [Header("Local-only rig prefabs (instantiated on the owner)")]
    [SerializeField] private GameObject _inputHandlerPrefab;
    [SerializeField] private GameObject _playerCorePrefab;

    [Header("Game-flow (SO asset refs persist on the prefab)")]
    [Tooltip("Raised on the peer that owns this player when it dies, so the local game-over screen fires on clients too.")]
    [SerializeField] private VoidEventChannel _playerDiedChannel;

    // Owner-write: the player decides its own aim pose; everyone reads it.
    private readonly NetworkVariable<bool> _isAiming = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    // Bite mirror: the owner runs the take-bite FSM and publishes its state so
    // remote copies (including the host, where the zombie AI runs) can answer
    // IBiteTarget availability checks (CanVictimBeBitten) correctly.
    private readonly NetworkVariable<bool> _isBitten = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<ulong> _biterObjectId = new NetworkVariable<ulong>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private CharacterLocomotion _locomotion;
    private CharacterBrain _brain;
    private Animator _animator;
    private Rig _rig;

    public bool SimulatesLocally => !IsSpawned || IsOwner;

    public bool MirroredIsBitten => _isBitten.Value;

    public IInteractable MirroredBiter
    {
        get
        {
            if (_biterObjectId.Value == 0 || NetworkManager.Singleton == null)
            {
                return null;
            }
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(_biterObjectId.Value, out NetworkObject attacker))
            {
                return attacker.GetComponent<ZombieBrain>();
            }
            return null;
        }
    }

    public override void OnNetworkSpawn()
    {
        _locomotion = GetComponent<CharacterLocomotion>();
        _brain = GetComponent<CharacterBrain>();
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

        // Client game-flow: this peer owns the player, so its death must fire
        // the local game-over screen. (PlayerSpawner wires this in the
        // single-player arena; the networked path wires it here — wire exactly
        // once per peer.)
        if (_playerDiedChannel != null)
        {
            GetComponent<CharacterBrain>().Died += _playerDiedChannel.Raise;
        }

        Debug.Log($"[NetworkedPlayerComposition] Local rig composed for {(IsHost ? "host" : "client")} player object (OwnerId={OwnerClientId}).");
    }

    // Remote copies instantiate with the prefab's NULL constraint sources
    // (prefab -> scene refs are stripped), so their rig graph aims at nothing.
    // Give the constraints a local forward-mounted target (it rotates with the
    // replicated transform, so remote aim tracks where the character faces)
    // and rebuild the graph — RigBuilder bakes during Instantiate, so sources
    // wired after that are ignored until Clear()+Build() (see
    // docs/spawnable_player_rigging_fixes.md).
    //
    // Public test seam (same convention as MainMenuController.BuildUI): every
    // non-owner spawn path must call this or remote rigged aim silently
    // degrades to null-source constraints.
    public void BuildRemoteRig()
    {
        var rigBuilder = GetComponent<RigBuilder>();
        if (rigBuilder == null)
        {
            return;
        }

        // Idempotent: re-running (re-spawn paths) reuses the existing target
        // instead of stacking duplicate children.
        Transform remoteAimTarget = transform.Find("RemoteAimTarget");
        if (remoteAimTarget == null)
        {
            remoteAimTarget = new GameObject("RemoteAimTarget").transform;
            remoteAimTarget.SetParent(transform, false);
            remoteAimTarget.localPosition = new Vector3(0f, RemoteAimTargetHeight, RemoteAimTargetDistance);
        }

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
            MirrorBiteStateToOwner();
            return;
        }

        UpdateRemoteAimVisuals();
    }

    // The owner runs the take-bite FSM; its state is mirrored back so the
    // host (where the zombie AI + IBiteTarget checks run) sees the victim as
    // pinned. Change-checked: NetworkVariables only send on write anyway, but
    // skipping unchanged writes avoids allocation-free per-frame redundancy.
    private void MirrorBiteStateToOwner()
    {
        // Death destroys the FSM (CharacterBrain.OnRagdollEnabled) — a corpse
        // must publish "not bitten" instead of freezing on the last value.
        bool bitten = _locomotion != null && _locomotion.isBeingAttacked;
        if (_isBitten.Value != bitten)
        {
            _isBitten.Value = bitten;
        }

        ulong biterId = 0;
        if (bitten && _locomotion.currentAttacker is Component attackerComponent)
        {
            var attackerNetworkObject = attackerComponent.GetComponentInParent<NetworkObject>();
            if (attackerNetworkObject != null)
            {
                biterId = attackerNetworkObject.NetworkObjectId;
            }
        }
        if (_biterObjectId.Value != biterId)
        {
            _biterObjectId.Value = biterId;
        }
    }

    // ── IPlayerBiteRelay ────────────────────────────────────────────────────

    // Server-side: the bite interaction lands here (zombie AI runs on the
    // host), but the victim-side take-bite FSM belongs to the victim's owner.
    // Forward the attacker reference; the owner runs its local pipeline and
    // its NetworkAnimator/transform replicate the result to everyone.
    public void RelayBiteFromServer(IInteractable attacker)
    {
        var attackerComponent = attacker as Component;
        NetworkObject attackerNetworkObject = attackerComponent != null
            ? attackerComponent.GetComponentInParent<NetworkObject>()
            : null;
        if (attackerNetworkObject == null)
        {
            Debug.LogWarning("[NetworkedPlayerComposition] Bite relay skipped: attacker has no NetworkObject.");
            return;
        }
        NotifyVictimBittenClientRpc(new NetworkObjectReference(attackerNetworkObject));
    }

    [ClientRpc]
    private void NotifyVictimBittenClientRpc(NetworkObjectReference attackerRef)
    {
        if (!IsOwner)
        {
            return; // only the victim's owner simulates the take-bite
        }
        if (!attackerRef.TryGet(out NetworkObject attackerNetworkObject))
        {
            Debug.LogWarning("[NetworkedPlayerComposition] Bite relay failed: attacker NetworkObject not found.");
            return;
        }

        IInteractable attacker = attackerNetworkObject.GetComponent<ZombieBrain>();
        if (attacker == null)
        {
            return;
        }

        // Normal victim-side pipeline on the owner (guard, flags, TakeBite
        // trigger — which replicates through the owner's NetworkAnimator —
        // and the initial pin to the bite socket).
        if (_brain != null)
        {
            _brain.OnExternalInteraction(attacker);
        }
    }

    // ── Remote aim visuals ──────────────────────────────────────────────────

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
