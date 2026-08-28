using UnityEngine;

/// <summary>
/// Exposes and manages standard socket hooks (camera follow target, weapon holder, aim target)
/// for any humanoid player model, with automatic fallback to Animator humanoid bones.
/// Survives runtime model swaps: hooks that live inside a model skeleton are re-resolved
/// via RebindModelHooks after a new model is instantiated.
/// </summary>
[DisallowMultipleComponent]
public class PlayerSockets : MonoBehaviour
{
    public const string CameraHookName = "3rdPersonCameraHook";
    public const string GunHookName = "GunHookPoint";
    public const string AimTargetName = "AimTarget";

    [Header("Socket References")]
    [Tooltip("Follow target the Cinemachine virtual camera tracks (e.g. head-height pivot).")]
    [SerializeField] private Transform _cameraHook;

    [Tooltip("Transform under the right hand where weapons are attached.")]
    [SerializeField] private Transform _weaponHolder;

    [Tooltip("World-space point weapons aim at.")]
    [SerializeField] private Transform _aimTarget;

    public Transform cameraHook => _cameraHook != null ? _cameraHook : transform;
    public Transform weaponHolder => _weaponHolder;
    public Transform aimTarget => _aimTarget;

    private void Awake()
    {
        InitializeSockets();
    }

    /// <summary>
    /// Resolves any missing sockets via named children, Animator humanoid bones,
    /// or dynamic fallback transforms. Safe to call repeatedly.
    /// </summary>
    public void InitializeSockets()
    {
        Animator animator = GetComponentInChildren<Animator>();

        if (_cameraHook == null)
        {
            _cameraHook = FindChildRecursive(transform, CameraHookName);
            if (_cameraHook == null && animator != null && animator.isHuman)
            {
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (head != null)
                {
                    _cameraHook = CreateSocket(head, CameraHookName, new Vector3(0f, 0.2f, 0f));
                }
            }
            if (_cameraHook == null)
            {
                _cameraHook = CreateSocket(transform, CameraHookName, new Vector3(0f, 1.6f, 0f));
            }
        }

        if (_weaponHolder == null)
        {
            _weaponHolder = FindChildRecursive(transform, GunHookName);
            if (_weaponHolder == null && animator != null && animator.isHuman)
            {
                Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (rightHand != null)
                {
                    _weaponHolder = CreateSocket(rightHand, GunHookName, Vector3.zero);
                }
            }
        }

        if (_aimTarget == null)
        {
            _aimTarget = FindChildRecursive(transform, AimTargetName);
            if (_aimTarget == null && animator != null && animator.isHuman)
            {
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (head != null)
                {
                    _aimTarget = CreateSocket(head, AimTargetName, new Vector3(0f, 0f, 5f));
                }
            }
            if (_aimTarget == null)
            {
                _aimTarget = CreateSocket(transform, AimTargetName, new Vector3(0f, 1.5f, 5f));
            }
        }
    }

    /// <summary>
    /// Re-resolves hooks that were parented inside a model skeleton after a model swap.
    /// The camera hook is a root-level system child and is left untouched.
    /// </summary>
    public void RebindModelHooks(Animator newModelAnimator)
    {
        _weaponHolder = null;
        _aimTarget = null;

        _weaponHolder = FindChildRecursive(transform, GunHookName);
        if (_weaponHolder == null && newModelAnimator != null && newModelAnimator.isHuman)
        {
            Transform rightHand = newModelAnimator.GetBoneTransform(HumanBodyBones.RightHand);
            if (rightHand != null)
            {
                _weaponHolder = CreateSocket(rightHand, GunHookName, Vector3.zero);
            }
        }

        _aimTarget = FindChildRecursive(transform, AimTargetName);
        if (_aimTarget == null)
        {
            if (_cameraHook != null && _cameraHook != transform)
            {
                _aimTarget = CreateSocket(_cameraHook, AimTargetName, new Vector3(0f, -0.1f, 5f));
            }
            else
            {
                _aimTarget = CreateSocket(transform, AimTargetName, new Vector3(0f, 1.5f, 5f));
            }
        }

        Debug.Log($"[{name}] PlayerSockets rebound: Camera={_cameraHook?.name}, Gun={_weaponHolder?.name}, Aim={_aimTarget?.name}");
    }

    [ContextMenu("Auto-Bind Sockets")]
    public void AutoBindSockets()
    {
        _cameraHook = null;
        InitializeSockets();
        Debug.Log($"[{name}] PlayerSockets auto-bound: Camera={_cameraHook?.name}, Gun={_weaponHolder?.name}, Aim={_aimTarget?.name}");
    }

    private static Transform CreateSocket(Transform parent, string socketName, Vector3 localPosition)
    {
        GameObject socketObj = new GameObject(socketName);
        socketObj.transform.SetParent(parent, false);
        socketObj.transform.localPosition = localPosition;
        socketObj.transform.localRotation = Quaternion.identity;
        return socketObj.transform;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent.name.Equals(childName, System.StringComparison.OrdinalIgnoreCase))
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindChildRecursive(parent.GetChild(i), childName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}
