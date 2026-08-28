using UnityEngine;

public class ZombieIdle : State<ZombieStates, ZombieContext>
{
    public void CheckTransitions(StateMachine<ZombieStates, ZombieContext> character)
    {
        if (character._context.isBitting)
        {
            character.ChangeState(ZombieStates.Bitting);
        }
        // Hysteresis: only chase again once the target is outside bite range,
        // so the zombie settles at the survivor instead of oscillating.
        else if (character._context.target != null)
        {
            float biteRange = character._context.data != null ? character._context.data.biteRange : ZombieBehavior.DefaultBiteRange;
            if (Vector3.Distance(character.transform.position, character._context.target.TargetPosition) > biteRange)
            {
                character.ChangeState(ZombieStates.Chasing);
            }
        }
    }

    public void EnterState(StateMachine<ZombieStates, ZombieContext> character)
    {
    }

    public void ExitState(StateMachine<ZombieStates, ZombieContext> character)
    {
        // do nothing
    }

    public void UpdateState(StateMachine<ZombieStates, ZombieContext> character)
    {
    }
}