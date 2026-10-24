using UnityEngine;

public class AimTarget : MonoBehaviour
{
    [SerializeField] private LayerMask _aimColliderMask;

    // How far the center-screen ray is cast when searching for an aim point.
    private const float RaycastMaxDistance = 999f;
    // How quickly the target marker catches up to the desired world position.
    private const float FollowLerpSpeed = 10f;

    // The ground plane (y = 0). A center-screen ray that hits no collider is
    // intersected with this plane — never with a camera-relative point. The
    // previous fallback was a hook 25 m in front of the camera; when aiming
    // down it sat far underground and whipped the aim rig (and with it the
    // camera) toward it, reading as a sudden jump to first person.
    private static readonly Plane GroundPlane = new Plane(Vector3.up, Vector3.zero);

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
        else if (GroundPlane.Raycast(ray, out float groundDistance) && groundDistance <= RaycastMaxDistance)
        {
            // No collider hit (e.g. aiming past the arena's edge): aim where
            // the ray meets the ground instead.
            targetWorldPosition = ray.GetPoint(groundDistance);
        }
        else
        {
            // The ray points at the sky: hold the last aim position.
            targetWorldPosition = transform.position;
        }

        transform.position = Vector3.Lerp(transform.position, targetWorldPosition, AnimatorUtils.DampFactor(FollowLerpSpeed, Time.deltaTime));
    }
}
