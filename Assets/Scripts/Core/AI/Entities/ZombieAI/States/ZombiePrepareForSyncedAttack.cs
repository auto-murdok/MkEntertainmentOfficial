using UnityEngine;

public class ZombiePrepareForSyncedAttack : State<ZombieStates, ZombieContext>
{
    float startTime;

    public void CheckTransitions(StateMachine<ZombieStates, ZombieContext> character)
    {
        float zombieVerticalMovement = character._context.animator.GetFloat("Vertical");
        if (zombieVerticalMovement < 0.01f)
        {
            Debug.LogWarning($"Vertical movement: {zombieVerticalMovement}");
            character.ChangeState(ZombieStates.Bitting);
        }
    }

    public void EnterState(StateMachine<ZombieStates, ZombieContext> character)
    {
        startTime = Time.time;
    }

    public void ExitState(StateMachine<ZombieStates, ZombieContext> character)
    {
        Debug.LogWarning($"Transition time: {startTime - Time.time}");
    }

    public void UpdateState(StateMachine<ZombieStates, ZombieContext> character)
    {

    }
}