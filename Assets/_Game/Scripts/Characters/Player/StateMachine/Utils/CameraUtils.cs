using UnityEngine;

public class CameraUtils
{
    private const float MouseDeltaTimeMultiplier = 1.0f;

    private float _mouseLookThreshold = 0f;
    private float _controllerLookThreshold = 0.1f;
    private float _cinemachineTargetYaw;
    private float _cinemachineTargetPitch;

    public void HandleCameraRotation(CharacterStateContext context, CinemachineContext cinemachineProps)
    {
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
        if (angle < -360f) angle += 360f;
        if (angle > 360f) angle -= 360f;
        return Mathf.Clamp(angle, min, max);
    }
}
