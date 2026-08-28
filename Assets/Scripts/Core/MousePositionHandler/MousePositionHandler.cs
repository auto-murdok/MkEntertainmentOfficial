using UnityEngine;

public class MousePositionHandler : Subject<AICharacterActions, Vector3>
{
    private const int LeftMouseButton = 0;
    private const float RaycastMaxDistance = 20f;

    private void OnValidate()
    {
        Debug.Log("Changes made, Validating...");
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(LeftMouseButton))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            bool isHit = Physics.Raycast(ray, out RaycastHit hit, RaycastMaxDistance);
            if (isHit)
            {
                NotifyObservers(AICharacterActions.MoveToDestination, hit.point);
            }
        }
    }
}
