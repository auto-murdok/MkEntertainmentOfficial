using UnityEngine;

public class AimTarget : MonoBehaviour
{
    [SerializeField] private LayerMask _aimColliderMask;
    // Wired by the PlayerSpawner at runtime (scene reference, cannot live in a prefab).
    public Transform _fallbackMouseWorldHook;

    // How far the center-screen ray is cast when searching for an aim point.
    private const float RaycastMaxDistance = 999f;

    private Camera _mainCamera;

    // The aim point from THIS frame's center-screen ray, un-smoothed.
    // Shots and aim rigging consume the aim point through this property /
    // the marker transform (which snaps to it): any smoothing here makes
    // the shot direction chase behind the crosshair during fast aim
    // movement — the classic TPS "aiming feels weird" complaint.
    public Vector3 CurrentAimPoint { get; private set; }

    private void Awake()
    {
        _mainCamera = Camera.main;
    }

    // Update is called once per frame
    void Update()
    {
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null) return;
        }

        Vector3 targetWorldPosition;
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Ray ray = _mainCamera.ScreenPointToRay(screenCenter);
        if (Physics.Raycast(ray, out RaycastHit raycastHit, RaycastMaxDistance, _aimColliderMask))
        {
            targetWorldPosition = raycastHit.point;
        }
        else
        {
            // No surface hit: fall back to the configured world hook position.
            targetWorldPosition = _fallbackMouseWorldHook != null ? _fallbackMouseWorldHook.position : transform.position;
        }

        // Snap, never lerp: the marker IS the aim point. A smoothed marker
        // visibly swims, and everything that reads it (shots, aim IK)
        // inherits the lag.
        CurrentAimPoint = targetWorldPosition;
        transform.position = targetWorldPosition;
    }
}
