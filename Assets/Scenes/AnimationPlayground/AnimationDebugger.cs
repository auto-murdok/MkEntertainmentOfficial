using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimationDebugger : MonoBehaviour
{
    private Animator animator;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        float animationTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
        if (animationTime > 0)
        {
            Debug.LogWarning($"{gameObject.name} Time: {animationTime}");
            Debug.LogWarning($"{gameObject.name} Position: {transform.position}");
        }
    }
}
