using UnityEngine;

// Zombie-specific wrapper around the reusable ActorMoveToTargetState. It only
// adds the return-home / interrupt transitions that are specific to the
// ZombieStates enum; all movement logic lives in the shared base state.
public class ZombieCommandedMoveState : ActorMoveToTargetState<ZombieStates, ZombieContext>
{
    public override void CheckTransitions(StateMachine<ZombieStates, ZombieContext> character)
    {
        ZombieContext context = character._context;

        if (context.isBitting)
        {
            character.ChangeState(ZombieStates.Bitting);
            return;
        }

        if (context.moveDestination == null)
        {
            character.ChangeState(ZombieStates.Idle);
        }
    }
}
