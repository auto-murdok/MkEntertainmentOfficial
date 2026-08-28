

using UnityEngine;

public class CameraUtils
{
    private float _mouseLookThreshold = 0f;
    private float _controllerLookThreshold = 0.1f;
    private float _cinemachineTargetYaw;
    private float _cinemachineTargetPitch;

    public void HandleCameraRotation(CharacterStateContext context, CinemachineContext cinemachineProps)
    {
        float lookThreshold = context.isCurrentDeviceMouse ? _mouseLookThreshold : _controllerLookThreshold;
        // if there is an input and camera position is not fixed
        if (context.lookInput.sqrMagnitude >= lookThreshold)
        {
            //Don't multiply mouse input by Time.deltaTime;
            float deltaTimeMultiplier = context.isCurrentDeviceMouse ? 1.0f : Time.deltaTime;

            _cinemachineTargetYaw += context.lookInput.x * deltaTimeMultiplier * cinemachineProps.lookSensivity;
            _cinemachineTargetPitch -= context.lookInput.y * deltaTimeMultiplier * cinemachineProps.lookSensivity;
        }

        // clamp our rotations so our values are limited 360 degrees
        _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
        _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, cinemachineProps.bottomClamp, cinemachineProps.topClamp);

        // _currentRotation = Vector3.Lerp(_targetRotation, Vector3.zero, 5f * Time.deltaTime);
        // _targetRotation = Vector3.Slerp(_currentRotation, _targetRotation, 5f * Time.deltaTime);

        // Cinemachine will follow this target
        context.mainCameraTarget.rotation = Quaternion.Euler(_cinemachineTargetPitch,
           _cinemachineTargetYaw, 0.0f);
    }

    private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
    {
        if (lfAngle < -360f) lfAngle += 360f;
        if (lfAngle > 360f) lfAngle -= 360f;
        return Mathf.Clamp(lfAngle, lfMin, lfMax);
    }
}
