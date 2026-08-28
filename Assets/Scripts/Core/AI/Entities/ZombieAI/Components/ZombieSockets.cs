using UnityEngine;

/// <summary>
/// Exposes and manages standard socket hooks (vision, victim sync, attack triggers)
/// for any 3D zombie model rig, with automatic fallback to Humanoid avatar bones.
/// </summary>
[DisallowMultipleComponent]
public class ZombieSockets : MonoBehaviour
{
    [Header("Socket References")]
    [Tooltip("Point from which line-of-sight raycasts originate (e.g. eyes/head).")]
    [SerializeField] private Transform _visionHook;

    [Tooltip("Point to which the victim snaps during synchronized bite attacks (e.g. mouth/chest).")]
    [SerializeField] private Transform _victimHook;

    [Tooltip("Hand transform holding the attack trigger (e.g. mixamorig:RightHand).")]
    [SerializeField] private Transform _attackHandHook;

    public Transform visionHook => _visionHook != null ? _visionHook : transform;
    public Transform victimHook => _victimHook != null ? _victimHook : transform;
    public Transform attackHandHook => _attackHandHook;

    private void Awake()
    {
        InitializeSockets();
    }

    /// <summary>
    /// Initializes missing sockets via Animator Humanoid bones or creates dynamic fallback transforms.
    /// </summary>
    public void InitializeSockets()
    {
        Animator animator = GetComponentInChildren<Animator>();

        if (_visionHook == null)
        {
            _visionHook = FindChildRecursive(transform, "VisionHook");
            if (_visionHook == null && animator != null && animator.isHuman)
            {
                _visionHook = animator.GetBoneTransform(HumanBodyBones.Head);
            }
            if (_visionHook == null)
            {
                _visionHook = CreateSocket("VisionHook", new Vector3(0, 1.6f, 0.2f));
            }
        }

        if (_victimHook == null)
        {
            _victimHook = FindChildRecursive(transform, "VictimHook");
            if (_victimHook == null)
            {
                _victimHook = CreateSocket("VictimHook", new Vector3(0, 1.4f, 0.5f));
            }
        }

        if (_attackHandHook == null)
        {
            ZombieHand existingHand = GetComponentInChildren<ZombieHand>(true);
            if (existingHand != null)
            {
                _attackHandHook = existingHand.transform;
            }
            else if (animator != null && animator.isHuman)
            {
                _attackHandHook = animator.GetBoneTransform(HumanBodyBones.RightHand);
            }
        }
    }

    [ContextMenu("Auto-Bind Humanoid Bones")]
    public void AutoBindHumanoidBones()
    {
        Animator animator = GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Debug.LogWarning($"[{gameObject.name}] No Animator found for auto-binding humanoid bones.");
            return;
        }

        if (animator.isHuman)
        {
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head != null && _visionHook == null)
            {
                _visionHook = head;
            }

            Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (rightHand != null && _attackHandHook == null)
            {
                _attackHandHook = rightHand;
            }
        }

        if (_victimHook == null)
        {
            _victimHook = FindChildRecursive(transform, "VictimHook");
            if (_victimHook == null)
            {
                _victimHook = CreateSocket("VictimHook", new Vector3(0, 1.4f, 0.5f));
            }
        }

        Debug.Log($"[{gameObject.name}] ZombieSockets auto-bound: Vision={_visionHook?.name}, Victim={_victimHook?.name}, Hand={_attackHandHook?.name}");
    }

    private Transform CreateSocket(string socketName, Vector3 localPosition)
    {
        GameObject socketObj = new GameObject(socketName);
        socketObj.transform.SetParent(transform, false);
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
