using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExampleCube : Subject<CubeActions, int>
{
    // Start is called before the first frame update
    void Start()
    {
        NotifyObservers(CubeActions.TEST_ACTION_2, 0);
    }
}
