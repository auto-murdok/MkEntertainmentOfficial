using UnityEngine;

public class ZombieBitingState : State<ZombieStates, ZombieContext>
{
    private const float VerticalMovementPrepareThreshold = 0.15f;
    private const float DefaultBittingRadius = 0.1f;
    private const float DefaultRadius = 0.3f;
    private const float DefaultAttackCooldown = 1.2f;

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
        float bittingRadius = context.data != null ? context.data.bittingAgentRadius : DefaultBittingRadius;
        if (context.agent != null)
        {
            context.agent.radius = bittingRadius;
            context.agent.ResetPath();
        }
        context.isPreparing = true;
    }

    public void ExitState(StateMachine<ZombieStates, ZombieContext> character)
    {
        ZombieContext context = character._context;
        float defaultRadius = context.data != null ? context.data.defaultAgentRadius : DefaultRadius;
        if (context.agent != null)
        {
            context.agent.radius = defaultRadius;
        }
        context.isPreparing = false;
        context.attackCooldownTimer = DefaultAttackCooldown;
        context.interactable = null;
    }

    public void UpdateState(StateMachine<ZombieStates, ZombieContext> character)
    {
        float verticalMovement = character._context.animator != null ? character._context.animator.GetFloat(AnimatorUtils.VerticalHash) : 0f;
        if (verticalMovement > VerticalMovementPrepareThreshold)
        {
            character.transform.position = _initialPosition;
            character.transform.rotation = _initialRotation;
            character._context.isPreparing = true;
            Debug.LogWarning($"{character.gameObject.name} is preparing...");
        }
        else if (character._context.isPreparing)
        {
            character._context.isPreparing = false;
        }
    }
}

public class ZombieBitting : ZombieBitingState {}
