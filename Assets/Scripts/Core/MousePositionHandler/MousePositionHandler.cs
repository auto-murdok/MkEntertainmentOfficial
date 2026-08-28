using UnityEngine;

public class MousePositionHandler : Subject<AICharacterActions, Vector3>
{
    private const int LeftMouseButton = 0;
    private const float RaycastMaxDistance = 20f;

    private void OnValidate()
    {
        Debug.Log("Changes made, Validating...");
    }

    private Camera _mainCamera;

    private void Awake()
    {
        _mainCamera = Camera.main;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(LeftMouseButton))
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null) return;
            }

            Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
            bool isHit = Physics.Raycast(ray, out RaycastHit hit, RaycastMaxDistance);
            if (isHit)
            {
                NotifyObservers(AICharacterActions.MoveToDestination, hit.point);
            }
        }
    }
}
