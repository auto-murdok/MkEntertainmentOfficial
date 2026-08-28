using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimationDebugger : MonoBehaviour
{
    private Animator _animator;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        // Log the current normalized playback position so animation timing can be inspected at runtime.
        float normalizedTime = _animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
        if (normalizedTime > 0)
        {
            Debug.LogWarning($"{gameObject.name} Time: {normalizedTime}");
            Debug.LogWarning($"{gameObject.name} Position: {transform.position}");
        }
    }
}
