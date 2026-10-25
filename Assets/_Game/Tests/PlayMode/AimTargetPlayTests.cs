using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public class AimTargetPlayTests
    {
        // The marker must sit exactly on THIS frame's center-screen ray hit.
        // The pre-fix implementation lerped the marker toward the hit at a
        // fixed damp rate, so both the visible marker and every consumer of
        // it (shots, aim IK) lagged behind the crosshair while aiming.
        [UnityTest]
        public IEnumerator Update_MarkerSnapsToCenterRayHit_WithoutLag()
        {
            // Camera at the origin looking down +Z.
            GameObject cameraGO = new GameObject("AimTargetTestCamera");
            cameraGO.tag = "MainCamera";
            cameraGO.AddComponent<Camera>();

            // Wall 5 m ahead; its front face sits at z = 4.5.
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 0f, 5f);
            wall.transform.localScale = new Vector3(10f, 10f, 1f);

            GameObject markerGO = new GameObject("AimTarget");
            markerGO.transform.position = new Vector3(3f, 3f, 0f); // start far off the point
            AimTarget aimTarget = markerGO.AddComponent<AimTarget>();
            typeof(AimTarget)
                .GetField("_aimColliderMask", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(aimTarget, (LayerMask)(1 << wall.layer));

            // The objects were placed this frame; push their transforms
            // into the physics world explicitly and let a physics step and
            // an Update pass run before asserting.
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return null;

            Assert.AreEqual(4.5f, aimTarget.CurrentAimPoint.z, 0.05f,
                "Aim point must come from this frame's center-screen ray.");
            Assert.AreEqual(aimTarget.CurrentAimPoint, markerGO.transform.position,
                "Marker must sit exactly on the aim point — no smoothing lag.");

            Object.Destroy(cameraGO);
            Object.Destroy(wall);
            Object.Destroy(markerGO);
        }
    }
}
