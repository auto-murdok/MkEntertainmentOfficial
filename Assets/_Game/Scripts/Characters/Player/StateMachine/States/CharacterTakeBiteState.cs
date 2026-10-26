using System;
using UnityEngine;

public class CharacterTakeBiteState : State<CharacterState, CharacterStateContext>
{
    private const float AttackedAgentRadius = 0.1f;

    // TUNING: distance (metres) the victim is pulled from the victimHook socket toward
    // the zombie's body. The socket sits ~0.5m in front of the zombie; this closes that
    // gap so the victim reads as "in contact" during the bite (previously closed by the
    // zombie's root-motion lunge, now suppressed by pinning). Raise for a tighter hug,
    // lower for more space. ~0.35 leaves the victim ~0.15m off the body.
    private const float PlayerBitePullInDistance = 0.35f;

    private float _biteTimer;
    // Latched once the attacker's grab phase has actually been observed, so
    // the asymmetric release below can never fire on the entry frames before
    // the (possibly network-mirrored) attacker flags have caught up.
    private bool _sawGrab;
    // The player prefab's actual NavMeshAgent radius, captured on entry so the
    // exit restores the real value instead of a hardcoded default.
    private float _originalAgentRadius;
    private bool _agentRadiusCaptured;

    public void CheckTransitions(StateMachine<CharacterState, CharacterStateContext> stateMachine)
    {
        if (!stateMachine._context.isBeingAttacked)
        {
            stateMachine.ChangeState(CharacterState.Idle);
        }
    }

    public void EnterState(StateMachine<CharacterState, CharacterStateContext> stateMachine)
    {
        CharacterStateContext context = stateMachine._context;

        // The ScriptableObject is the only source of truth for the bite duration:
        // fail loudly instead of falling back to a script-side default.
        if (context.data == null)
        {
            Debug.LogError($"[{stateMachine.name}] PlayerData is not assigned on CharacterLocomotion. " +
                           "Assign a PlayerData asset so the take-bite duration can be resolved.");
            return;
        }

        if (context.agent != null)
        {
            _originalAgentRadius = context.agent.radius;
            _agentRadiusCaptured = true;
            context.agent.radius = AttackedAgentRadius;
            context.agent.ResetPath();
        }

        _biteTimer = context.data.takeBiteDuration;
        _sawGrab = false;
    }

    public void ExitState(StateMachine<CharacterState, CharacterStateContext> stateMachine)
    {
        if (_agentRadiusCaptured && stateMachine._context.agent != null)
        {
            stateMachine._context.agent.radius = _originalAgentRadius;
            _agentRadiusCaptured = false;
        }
    }

    public void UpdateState(StateMachine<CharacterState, CharacterStateContext> stateMachine)
    {
        // If the attacker is gone (e.g. killed mid-bite), bail out safely.
        if (stateMachine._context.attacker == null)
        {
            stateMachine.ChangeState(CharacterState.Idle);
            return;
        }

        // Only lock position and rotation while the attacker is in the initial grab/prepare phase.
        // Once isPreparing is false (bite & push-off phase), release the transform to let the
        // pushback animation/root motion execute naturally.
        if (stateMachine._context.attacker.isPreparing)
        {
            _sawGrab = true;
            // Snap to the victimHook, then pull the victim toward the zombie body to remove the
            // ~0.5m socket gap (see PlayerBitePullInDistance).
            Vector3 lockedPos = stateMachine._context.attacker.victimHook.position;
            lockedPos = Vector3.MoveTowards(lockedPos, stateMachine._context.attacker.position, PlayerBitePullInDistance);
            stateMachine.transform.position = lockedPos;
            stateMachine.transform.rotation = stateMachine._context.attacker.victimHook.rotation;
        }

        // Asymmetric release: once the grab has happened and the attacker's
        // bite has fully ended (push-off complete), the victim breaks free —
        // while the zombie is still in its recovery / attack cooldown, she is
        // already moving again. The duration timer below stays as a safety cap
        // for a stuck or destroyed attacker whose flags never clear.
        if (_sawGrab
            && !stateMachine._context.attacker.isPreparing
            && !stateMachine._context.attacker.isBiting)
        {
            stateMachine._context.isBeingAttacked = false;
            return;
        }

        // End the take-bite from the C# side once the duration elapses.
        _biteTimer -= Time.deltaTime;
        if (_biteTimer <= 0f)
        {
            stateMachine._context.isBeingAttacked = false;
        }
    }
}
