using UnityEngine;

public class TakeBiteBehavior : StateMachineBehaviour
{
    // Called when the Take Bite state finishes evaluating, handing control back to the survivor.
    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (animator == null) return;
        ISurvivor survivor = animator.GetComponent<ISurvivor>();
        survivor?.RecoverControl();
    }
}
