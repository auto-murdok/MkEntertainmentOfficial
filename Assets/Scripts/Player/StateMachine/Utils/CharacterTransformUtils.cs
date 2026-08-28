using UnityEngine;

public class CharacterTransformUtils
{
    private const float MaxRotationDegreesPerSecond = 270f;
    private const float RunInputMultiplier = 2f;
    private const float MovementSmoothSpeed = 0.25f;

    public static void HandleCharacterRotation(Transform characterTransform, CharacterStateContext context)
    {
        Transform mainCameraTarget = context.mainCameraTarget;
        float maxDegreesDelta = MaxRotationDegreesPerSecond * Time.deltaTime;
        characterTransform.rotation = Quaternion.RotateTowards(characterTransform.rotation, mainCameraTarget.rotation, maxDegreesDelta);
    }

    public static void HandleCharacterMovement(CharacterStateContext context)
    {
        Vector2 movementInput = context.movementInput;
        bool shouldRun = context.isRunning && !context.isAiming;

        if (movementInput != Vector2.zero)
        {
            // Running (while not aiming) doubles the movement input fed to the animator.
            movementInput = shouldRun ? movementInput * RunInputMultiplier : movementInput;

            AnimatorUtils.SetMovementRootMotion(context.animator, movementInput, MovementSmoothSpeed);
        }
    }
}
