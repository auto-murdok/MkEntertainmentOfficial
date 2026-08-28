

using UnityEngine;

public class CharacterTransformUtils
{
    public static void HandleCharacterRotation(Transform characterTransform, CharacterStateContext context)
    {
        Transform mainCamera = context.mainCameraTarget;
        float maxDegreesDelta = 270 * Time.deltaTime;
        characterTransform.rotation = Quaternion.RotateTowards(characterTransform.rotation, mainCamera.rotation, maxDegreesDelta);
    }

    public static void HandleCharacterMovement(CharacterStateContext context)
    {
        Vector2 movementInput = context.movementInput;
        bool shouldRun = context.isRunning && !context.isAiming;

        if (movementInput != Vector2.zero)
        {
            // duplicate value if running and not aiming
            movementInput = shouldRun ? movementInput * 2 : movementInput;

            // setting animator values
            AnimatorUtils.SetMovementRootMotion(context.animator, movementInput, 0.25f);
        }
    }
}
