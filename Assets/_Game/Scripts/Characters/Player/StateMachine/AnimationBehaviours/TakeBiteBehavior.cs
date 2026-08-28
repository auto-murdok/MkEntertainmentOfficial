using UnityEngine;

// DEPRECATED: The take-bite lifecycle is now owned entirely by the C# FSM
// (CharacterTakeBiteState ends it via its takeBiteDuration timer). This
// StateMachineBehaviour is kept only so the animator controller reference stays
// valid; it no longer drives state. Use the Editor menu
// "Cleanup > Remove Bite Bridges" to detach and delete it.
public class TakeBiteBehavior : StateMachineBehaviour
{
}
