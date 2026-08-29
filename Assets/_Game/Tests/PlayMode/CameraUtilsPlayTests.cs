using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public class CameraUtilsPlayTests
    {
        // Controller look scales input by Time.deltaTime, which is 0 in
        // EditMode tests — the above-threshold rotation assertion only holds
        // in PlayMode where a real frame gives deltaTime > 0 (see
        // docs/testing.md, EditMode vs PlayMode conventions).

        [UnityTest]
        public IEnumerator HandleCameraRotation_ControllerAboveThreshold_Rotates()
        {
            GameObject host = new GameObject("CameraTarget");
            CharacterStateContext context = new CharacterStateContext
            {
                mainCameraTarget = host.transform,
                isCurrentDeviceMouse = false
            };
            CinemachineContext cine = new CinemachineContext
            {
                topClamp = 60f,
                bottomClamp = -70f,
                lookSensivity = 1f
            };
            var utils = new CameraUtils();

            context.lookInput = new Vector2(1f, 0f);
            yield return null;

            utils.HandleCameraRotation(context, cine);
            float yaw = host.transform.eulerAngles.y;

            Object.Destroy(host);

            Assert.Greater(yaw, 0f, "Controller look above threshold must rotate the camera target.");
        }
    }
}
