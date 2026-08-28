using UnityEngine;

// Reusable "dead / ragdoll" terminal state. Works for any FSM whose context
// derives from ActorBlackboard. The entity-specific death routine is supplied via
// context.onDeath (set by the entity's brain) so this state stays generic and the
// Core assembly remains decoupled from entity-specific utilities.
public class ActorDeadState<TKey, TContext> : State<TKey, TContext>
    where TContext : ActorBlackboard, new()
{
    public void EnterState(StateMachine<TKey, TContext> character)
    {
        TContext context = character._context;
        context.isRagdoll = true;
        context.onDeath?.Invoke();
    }

    public void UpdateState(StateMachine<TKey, TContext> character)
    {
    }

    public void ExitState(StateMachine<TKey, TContext> character)
    {
    }

    public void CheckTransitions(StateMachine<TKey, TContext> character)
    {
    }
}
