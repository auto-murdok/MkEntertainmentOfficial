using UnityEngine;

public class AIDetectionUtils
{
    // Casts a detection sphere and returns the first matching component that is
    // within the field-of-view cone and not blocked by an obstacle.
    public static TComponent DetectViaLineOfSight<TComponent>(Transform transform, float detectionRadius, LayerMask detectionLayer, LayerMask obstacleLayer, int minDetectionAngle, int maxDetectionAngle)
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRadius, detectionLayer);
        TComponent detectedTarget = default(TComponent);

        foreach (Collider collider in colliders)
        {
            TComponent possibleMatch = collider.GetComponentInParent<TComponent>();
            if (possibleMatch != null)
            {
                if (IsInLineOfSight(transform, collider.transform.position, minDetectionAngle, maxDetectionAngle))
                {
                    if (IsNotBlockedByObstacles(transform.position, collider.transform.position, obstacleLayer))
                    {
                        // Debug.DrawLine(transform.position, collider.transform.position, Color.yellow, detectionRadius);
                        detectedTarget = possibleMatch;
                        break;
                    }
                }
            }
        }

        return detectedTarget;
    }

    public static bool IsNotBlockedByObstacles(Vector3 origin, Vector3 destination, LayerMask obstacleLayer)
    {
        return !Physics.Linecast(origin, destination, obstacleLayer);
    }

    // Checks whether the destination is inside the view cone defined by the
    // minimum and maximum angles relative to the transform's forward vector.
    public static bool IsInLineOfSight(Transform origin, Vector3 destination, int minDetectionAngle, int maxDetectionAngle)
    {
        Vector3 directionToDestination = (origin.position - destination).normalized;
        float viewableAngle = Vector3.Angle(origin.forward, directionToDestination);
        return viewableAngle > minDetectionAngle && viewableAngle < maxDetectionAngle;
    }
}
