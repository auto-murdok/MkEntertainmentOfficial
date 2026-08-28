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

    public static void HandleWalkingMovement(CharacterStateContext context)
    {
        if (context.movementInput != Vector2.zero)
        {
            AnimatorUtils.SetMovementRootMotion(context.animator, context.movementInput, MovementSmoothSpeed);
        }
    }

    public static void HandleSprintingMovement(CharacterStateContext context)
    {
        if (context.movementInput != Vector2.zero)
        {
            Vector2 sprintInput = context.movementInput * RunInputMultiplier;
            AnimatorUtils.SetMovementRootMotion(context.animator, sprintInput, MovementSmoothSpeed);
        }
    }

    public static void HandleCharacterMovement(CharacterStateContext context)
    {
        HandleWalkingMovement(context);
    }
}
