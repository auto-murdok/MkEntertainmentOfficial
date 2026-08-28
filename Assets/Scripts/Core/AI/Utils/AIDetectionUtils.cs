using UnityEngine;

public class AIDetectionUtils
{
    private const float DefaultProximityRadius = 2.5f;

    // Casts a detection sphere and returns the first matching component that is
    // within the field-of-view cone or proximity radius and not blocked by an obstacle.
    public static TComponent DetectViaLineOfSight<TComponent>(
        Transform originTransform,
        float detectionRadius,
        LayerMask detectionLayer,
        LayerMask obstacleLayer,
        int minDetectionAngle,
        int maxDetectionAngle,
        float proximityRadius = DefaultProximityRadius)
    {
        if (originTransform == null) return default;

        Collider[] colliders = Physics.OverlapSphere(originTransform.position, detectionRadius, detectionLayer);
        TComponent detectedTarget = default;

        foreach (Collider collider in colliders)
        {
            TComponent possibleMatch = collider.GetComponentInParent<TComponent>();
            if (possibleMatch != null)
            {
                Vector3 targetPosition = collider.bounds.center;
                float distance = Vector3.Distance(originTransform.position, targetPosition);

                // Target is detected if within proximity hearing range OR inside the forward vision cone
                bool inDetectionZone = distance <= proximityRadius || IsInLineOfSight(originTransform, targetPosition, minDetectionAngle, maxDetectionAngle);

                if (inDetectionZone)
                {
                    if (IsNotBlockedByObstacles(originTransform.position, targetPosition, obstacleLayer))
                    {
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
        if (obstacleLayer.value == 0) return true;
        return !Physics.Linecast(origin, destination, obstacleLayer);
    }

    // Checks whether the destination is inside the view cone defined by the
    // minimum and maximum angles relative to the origin's forward vector.
    public static bool IsInLineOfSight(Transform origin, Vector3 destination, int minDetectionAngle, int maxDetectionAngle)
    {
        Vector3 directionToDestination = (origin.position - destination).normalized;
        float viewableAngle = Vector3.Angle(origin.forward, directionToDestination);
        return viewableAngle > minDetectionAngle && viewableAngle < maxDetectionAngle;
    }
}
