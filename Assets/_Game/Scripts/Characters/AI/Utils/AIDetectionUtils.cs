using UnityEngine;

public class AIDetectionUtils
{
    // Full forward vision-cone angle in degrees (half of it on each side of
    // forward). 120 = a 60-degree half cone, matching the Walker/Runner data.
    public const float DefaultFieldOfViewAngle = 120f;

    private const int MaxDetectionColliders = 32;
    private static readonly Collider[] DetectionColliderBuffer = new Collider[MaxDetectionColliders];

    // Casts a detection sphere and returns the NEAREST matching component that
    // is inside the forward field-of-view cone and not blocked by an obstacle.
    public static TComponent DetectViaLineOfSight<TComponent>(
        Transform originTransform,
        float detectionRadius,
        LayerMask detectionLayer,
        LayerMask obstacleLayer,
        float fieldOfViewAngle)
    {
        if (originTransform == null) return default;

        int colliderCount = Physics.OverlapSphereNonAlloc(originTransform.position, detectionRadius, DetectionColliderBuffer, detectionLayer);
        TComponent detectedTarget = default;
        float nearestSqrDistance = float.MaxValue;

        for (int i = 0; i < colliderCount; i++)
        {
            Collider collider = DetectionColliderBuffer[i];
            if (collider == null) continue;

            TComponent possibleMatch = collider.GetComponentInParent<TComponent>();
            if (possibleMatch != null)
            {
                Vector3 targetPosition = collider.bounds.center;
                float sqrDistance = (targetPosition - originTransform.position).sqrMagnitude;

                // Candidates farther than the current best cannot win — skip
                // their cone/LOS casts entirely.
                if (sqrDistance >= nearestSqrDistance) continue;

                if (IsInLineOfSight(originTransform, targetPosition, fieldOfViewAngle)
                    && IsNotBlockedByObstacles(originTransform.position, targetPosition, obstacleLayer))
                {
                    detectedTarget = possibleMatch;
                    nearestSqrDistance = sqrDistance;
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

    // Checks whether the destination is inside the forward view cone of the
    // zombie. fieldOfViewAngle is the FULL cone angle in degrees; invalid
    // values (<= 0 or >= 360) fall back to the default cone.
    public static bool IsInLineOfSight(Transform origin, Vector3 destination, float fieldOfViewAngle)
    {
        if (fieldOfViewAngle <= 0f || fieldOfViewAngle >= 360f)
        {
            fieldOfViewAngle = DefaultFieldOfViewAngle;
        }

        Vector3 directionToTarget = (destination - origin.position).normalized;
        float angleToTarget = Vector3.Angle(origin.forward, directionToTarget);
        return angleToTarget <= fieldOfViewAngle * 0.5f;
    }
}
