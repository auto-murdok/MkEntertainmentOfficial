using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

public class StateMachine<EGenericEnum, EGenericStruct> : MonoBehaviour
    where EGenericStruct : struct
{
    [SerializeField] private EGenericEnum currentStateEnum;
    private State<EGenericEnum, EGenericStruct> currentState; // Reference to the current state
    public Action<EGenericEnum> OnCommonUpdate;
    public Dictionary<EGenericEnum, State<EGenericEnum, EGenericStruct>> states = new Dictionary<EGenericEnum, State<EGenericEnum, EGenericStruct>>();
    public EGenericStruct _context = new EGenericStruct();

    void Start()
    {
        Assert.IsTrue(states.Count > 0, "Please set at least one state.");
        currentState = states[currentStateEnum];
        currentState.EnterState(this);
    }

    void Update()
    {
        // common update
        OnCommonUpdate?.Invoke(currentStateEnum);
        currentState.UpdateState(this);
        currentState.CheckTransitions(this);
    }

    // Function to change state
    public void ChangeState(EGenericEnum newState)
    {
        // state machine behaviour
        currentState.ExitState(this);
        currentStateEnum = newState;
        currentState = states[newState];
        currentState.EnterState(this);
        Debug.LogWarning("ENTERING -> " + newState);
    }
}
