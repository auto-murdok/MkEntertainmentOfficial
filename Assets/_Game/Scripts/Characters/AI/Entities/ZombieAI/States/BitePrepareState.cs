// First phase of the zombie bite: the lunge / grab. While active the zombie is
// pinned to its initial grab pose (snapping handled by the parent ZombieBitingState)
// and signals "preparing" so the victim (player) stays locked to the attacker hook.
public class BitePrepareState : State<ZombieStates, ZombieContext>
{
    public void EnterState(StateMachine<ZombieStates, ZombieContext> character)
    {
        character._context.isPreparing = true;
    }

    public void UpdateState(StateMachine<ZombieStates, ZombieContext> character)
    {
    }

    public void ExitState(StateMachine<ZombieStates, ZombieContext> character)
    {
    }

    public void CheckTransitions(StateMachine<ZombieStates, ZombieContext> character)
    {
    }
}
