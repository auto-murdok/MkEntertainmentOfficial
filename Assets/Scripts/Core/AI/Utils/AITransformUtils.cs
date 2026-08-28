using System;
using UnityEngine;
using UnityEngine.AI;

public class AITransformUtils
{
    public static Vector2 GetAIMovementThreshold(Transform transform, NavMeshAgent agent, Animator animator)
    {
        Vector2 movementThreshold = Vector2.zero;

        if (agent.hasPath)
        {
            Vector3 globalDirection = (agent.steeringTarget - transform.position).normalized;
            Vector3 localPosition = transform.InverseTransformDirection(globalDirection);
            bool isFacingMoveDirection = Vector3.Dot(globalDirection, transform.forward) > 0.5f;

            Quaternion globalRotation = Quaternion.LookRotation(globalDirection);
            float maxDegreesDelta = 180 * Time.deltaTime;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, globalRotation, maxDegreesDelta);

            float horizontalValue = isFacingMoveDirection ? localPosition.x : 0f;
            float verticalValue = isFacingMoveDirection ? localPosition.z : 0f;

            movementThreshold.x = horizontalValue;
            movementThreshold.y = verticalValue;

            if (HasReachedTarget(transform, agent))
            {
                agent.ResetPath();
            }
        }

        return movementThreshold;
    }

    public static bool HasReachedTarget(Transform transform, NavMeshAgent agent) {
        return Vector3.Distance(transform.position, agent.destination) < agent.radius;
    }
}