using UnityEngine;

public class ZombieIdleState : State<ZombieStates, ZombieContext>
{
    public void CheckTransitions(StateMachine<ZombieStates, ZombieContext> character)
    {
        if (character._context.isBiting)
        {
            character.ChangeState(ZombieStates.Biting);
        }
        else if (character._context.moveDestination != null)
        {
            character.ChangeState(ZombieStates.CommandedMove);
        }
        else if (character._context.target != null)
        {
            float biteRange = character._context.data != null ? character._context.data.biteRange : ZombieBehavior.DefaultBiteRange;
            float distance = Vector3.Distance(character.transform.position, character._context.target.TargetPosition);

        if (distance > biteRange)
        {
            // The (push-off) animation separated them: re-arm a future bite.
            character._context.recentlyBitten = false;
            character.ChangeState(ZombieStates.Chasing);
        }
        else if (character._context.attackCooldownTimer <= 0f && !character._context.isBiting && !character._context.recentlyBitten)
        {
            // Target is within bite range, cooldown elapsed, and this is a fresh contact:
            // re-engage bite!
            if (character is ZombieBehavior behavior)
            {
                behavior.TryTriggerAttack();
            }
        }
        }
    }

    public void EnterState(StateMachine<ZombieStates, ZombieContext> character)
    {
    }

    public void ExitState(StateMachine<ZombieStates, ZombieContext> character)
    {
    }

    public void UpdateState(StateMachine<ZombieStates, ZombieContext> character)
    {
        if (character._context.attackCooldownTimer > 0f)
        {
            character._context.attackCooldownTimer -= Time.deltaTime;
        }
    }
}