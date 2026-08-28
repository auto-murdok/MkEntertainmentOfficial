using UnityEngine;

public class ZombieBitting : State<ZombieStates, ZombieContext>
{
    private const string VerticalParameter = "Vertical";
    private const float VerticalMovementPrepareThreshold = 0.15f;
    private const float BittingAgentRadius = 0.1f;
    private const float DefaultAgentRadius = 0.3f;

    private Vector3 _initialPosition;
    private Quaternion _initialRotation;

    public void CheckTransitions(StateMachine<ZombieStates, ZombieContext> character)
    {
        if (!character._context.isBitting)
        {
            character.ChangeState(ZombieStates.Idle);
        }
    }

    public void EnterState(StateMachine<ZombieStates, ZombieContext> character)
    {
        _initialPosition = character.transform.position;
        _initialRotation = character.transform.rotation;
        ZombieContext context = character._context;
        context.agent.radius = BittingAgentRadius;
        context.agent.ResetPath();
        context.isPreparing = true;
    }

    public void ExitState(StateMachine<ZombieStates, ZombieContext> character)
    {
        ZombieContext context = character._context;
        context.agent.radius = DefaultAgentRadius;
        context.isPreparing = false;
        context.interactable = null;
    }

    public void UpdateState(StateMachine<ZombieStates, ZombieContext> character)
    {
        float verticalMovement = character._context.animator.GetFloat(VerticalParameter);
        if (verticalMovement > VerticalMovementPrepareThreshold)
        {
            character.transform.position = _initialPosition;
            character.transform.rotation = _initialRotation;
            Debug.LogWarning($"{character.gameObject.name} is preparing...");
        }
    }
}
