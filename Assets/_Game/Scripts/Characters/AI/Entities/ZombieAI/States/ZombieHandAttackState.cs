using UnityEngine;

// Secondary zombie attack for a victim that is already pinned by another
// attacker's bite (see ZombieBehavior.TryTriggerAttack). A free-standing
// right-hand swing: no pinning, no agent radius shrink, no isPreparing and no
// victim lock. Like the bite, the C# FSM timer owns the lifecycle
// (ZombieBitingState pattern) and damage is attacker-driven. The hit is scored
// exactly once inside a short window (same one-damage-per-action guard as the
// pooled bullets' _hasHit).
public class ZombieHandAttackState : State<ZombieStates, ZombieContext>
{
    // TUNING: normalized point in the swing (0..1) where the hit is judged.
    private const float HitFraction = 0.4f;
    // TUNING: delay before this zombie may attack again after a swing.
    private const float HandAttackCooldown = 1.5f;

    private const float DefaultHandAttackDamage = 15f;
    private const float DefaultHandAttackDuration = 1.2f;
    private const float DefaultHandAttackRange = 1.6f;

    private ZombieData _data;
    private float _duration;
    private float _hitTimer;
    private bool _hasHit;

    public void EnterState(StateMachine<ZombieStates, ZombieContext> character)
    {
        ZombieContext context = character._context;
        _data = context.data;

        float configured = _data != null ? _data.handAttackDuration : DefaultHandAttackDuration;
        _duration = configured > 0f ? configured : DefaultHandAttackDuration;

        _hitTimer = _duration * HitFraction;
        _hasHit = false;

        if (context.agent != null && context.agent.isOnNavMesh)
        {
            context.agent.ResetPath();
        }

        if (context.animator != null)
        {
            context.animator.SetTrigger(AnimatorUtils.RHandAttackHash);
        }

        LookAtVictim(character, context);
    }

    public void UpdateState(StateMachine<ZombieStates, ZombieContext> character)
    {
        ZombieContext context = character._context;

        if (!_hasHit)
        {
            _hitTimer -= Time.deltaTime;
            if (_hitTimer <= 0f)
            {
                // One damage event per swing regardless of outcome (out of range
                // or dead victim still burns the hit).
                _hasHit = true;
                ApplyDamage(character, context);
            }
        }

        _duration -= Time.deltaTime;
        if (_duration <= 0f && context.isHandAttacking)
        {
            context.isHandAttacking = false;
        }
    }

    public void CheckTransitions(StateMachine<ZombieStates, ZombieContext> character)
    {
        if (!character._context.isHandAttacking)
        {
            character.ChangeState(ZombieStates.Idle);
        }
    }

    public void ExitState(StateMachine<ZombieStates, ZombieContext> character)
    {
        ZombieContext context = character._context;
        context.attackCooldownTimer = HandAttackCooldown;
        context.interactable = null;
        context.isHandAttacking = false;
    }

    private void ApplyDamage(StateMachine<ZombieStates, ZombieContext> character, ZombieContext context)
    {
        IInteractable victim = ResolveVictim(context);
        if (victim is not IDamageable damageable)
        {
            return;
        }

        float range = _data != null ? _data.handAttackRange : DefaultHandAttackRange;
        float sqrDistance = (character.transform.position - victim.position).sqrMagnitude;
        if (sqrDistance > range * range)
        {
            return;
        }

        float damage = _data != null ? _data.handAttackDamage : DefaultHandAttackDamage;
        using (CombatLog.BeginSource("ZombieHandAttack"))
        {
            damageable.TakeDamage(damage);
        }
    }

    private void LookAtVictim(StateMachine<ZombieStates, ZombieContext> character, ZombieContext context)
    {
        IInteractable victim = ResolveVictim(context);
        if (victim == null)
        {
            return;
        }

        Vector3 direction = victim.position - character.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            character.transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    // The interactable is captured on attack start (survivor scans may clear
    // the live target mid-swing); the target is the fallback.
    private static IInteractable ResolveVictim(ZombieContext context)
    {
        return context.interactable != null ? context.interactable : context.target as IInteractable;
    }
}
