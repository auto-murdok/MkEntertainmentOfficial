using UnityEngine;

/// <summary>
/// Handles runtime swapping of the visual player model under a single reusable player prefab.
/// The model prefab's hierarchy is merged directly under the player root (like the baked
/// model) so the root Animator can drive it with the model's avatar and apply root motion
/// to the player root. System children (animation rig layers, camera hook) are retained.
/// </summary>
[DisallowMultipleComponent]
public class PlayerModelSlot : MonoBehaviour
{
    [Header("Model Configuration")]
    [Tooltip("Default model prefab used when no baked model exists in the player prefab.")]
    [SerializeField] private GameObject _defaultModelPrefab;

    [Tooltip("Root children that are system-owned and never removed during a model swap.")]
    [SerializeField] private string[] _retainedChildren = { "Rig", "3rdPersonCameraHook" };

    private string _currentModelName = string.Empty;

    /// <summary>Name of the currently active model prefab.</summary>
    public string currentModelName => _currentModelName;

    /// <summary>Swaps in the default model prefab, if one is configured.</summary>
    [ContextMenu("Swap To Default Model")]
    public bool SwapToDefaultModel()
    {
        if (_defaultModelPrefab == null)
        {
            Debug.LogWarning($"[{name}] No default model prefab assigned on PlayerModelSlot.");
            return false;
        }

        return SwapModel(_defaultModelPrefab);
    }

    /// <summary>
    /// Replaces the current visual model with the supplied model prefab: merges its
    /// hierarchy under the player root, rebinds the root Animator to its avatar,
    /// preserves the equipped weapon, and rebinds sockets.
    /// </summary>
    public bool SwapModel(GameObject modelPrefab)
    {
        if (modelPrefab == null)
        {
            Debug.LogWarning($"[{name}] Cannot swap model: prefab is null.");
            return false;
        }

        PlayerSockets sockets = GetComponent<PlayerSockets>();
        CharacterLocomotion locomotion = GetComponent<CharacterLocomotion>();

        // Preserve the gun hook (and any equipped weapon parented under it) by
        // lifting it out of the old skeleton before it is destroyed.
        Transform preservedGunHook = null;
        if (sockets != null && sockets.weaponHolder != null && sockets.weaponHolder.name == PlayerSockets.GunHookName)
        {
            preservedGunHook = sockets.weaponHolder;
            preservedGunHook.SetParent(transform, false);
        }
        else if (locomotion != null)
        {
            locomotion.DetachEquippedWeaponToRoot();
        }

        // The root Animator stays the single animation driver; only its avatar changes.
        Animator rootAnimator = GetComponent<Animator>();
        RuntimeAnimatorController controllerToKeep = rootAnimator != null ? rootAnimator.runtimeAnimatorController : null;

        DestroyCurrentModel();

        GameObject instance = Instantiate(modelPrefab, transform);
        Animator modelAnimator = instance.GetComponentInChildren<Animator>();
        Avatar modelAvatar = modelAnimator != null ? modelAnimator.avatar : null;

        // Prefer the incoming model's own gun hook over the preserved one.
        Transform existingHook = null;
        Transform[] modelTransforms = instance.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < modelTransforms.Length; i++)
        {
            if (modelTransforms[i].name == PlayerSockets.GunHookName)
            {
                existingHook = modelTransforms[i];
                break;
            }
        }

        // Merge the model hierarchy directly under the player root so animator
        // avatar paths and root motion match the baked-model layout.
        for (int i = instance.transform.childCount - 1; i >= 0; i--)
        {
            instance.transform.GetChild(i).SetParent(transform, false);
        }
        Object.DestroyImmediate(instance);

        if (existingHook != null)
        {
            if (preservedGunHook != null)
            {
                // Lift the weapon out first, then remove the preserved hook
                // immediately so socket rebinding below resolves the model's
                // own hook, not a doomed duplicate.
                if (locomotion != null)
                {
                    locomotion.DetachEquippedWeaponToRoot();
                }
                Object.DestroyImmediate(preservedGunHook.gameObject);
            }
        }
        else if (preservedGunHook != null && rootAnimator != null && rootAnimator.isHuman)
        {
            Transform rightHand = rootAnimator.GetBoneTransform(HumanBodyBones.RightHand);
            if (rightHand != null)
            {
                preservedGunHook.SetParent(rightHand, false);
            }
        }

        if (rootAnimator != null)
        {
            if (modelAvatar != null)
            {
                rootAnimator.avatar = modelAvatar;
            }
            if (rootAnimator.runtimeAnimatorController == null && controllerToKeep != null)
            {
                rootAnimator.runtimeAnimatorController = controllerToKeep;
            }
            rootAnimator.enabled = true;
            rootAnimator.Rebind();
        }

        LayerUtils.SetLayer(transform, LayerUtils.LocalPlayerLayerName);
        RagdollUtils.DisableRagdoll(transform);

        _currentModelName = modelPrefab.name;

        if (sockets != null)
        {
            sockets.RebindModelHooks(rootAnimator);
        }

        if (locomotion != null)
        {
            locomotion.OnModelSwapped(rootAnimator);
        }

        Debug.Log($"[{name}] Swapped model to '{modelPrefab.name}'.");
        return true;
    }

    private void DestroyCurrentModel()
    {
        _currentModelName = string.Empty;

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (IsRetained(child.name) || child.name == PlayerSockets.GunHookName)
            {
                continue;
            }

            // Immediate teardown so socket rebinding below never resolves
            // against the outgoing model's stale bones and hooks.
            Object.DestroyImmediate(child.gameObject);
        }
    }

    private bool IsRetained(string childName)
    {
        for (int i = 0; i < _retainedChildren.Length; i++)
        {
            if (childName.Equals(_retainedChildren[i], System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
