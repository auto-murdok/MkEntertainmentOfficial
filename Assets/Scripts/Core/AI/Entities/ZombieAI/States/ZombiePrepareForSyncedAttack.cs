using UnityEngine;

public class ZombiePrepareForSyncedAttack : State<ZombieStates, ZombieContext>
{
    private const string VerticalParameter = "Vertical";
    private const float VerticalMovementStopThreshold = 0.01f;

    private float _startTime;

    public void CheckTransitions(StateMachine<ZombieStates, ZombieContext> character)
    {
        float verticalMovement = character._context.animator.GetFloat(VerticalParameter);
        if (verticalMovement < VerticalMovementStopThreshold)
        {
            Debug.LogWarning($"Vertical movement: {verticalMovement}");
            character.ChangeState(ZombieStates.Bitting);
        }
    }

    public void EnterState(StateMachine<ZombieStates, ZombieContext> character)
    {
        _startTime = Time.time;
    }

    public void ExitState(StateMachine<ZombieStates, ZombieContext> character)
    {
        Debug.LogWarning($"Transition time: {_startTime - Time.time}");
    }

    public void UpdateState(StateMachine<ZombieStates, ZombieContext> character)
    {
    }
}
