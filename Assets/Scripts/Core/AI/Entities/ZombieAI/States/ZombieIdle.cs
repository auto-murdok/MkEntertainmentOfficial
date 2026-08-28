using UnityEngine;

public class ZombieIdle : State<ZombieStates, ZombieContext>
{
    public void CheckTransitions(StateMachine<ZombieStates, ZombieContext> character)
    {
        if (character._context.target != null)
        {
            character.ChangeState(ZombieStates.Chasing);
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