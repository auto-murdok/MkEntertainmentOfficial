using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public class StateMachine<TStateKey, TContext> : MonoBehaviour
    where TContext : Blackboard, new()
{
    [SerializeField] private TStateKey currentStateEnum;
    [SerializeField] protected bool debugStateMachine;

    private State<TStateKey, TContext> _currentState; // Reference to the current state
    public Action<TStateKey> OnCommonUpdate;
    public event Action<TStateKey> OnStateChanged;
    public Dictionary<TStateKey, State<TStateKey, TContext>> states = new Dictionary<TStateKey, State<TStateKey, TContext>>();
    public TContext _context = new TContext();

    // Evaluated every frame after the current state's own CheckTransitions.
    // Return a state key different from the current one to force a transition
    // (e.g. a global "death" guard). Return the current key to do nothing.
    public Func<TStateKey, TStateKey> CheckGlobalTransition;

    // Deferred transition: at most one state change is applied per Update, after
    // CheckTransitions has run. This prevents re-entrancy and multiple transitions
    // in a single evaluation.
    private bool _hasPendingTransition;
    private TStateKey _pendingState;

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

        // global transition guard (e.g. death) evaluated after the state's own checks
        if (CheckGlobalTransition != null)
        {
            TStateKey forced = CheckGlobalTransition(currentStateEnum);
            if (!EqualityComparer<TStateKey>.Default.Equals(forced, currentStateEnum))
            {
                ChangeState(forced);
            }
        }

        if (_hasPendingTransition)
        {
            TStateKey next = _pendingState;
            _hasPendingTransition = false;

            _currentState.ExitState(this);
            currentStateEnum = next;
            _currentState = states[next];
            _currentState.EnterState(this);

            if (debugStateMachine)
            {
                Debug.Log($"[{gameObject.name}] -> {currentStateEnum}");
            }

            OnStateChanged?.Invoke(currentStateEnum);
        }
    }

    // Request a state change. The swap is deferred to the end of the current
    // Update so a state can never be left/exited more than once per frame.
    // First request wins within a frame; later requests are ignored (this gives
    // priority to the transition evaluated first, e.g. "being attacked").
    public void ChangeState(TStateKey newState)
    {
        if (EqualityComparer<TStateKey>.Default.Equals(newState, currentStateEnum))
        {
            return;
        }

        if (!states.ContainsKey(newState))
        {
            Debug.LogError($"[{gameObject.name}] Requested state '{newState}' is not registered.");
            return;
        }

        if (_hasPendingTransition)
        {
            return;
        }

        _pendingState = newState;
        _hasPendingTransition = true;
    }

    public TStateKey CurrentStateName => currentStateEnum;

    private void OnGUI()
    {
        if (debugStateMachine)
        {
            GUI.Label(new Rect(10, 10, 400, 20), $"{gameObject.name}: {currentStateEnum}");
        }
    }
}
