using UnityEngine;

public class ZombieBitting : State<ZombieStates, ZombieContext>
{
    private Vector3 initialPosition;
    private Quaternion initialRotation;

    public void CheckTransitions(StateMachine<ZombieStates, ZombieContext> character)
    {
        if (!character._context.isBitting)
        {
            character.ChangeState(ZombieStates.Idle);
        }
    }

    public void EnterState(StateMachine<ZombieStates, ZombieContext> character)
    {
        initialPosition = character.transform.position;
        initialRotation = character.transform.rotation;
        ZombieContext context = character._context;
        context.agent.radius = 0.1f;
        character._context.isPreparing = true;
    }

    public void ExitState(StateMachine<ZombieStates, ZombieContext> character)
    {
        ZombieContext context = character._context;
        context.agent.radius = 0.3f;
        character.gameObject.SetActive(true);
        context.interactable = null;
    }

    public void UpdateState(StateMachine<ZombieStates, ZombieContext> character)
    {
        float zombieVerticalMovement = character._context.animator.GetFloat("Vertical");
        if (zombieVerticalMovement > 0.15f)
        {
            character.transform.position = initialPosition;
            character.transform.rotation = initialRotation;
            Debug.LogWarning($"{character.gameObject.name} is preparing...");
        }
        else if (character._context.isPreparing)
        {
            character._context.isPreparing = false;
        }
    }
}