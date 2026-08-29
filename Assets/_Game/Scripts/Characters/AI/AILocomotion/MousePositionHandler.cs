using UnityEngine;
using UnityEngine.InputSystem;

public class MousePositionHandler : Subject<AICharacterActions, Vector3>
{
    private const float RaycastMaxDistance = 20f;

    private Camera _mainCamera;

    private void Awake()
    {
        _mainCamera = Camera.main;
    }

    void Update()
    {
        // Input System API (project standard) — the legacy Input Manager calls
        // break when Active Input Handling is set to Input System only.
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
        {
            return;
        }

        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null) return;
        }

        Ray ray = _mainCamera.ScreenPointToRay(mouse.position.ReadValue());
        bool isHit = Physics.Raycast(ray, out RaycastHit hit, RaycastMaxDistance);
        if (isHit)
        {
            NotifyObservers(AICharacterActions.MoveToDestination, hit.point);
        }
    }
}
