// Second phase of the zombie bite: the actual bite / push-off. The zombie stops
// reporting "preparing" so the victim is released and the pushback animation plays.
// The Animator bridge (ZombieBiteBehaviour) ends the whole bite by clearing isBitting.
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
