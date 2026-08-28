using UnityEngine;

// Single source of truth for the player's flag-derived state. All locomotion and
// action states (Idle, Walking, Sprinting, Aiming, Reloading) are pure functions
// of the context flags, so their transition logic is centralized here instead of
// being copy-pasted into every state's CheckTransitions (which had drifted out of
// sync, e.g. Sprinting's extra !isRunning branch). TakeBite/Dead keep their own
// transitions because they are not pure flag-derivations.
public static class CharacterStateResolver
{
    public static CharacterState? Resolve(CharacterStateContext context)
    {
        if (context.isBeingAttacked) return CharacterState.TakingBite;
        if (context.isReloading)     return CharacterState.Reloading;
        if (context.isAiming)        return CharacterState.Aiming;
        if (context.movementInput != Vector2.zero)
            return context.isRunning ? CharacterState.Sprinting : CharacterState.Walking;
        return CharacterState.Idle;
    }
}
