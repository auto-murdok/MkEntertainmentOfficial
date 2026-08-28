using UnityEngine;

public class AimTarget : MonoBehaviour
{
    [SerializeField] private LayerMask _aimColliderMask;
    [SerializeField] private Transform _fallbackMouseWorldHook;

    // Update is called once per frame
    void Update()
    {
        Vector3 mouseWorldPosition;
        Vector2 screenCenterPoint = new Vector2(Screen.width / 2f, Screen.height / 2f);
        Ray ray = Camera.main.ScreenPointToRay(screenCenterPoint);
        if (Physics.Raycast(ray, out RaycastHit raycastHit, 999f, _aimColliderMask))
        {
            mouseWorldPosition = raycastHit.point;
        }
        else
        {
            mouseWorldPosition = _fallbackMouseWorldHook.position;
        }

        transform.position = Vector3.Lerp(transform.position, mouseWorldPosition, 10f * Time.deltaTime);
    }
}
