using UnityEngine;

// Reusable "move toward a commanded world position" state. Any FSM whose context
// derives from ActorBlackboard can register this to gain click-to-move / patrol
// style control via context.moveDestination. Kept fully self-contained (no
// dependency on entity-specific animator utilities) so the Core assembly stays
// reusable. Avoids per-frame allocations: the destination is only re-issued on a
// fixed interval, not every frame.
public class ActorMoveToTargetState<TKey, TContext> : State<TKey, TContext>
    where TContext : ActorBlackboard, new()
{
    private const float DestinationUpdateInterval = 0.15f;
    private const float TurnSpeedDegreesPerSecond = 180f;
    private const float RootMotionSmoothing = 0.5f;

    private static readonly int HorizontalHash = AnimatorUtils.HorizontalHash;
    private static readonly int VerticalHash = AnimatorUtils.VerticalHash;

    private float _updateTimer;

    public void EnterState(StateMachine<TKey, TContext> character)
    {
        _updateTimer = 0f;
    }

    public void UpdateState(StateMachine<TKey, TContext> character)
    {
        TContext context = character._context;
        if (context.agent == null || !context.moveDestination.HasValue)
        {
            return;
        }

        if (context.agent.isActiveAndEnabled && context.agent.isOnNavMesh)
        {
            _updateTimer -= Time.deltaTime;
            if (_updateTimer <= 0f)
            {
                _updateTimer = DestinationUpdateInterval;
                context.agent.SetDestination(context.moveDestination.Value);
            }

            Vector3 direction = (context.agent.steeringTarget - character.transform.position).normalized;
            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                character.transform.rotation = Quaternion.RotateTowards(
                    character.transform.rotation, targetRotation, TurnSpeedDegreesPerSecond * Time.deltaTime);

                Vector3 local = character.transform.InverseTransformDirection(direction);
                if (context.animator != null)
                {
                    context.animator.SetFloat(HorizontalHash, local.x, RootMotionSmoothing, Time.deltaTime);
                    context.animator.SetFloat(VerticalHash, local.z, RootMotionSmoothing, Time.deltaTime);
                }
            }
        }
    }

    public virtual void CheckTransitions(StateMachine<TKey, TContext> character)
    {
    }

    public void ExitState(StateMachine<TKey, TContext> character)
    {
        character._context.agent?.ResetPath();
    }
}
