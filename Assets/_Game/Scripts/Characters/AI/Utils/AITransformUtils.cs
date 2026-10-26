using UnityEngine;
using UnityEngine.AI;

public class AITransformUtils
{
    private const float MaxRotationDegreesPerSecond = 180f;
    // Measured-velocity smoothing window: long enough to hide per-frame
    // root-motion judder, short enough to react within a step.
    private const float VelocitySmoothingSeconds = 0.1f;
    // A measurement older than this belongs to a different movement
    // episode (state re-entry, teleport): restart from rest instead of
    // dividing a long gap's displacement into a phantom velocity.
    private const float MeasurementStaleSeconds = 0.25f;
    // Below this the body counts as stationary and the blend direction
    // is fed from steering instead, so the first step can begin.
    private const float StationarySpeed = 0.05f;

    // Computes the local-space blend input for a chasing zombie from its
    // MEASURED velocity — never from the NavMeshAgent, whose speed is 0
    // in this project (root motion is the only mover), so agent.velocity
    // and agent.desiredVelocity read zero for the entire chase. Feeding
    // the blend from steering intent alone (the old behaviour) made
    // held-back zombies stride in place and let the input saturate far
    // past the walk clip's home position in the blend tree.
    public static Vector2 GetAIMovementBlend(Transform transform, NavMeshAgent agent, ZombieContext context, float targetSpeed)
    {
        UpdateMeasuredVelocity(transform, context);

        if (!agent.hasPath)
        {
            return Vector2.zero;
        }

        Vector3 steeringDirection = agent.steeringTarget - transform.position;
        steeringDirection.y = 0f;
        if (steeringDirection.sqrMagnitude <= 0.0001f)
        {
            return Vector2.zero;
        }
        steeringDirection.Normalize();

        Quaternion targetRotation = Quaternion.LookRotation(steeringDirection);
        float maxDegreesDelta = MaxRotationDegreesPerSecond * Time.deltaTime;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, maxDegreesDelta);

        float measuredSpeed = context.measuredVelocity.magnitude;
        Vector3 strideDirection = measuredSpeed >= StationarySpeed
            ? context.measuredVelocity / measuredSpeed
            : steeringDirection;

        float magnitude = ZombieAnimatorUtils.ComputeBlendMagnitude(
            measuredSpeed, targetSpeed, Time.time - context.measuredEpisodeStart);

        // Facing falloff replaces the old binary gate (dot > 0.5 feeds,
        // otherwise zero): the stride fades smoothly as the heading error
        // grows, so a turning zombie turns mostly in place instead of
        // gliding at full stride, and nothing snaps off at a threshold.
        magnitude *= Mathf.Clamp01(Vector3.Dot(transform.forward, steeringDirection));

        Vector3 local = transform.InverseTransformDirection(strideDirection);

        if (HasReachedTarget(transform, agent))
        {
            agent.ResetPath();
        }

        return new Vector2(local.x, local.z) * magnitude;
    }

    private static void UpdateMeasuredVelocity(Transform transform, ZombieContext context)
    {
        float now = Time.time;
        Vector3 position = transform.position;

        if (!context.hasMeasuredPosition || now - context.measuredTimestamp > MeasurementStaleSeconds)
        {
            context.measuredPosition = position;
            context.measuredVelocity = Vector3.zero;
            context.measuredTimestamp = now;
            context.measuredEpisodeStart = now;
            context.hasMeasuredPosition = true;
            return;
        }

        float deltaTime = now - context.measuredTimestamp;
        if (deltaTime <= 0f)
        {
            return;
        }

        Vector3 rawVelocity = (position - context.measuredPosition) / deltaTime;
        rawVelocity.y = 0f;
        float blend = Mathf.Clamp01(deltaTime / VelocitySmoothingSeconds);
        context.measuredVelocity = Vector3.Lerp(context.measuredVelocity, rawVelocity, blend);
        context.measuredPosition = position;
        context.measuredTimestamp = now;
    }

    public static bool HasReachedTarget(Transform transform, NavMeshAgent agent)
    {
        float sqrDistance = (transform.position - agent.destination).sqrMagnitude;
        return sqrDistance < (agent.radius * agent.radius);
    }
}
