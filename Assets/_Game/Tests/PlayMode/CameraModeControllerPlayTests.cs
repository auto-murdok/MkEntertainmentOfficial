using System.Collections;
using Cinemachine;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public class CameraModeControllerPlayTests
    {
        private GameObject _followHost;
        private GameObject _aimHost;
        private GameObject _controllerHost;
        private CinemachineVirtualCamera _followCamera;
        private CinemachineVirtualCamera _aimCamera;
        private CameraModeController _controller;

        [SetUp]
        public void SetUp()
        {
            _followHost = new GameObject("PlayerFollowCamera");
            _followCamera = _followHost.AddComponent<CinemachineVirtualCamera>();
            _followCamera.Priority = CameraModeController.StandbyPriority;

            _aimHost = new GameObject("PlayerAimCamera");
            _aimCamera = _aimHost.AddComponent<CinemachineVirtualCamera>();
            _aimCamera.Priority = CameraModeController.LivePriority;

            _controllerHost = new GameObject("CameraModeControllerHost");
            _controller = _controllerHost.AddComponent<CameraModeController>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_followHost);
            Object.Destroy(_aimHost);
            Object.Destroy(_controllerHost);
        }

        [UnityTest]
        public IEnumerator Wire_NoStateMachine_DefaultsToFollowMode()
        {
            _controller.Wire(null, _followCamera, _aimCamera);
            yield return null;

            Assert.AreEqual(CameraMode.Follow, _controller.CurrentMode);
            Assert.AreEqual(CameraModeController.LivePriority, _followCamera.Priority,
                "Follow camera must hold the live priority in Follow mode.");
            Assert.AreEqual(CameraModeController.StandbyPriority, _aimCamera.Priority,
                "Aim camera must be on standby priority in Follow mode.");
        }

        [UnityTest]
        public IEnumerator SetMode_Aim_AimCameraHoldsLivePriority()
        {
            _controller.Wire(null, _followCamera, _aimCamera);
            _controller.SetMode(CameraMode.Aim);
            yield return null;

            Assert.AreEqual(CameraMode.Aim, _controller.CurrentMode);
            Assert.AreEqual(CameraModeController.LivePriority, _aimCamera.Priority);
            Assert.AreEqual(CameraModeController.StandbyPriority, _followCamera.Priority);
        }

        [UnityTest]
        public IEnumerator SetMode_FollowAfterAim_RestoresFollowPriority()
        {
            _controller.Wire(null, _followCamera, _aimCamera);
            _controller.SetMode(CameraMode.Aim);
            _controller.SetMode(CameraMode.Follow);
            yield return null;

            Assert.AreEqual(CameraMode.Follow, _controller.CurrentMode);
            Assert.AreEqual(CameraModeController.LivePriority, _followCamera.Priority);
            Assert.AreEqual(CameraModeController.StandbyPriority, _aimCamera.Priority);
        }

        [UnityTest]
        public IEnumerator Wire_AimCameraStandbyUpdateIsAlways()
        {
            _aimCamera.m_StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.RoundRobin;
            _controller.Wire(null, _followCamera, _aimCamera);
            yield return null;

            Assert.AreEqual(CinemachineVirtualCameraBase.StandbyUpdateMode.Always, _aimCamera.m_StandbyUpdate,
                "The aim camera must keep tracking the shared follow target while on standby.");
        }
    }
}
