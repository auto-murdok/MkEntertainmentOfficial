using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MousePositionHandler : Subject<AICharacterActions, Vector3>
{
    private void OnValidate() {
        Debug.Log("Changes made, Validating...");
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            bool isHit = Physics.Raycast(ray, out RaycastHit hit, 20);
            if (isHit) {
                NotifyObservers(AICharacterActions.MoveToDestination, hit.point);
            }
        }
    }
}
