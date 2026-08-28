using UnityEngine;

public class AIDetectionUtils
{
    public static EGeneric DetectViaLineOfSight<EGeneric>(Transform transform, float detectionRadius, LayerMask detectionLayer, LayerMask obstacleLayer, int minDetectionAngle, int maxDetectionAngle)
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRadius, detectionLayer);
        EGeneric survivor = default(EGeneric);

        foreach (Collider collider in colliders)
        {
            EGeneric possibleMatch = collider.GetComponentInParent<EGeneric>();
            if (possibleMatch != null)
            {
                if (IsInLineOfSight(transform, collider.transform.position, minDetectionAngle, maxDetectionAngle))
                {
                    if (IsNotBlockedByObstacles(transform.position, collider.transform.position, obstacleLayer))
                    {
                        // Debug.DrawLine(transform.position, collider.transform.position, Color.yellow, detectionRadius);
                        survivor = possibleMatch;
                        break;
                    }
                }
            }
        }

        return survivor;
    }

    public static bool IsNotBlockedByObstacles(Vector3 origin, Vector3 destination, LayerMask obstacleLayer)
    {
        return !Physics.Linecast(origin, destination, obstacleLayer);
    }

    public static bool IsInLineOfSight(Transform origin, Vector3 destination, int minDetectionAngle, int maxDetectionAngle)
    {
        Vector3 playerDirection = (origin.position - destination).normalized;
        float viewableAngle = Vector3.Angle(origin.forward, playerDirection);
        return viewableAngle > minDetectionAngle && viewableAngle < maxDetectionAngle;
    }
}