// Second phase of the zombie bite: the actual bite / push-off. The zombie stops
// reporting "preparing" so the victim is released and the pushback animation plays.
// The whole bite (including clearing isBiting) is now ended by the C# FSM timer in
// ZombieBitingState, not the Animator bridge.
public class BiteReleaseState : State<ZombieStates, ZombieContext>
{
    public void EnterState(StateMachine<ZombieStates, ZombieContext> character)
    {
        character._context.isPreparing = false;
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
