using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public class StateMachine<TStateKey, TContext> : MonoBehaviour
    where TContext : struct
{
    [SerializeField] private TStateKey currentStateEnum;
    private State<TStateKey, TContext> _currentState; // Reference to the current state
    public Action<TStateKey> OnCommonUpdate;
    public Dictionary<TStateKey, State<TStateKey, TContext>> states = new Dictionary<TStateKey, State<TStateKey, TContext>>();
    public TContext _context = new TContext();

    private const string StateTransitionLogPrefix = "ENTERING -> ";

    void Start()
    {
        Assert.IsTrue(states.Count > 0, "Please set at least one state.");
        _currentState = states[currentStateEnum];
        _currentState.EnterState(this);
    }

    void Update()
    {
        // common update
        OnCommonUpdate?.Invoke(currentStateEnum);
        _currentState.UpdateState(this);
        _currentState.CheckTransitions(this);
    }

    // Function to change state
    public void ChangeState(TStateKey newState)
    {
        // state machine behaviour
        _currentState.ExitState(this);
        currentStateEnum = newState;
        _currentState = states[newState];
        _currentState.EnterState(this);
        Debug.LogWarning(StateTransitionLogPrefix + newState);
    }
}
