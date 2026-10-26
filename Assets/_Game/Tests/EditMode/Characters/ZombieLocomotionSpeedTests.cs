using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Tests.EditMode
{
    // Speed matching (ZombieAnimatorUtils): the walk clip in the blend
    // tree — base or variant — must play at the rate that makes it cover
    // ground at the ZombieData pace, clamped to the window where a gait
    // still reads as the same gait.
    public class ZombieLocomotionSpeedTests
    {
        private ZombieData _data;

        [SetUp]
        public void SetUp()
        {
            // Class defaults are the Walker numbers: walk 1.5, chase 3.5.
            _data = ScriptableObject.CreateInstance<ZombieData>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        [Test]
        public void ComputeSpeedMul_CreepClip_WalkIsProportionalChaseClamps()
        {
            // Creep variant authors 1.03 m/s.
            Assert.AreEqual(1.5f / 1.03f, ZombieAnimatorUtils.ComputeSpeedMul(_data, false, 1.03f), 0.001f);
            Assert.AreEqual(ZombieAnimatorUtils.MaxSpeedMul, ZombieAnimatorUtils.ComputeSpeedMul(_data, true, 1.03f));
        }

        [Test]
        public void ComputeSpeedMul_BaseClip_ClampsToMax()
        {
            // The base zombie_walk authors only 0.31 m/s: both paces clamp.
            Assert.AreEqual(ZombieAnimatorUtils.MaxSpeedMul, ZombieAnimatorUtils.ComputeSpeedMul(_data, false, 0.31f));
            Assert.AreEqual(ZombieAnimatorUtils.MaxSpeedMul, ZombieAnimatorUtils.ComputeSpeedMul(_data, true, 0.31f));
        }

        [Test]
        public void ComputeSpeedMul_FastClip_ScalesDownToWindowFloor()
        {
            Assert.AreEqual(0.6f, ZombieAnimatorUtils.ComputeSpeedMul(_data, false, 2.5f), 0.001f);
            Assert.AreEqual(ZombieAnimatorUtils.MinSpeedMul, ZombieAnimatorUtils.ComputeSpeedMul(_data, false, 3.0f));
            Assert.AreEqual(3.5f / 3.0f, ZombieAnimatorUtils.ComputeSpeedMul(_data, true, 3.0f), 0.001f);
        }

        [Test]
        public void ComputeSpeedMul_MissingDataOrClip_IsNeutral()
        {
            Assert.AreEqual(1f, ZombieAnimatorUtils.ComputeSpeedMul(null, true, 1.03f));
            Assert.AreEqual(1f, ZombieAnimatorUtils.ComputeSpeedMul(_data, true, 0f));
        }

        [Test]
        public void ComputeBlendMagnitude_AtPace_IsHomeMagnitude()
        {
            Assert.AreEqual(ZombieAnimatorUtils.LocomotionHomeMagnitude,
                ZombieAnimatorUtils.ComputeBlendMagnitude(1.65f, 1.65f, 5f));
        }

        [Test]
        public void ComputeBlendMagnitude_NoTarget_IsZero()
        {
            Assert.AreEqual(0f, ZombieAnimatorUtils.ComputeBlendMagnitude(1f, 0f, 5f));
        }

        [Test]
        public void ComputeBlendMagnitude_Stalled_FreshEpisode_GetsStartupStride()
        {
            Assert.AreEqual(
                ZombieAnimatorUtils.LocomotionHomeMagnitude * ZombieAnimatorUtils.StartupStrideFraction,
                ZombieAnimatorUtils.ComputeBlendMagnitude(0f, 1.65f, 0f), 0.001f);
        }

        [Test]
        public void ComputeBlendMagnitude_Stalled_OldEpisode_KeepsOnlyTheStrainFloor()
        {
            Assert.AreEqual(
                ZombieAnimatorUtils.LocomotionHomeMagnitude * ZombieAnimatorUtils.StallFloorFraction,
                ZombieAnimatorUtils.ComputeBlendMagnitude(0f, 1.65f, 5f), 0.001f);
        }

        [Test]
        public void ComputeBlendMagnitude_HalfPace_GetsSqrtGain_NeverAboveHome()
        {
            // Sqrt gain: a body at half pace is fed ~71% stride, so the
            // loop converges instead of idling at half feed forever.
            Assert.AreEqual(ZombieAnimatorUtils.LocomotionHomeMagnitude * Mathf.Sqrt(0.5f),
                ZombieAnimatorUtils.ComputeBlendMagnitude(0.8f, 1.6f, 5f), 0.001f);
            Assert.AreEqual(ZombieAnimatorUtils.LocomotionHomeMagnitude,
                ZombieAnimatorUtils.ComputeBlendMagnitude(9f, 1.6f, 5f));
        }
    }

    // The code constants are only true while the controller asset keeps
    // its shape: the walk clip's home position in the blend tree and the
    // SpeedMul binding on the locomotion state. Pin both against the
    // real asset so a controller edit fails loudly here instead of
    // silently un-matching every gait in game.
    public class ZombieLocomotionAssetTests
    {
        private const string ControllerPath = "Assets/_Game/Animations/Characters/Zombie/AC_Zombie.controller";
        private const string ZombiePrefabPath = "Assets/_Game/Prefabs/Characters/Zombie/Zombie.prefab";

        [Test]
        public void Controller_SpeedMulParameterExists_AndLocomotionStateBindsIt()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.IsNotNull(controller, "AC_Zombie controller must load.");

            bool found = false;
            foreach (var parameter in controller.parameters)
            {
                if (parameter.name == "SpeedMul" && parameter.type == AnimatorControllerParameterType.Float)
                {
                    found = true;
                }
            }
            Assert.IsTrue(found, "AC_Zombie must declare the SpeedMul float parameter.");

            AnimatorState locomotion = FindState(controller, "Blend Tree");
            Assert.IsNotNull(locomotion, "The locomotion state must exist.");
            Assert.IsTrue(locomotion.speedParameterActive, "The locomotion state's speed must be parameter-driven.");
            Assert.AreEqual("SpeedMul", locomotion.speedParameter);
        }

        [Test]
        public void Controller_WalkClipHome_MatchesLocomotionHomeMagnitude()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            AnimatorState locomotion = FindState(controller, "Blend Tree");
            var blendTree = locomotion.motion as BlendTree;
            Assert.IsNotNull(blendTree, "The locomotion state must hold the blend tree.");

            bool foundWalk = false;
            foreach (var child in blendTree.children)
            {
                if (child.motion != null && child.motion.name == "zombie_walk")
                {
                    foundWalk = true;
                    Assert.AreEqual(0f, child.position.x, 0.001f);
                    Assert.AreEqual(ZombieAnimatorUtils.LocomotionHomeMagnitude, child.position.y, 0.001f,
                        "If the walk clip moves in the tree, LocomotionHomeMagnitude must move with it.");
                }
            }
            Assert.IsTrue(foundWalk, "The blend tree must contain the base walk clip.");
        }

        [Test]
        public void Prefab_WalkClips_AuthoredSpeedsMatchTheMeasuredBasis()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ZombiePrefabPath);
            var variant = prefab.GetComponent<ZombieAnimationVariant>();
            Assert.IsNotNull(variant, "The zombie prefab must carry the variant component.");

            var so = new SerializedObject(variant);
            var baseWalk = so.FindProperty("_baseWalkClip").objectReferenceValue as AnimationClip;
            Assert.IsNotNull(baseWalk);
            Assert.AreEqual(0.31f, ZombieAnimationVariant.AuthoredSpeed(baseWalk), 0.05f,
                "The base walk's authored speed is the measurement the speed matching was tuned against.");

            var walkVariants = so.FindProperty("_walkVariants");
            Assert.Greater(walkVariants.arraySize, 0, "Walk variants must be configured.");
            for (int i = 0; i < walkVariants.arraySize; i++)
            {
                var clip = walkVariants.GetArrayElementAtIndex(i).objectReferenceValue as AnimationClip;
                Assert.IsNotNull(clip);
                float authored = ZombieAnimationVariant.AuthoredSpeed(clip);
                Assert.That(authored, Is.InRange(0.5f, 1.2f),
                    $"Variant {clip.name} authored {authored:F2} m/s — outside the measured 0.63–1.03 band.");
            }
        }

        private static AnimatorState FindState(AnimatorController controller, string stateName)
        {
            foreach (var layer in controller.layers)
            {
                foreach (var childState in layer.stateMachine.states)
                {
                    if (childState.state.name == stateName)
                    {
                        return childState.state;
                    }
                }
            }
            return null;
        }
    }
}
