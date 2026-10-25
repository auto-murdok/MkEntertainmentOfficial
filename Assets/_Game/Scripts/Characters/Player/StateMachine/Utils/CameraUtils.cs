using UnityEngine;

public class CameraUtils
{
    private const float MouseDeltaTimeMultiplier = 1.0f;

    private float _mouseLookThreshold = 0f;
    private float _controllerLookThreshold = 0.1f;
    private float _cinemachineTargetYaw;
    private float _cinemachineTargetPitch;
    private bool _seeded;

    // True once the accumulators have been seeded from a placed camera
    // target (see SeedFrom).
    public bool IsSeeded => _seeded;

    // Seed the yaw/pitch accumulators from the camera target's CURRENT world
    // rotation. Placement code calls this once the character has been placed
    // (spawn / rigging complete — CharacterLocomotion.SeedCamera, driven from
    // PlayerRigging) and again after any discontinuous move (respawn,
    // teleport, cutscene return). Until seeded, the accumulators sit at 0
    // and the first HandleCameraRotation write would snap the camera to
    // world-north/level whatever the character is actually facing — the
    // "camera starts wrong until first aim" bug (aim only masked it: the
    // Aim state rotates the CHARACTER to the camera's wrong yaw).
    //
    // Angles are read as eulerAngles (degrees) — never from quaternion
    // components (rotation.y is sin(yaw/2), not an angle). Pitch arrives in
    // 0..360, so it is normalised to -180..180 here, BEFORE the clamp in
    // HandleCameraRotation can see it: an authored -10 degrees reads back
    // as 350 and would otherwise snap to the top clamp on frame 1.
    public void SeedFrom(Transform cameraTarget)
    {
        if (cameraTarget == null)
        {
            return;
        }

        Vector3 euler = cameraTarget.rotation.eulerAngles;
        _cinemachineTargetYaw = euler.y;

        float pitch = euler.x;
        if (pitch > 180f) pitch -= 360f;
        _cinemachineTargetPitch = pitch;

        _seeded = true;
    }

    public void HandleCameraRotation(CharacterStateContext context, CinemachineContext cinemachineProps)
    {
        // Belt-and-braces: if no spawn path seeded us, seed from the target
        // as it stands now — before the write below can snap it to (0, 0).
        if (!_seeded)
        {
            SeedFrom(context.mainCameraTarget);
        }

        float lookThreshold = context.isCurrentDeviceMouse ? _mouseLookThreshold : _controllerLookThreshold;
        // Only rotate the camera when there is meaningful look input.
        if (context.lookInput.sqrMagnitude >= lookThreshold)
        {
            // Mouse input is already frame-rate independent, so do not scale it by deltaTime.
            float deltaTimeMultiplier = context.isCurrentDeviceMouse ? MouseDeltaTimeMultiplier : Time.deltaTime;

            _cinemachineTargetYaw += context.lookInput.x * deltaTimeMultiplier * cinemachineProps.lookSensivity;
            _cinemachineTargetPitch -= context.lookInput.y * deltaTimeMultiplier * cinemachineProps.lookSensivity;
        }

        // Yaw is left unbounded; pitch is clamped to the configured limits.
        _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
        _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, cinemachineProps.bottomClamp, cinemachineProps.topClamp);

        // Cinemachine follows this target transform.
        context.mainCameraTarget.rotation = Quaternion.Euler(_cinemachineTargetPitch, _cinemachineTargetYaw, 0.0f);
    }

    private static float ClampAngle(float angle, float min, float max)
    {
        // Wrap with while loops: a single if only corrects one 360-degree step,
        // which breaks after fast multi-rotation yaw accumulation.
        while (angle < -360f) angle += 360f;
        while (angle > 360f) angle -= 360f;
        return Mathf.Clamp(angle, min, max);
    }
}
