using UnityEngine;

public class AimTarget : MonoBehaviour
{
    [SerializeField] private LayerMask _aimColliderMask;
    [SerializeField] private Transform _fallbackMouseWorldHook;

    // How far the center-screen ray is cast when searching for an aim point.
    private const float RaycastMaxDistance = 999f;
    // How quickly the target marker catches up to the desired world position.
    private const float FollowLerpSpeed = 10f;

    private Camera _mainCamera;

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

        transform.position = Vector3.Lerp(transform.position, targetWorldPosition, FollowLerpSpeed * Time.deltaTime);
    }
}
