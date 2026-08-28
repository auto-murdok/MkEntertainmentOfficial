using System;
using UnityEngine;
using UnityEngine.AI;

public class AITransformUtils
{
    private const float FacingDotThreshold = 0.5f;
    private const float MaxRotationDegreesPerSecond = 180f;

    // Computes the local-space horizontal/vertical movement to feed the animator
    // so the zombie turns toward and walks along its steering target.
    public static Vector2 GetAIMovementThreshold(Transform transform, NavMeshAgent agent, Animator animator)
    {
        Vector2 movementThreshold = Vector2.zero;

        if (agent.hasPath)
        {
            Vector3 globalDirection = (agent.steeringTarget - transform.position).normalized;
            Vector3 localDirection = transform.InverseTransformDirection(globalDirection);
            bool isFacingMoveDirection = Vector3.Dot(globalDirection, transform.forward) > FacingDotThreshold;

            if (globalDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(globalDirection);
                float maxDegreesDelta = MaxRotationDegreesPerSecond * Time.deltaTime;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, maxDegreesDelta);
            }

            float horizontalValue = isFacingMoveDirection ? localDirection.x : 0f;
            float verticalValue = isFacingMoveDirection ? localDirection.z : 0f;

            movementThreshold.x = horizontalValue;
            movementThreshold.y = verticalValue;

            if (HasReachedTarget(transform, agent))
            {
                agent.ResetPath();
            }
        }

        return movementThreshold;
    }

    public static bool HasReachedTarget(Transform transform, NavMeshAgent agent)
    {
        float sqrDistance = (transform.position - agent.destination).sqrMagnitude;
        return sqrDistance < (agent.radius * agent.radius);
    }
}
