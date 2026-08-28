public interface State<TStateKey, TContext> where TContext : struct
{
    void EnterState(StateMachine<TStateKey, TContext> character);
    void UpdateState(StateMachine<TStateKey, TContext> character);
    void ExitState(StateMachine<TStateKey, TContext> character);
    void CheckTransitions(StateMachine<TStateKey, TContext> character);
}
