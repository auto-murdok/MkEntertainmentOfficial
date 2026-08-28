using UnityEngine;

public class ZombieIdle : State<ZombieStates, ZombieContext>
{
    public void CheckTransitions(StateMachine<ZombieStates, ZombieContext> character)
    {
        if (character._context.isBitting)
        {
            character.ChangeState(ZombieStates.Bitting);
        }
        else if (character._context.target != null)
        {
            float biteRange = character._context.data != null ? character._context.data.biteRange : ZombieBehavior.DefaultBiteRange;
            float distance = Vector3.Distance(character.transform.position, character._context.target.TargetPosition);

            if (distance > biteRange)
            {
                character.ChangeState(ZombieStates.Chasing);
            }
            else if (character._context.attackCooldownTimer <= 0f && !character._context.isBitting)
            {
                // Target is within bite range and attack cooldown has elapsed: re-engage bite!
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