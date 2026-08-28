using UnityEngine;

public class AIDetectionUtils
{
    private const float DefaultMaxFovAngle = 60f; // 60 degrees half-angle = 120 degree vision cone

    // Casts a detection sphere and returns the first matching component that is
    // inside the forward field-of-view cone and not blocked by an obstacle.
    public static TComponent DetectViaLineOfSight<TComponent>(
        Transform originTransform,
        float detectionRadius,
        LayerMask detectionLayer,
        LayerMask obstacleLayer,
        int minDetectionAngle,
        int maxDetectionAngle)
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

                // Player must be strictly inside the forward vision cone of the zombie
                if (IsInLineOfSight(originTransform, targetPosition, minDetectionAngle, maxDetectionAngle))
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

    // Checks whether the destination is inside the forward view cone of the zombie.
    public static bool IsInLineOfSight(Transform origin, Vector3 destination, int minDetectionAngle, int maxDetectionAngle)
    {
        Vector3 directionToTarget = (destination - origin.position).normalized;
        float angleToTarget = Vector3.Angle(origin.forward, directionToTarget);

        float maxHalfAngle = (minDetectionAngle > 0 && maxDetectionAngle == 180) ? (180f - minDetectionAngle) : DefaultMaxFovAngle;
        if (maxHalfAngle <= 0f || maxHalfAngle > 180f) maxHalfAngle = DefaultMaxFovAngle;

        return angleToTarget <= maxHalfAngle;
    }
}
