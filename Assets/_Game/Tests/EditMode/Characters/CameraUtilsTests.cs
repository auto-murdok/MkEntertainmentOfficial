using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public class CameraUtilsTests
    {
        private GameObject _cameraTargetHost;
        private CharacterStateContext _context;
        private CinemachineContext _cine;
        private CameraUtils _utils;

        [SetUp]
        public void SetUp()
        {
            _cameraTargetHost = new GameObject("CameraTarget");
            _context = new CharacterStateContext
            {
                mainCameraTarget = _cameraTargetHost.transform,
                isCurrentDeviceMouse = true
            };
            _cine = new CinemachineContext
            {
                topClamp = 60f,
                bottomClamp = -70f,
                lookSensivity = 1f
            };
            _utils = new CameraUtils();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_cameraTargetHost);
        }

        private float TargetYaw => _cameraTargetHost.transform.eulerAngles.y;
        private float TargetPitch => _cameraTargetHost.transform.eulerAngles.x > 180f
            ? _cameraTargetHost.transform.eulerAngles.x - 360f
            : _cameraTargetHost.transform.eulerAngles.x;

        [Test]
        public void HandleCameraRotation_MouseInput_AccumulatesYaw()
        {
            _context.lookInput = new Vector2(10f, 0f);
            _utils.HandleCameraRotation(_context, _cine);
            Assert.AreEqual(10f, TargetYaw, 0.01f);
        }

        [Test]
        public void HandleCameraRotation_MouseInput_AccumulatesPitchInverted()
        {
            _context.lookInput = new Vector2(0f, 5f);
            _utils.HandleCameraRotation(_context, _cine);
            Assert.AreEqual(-5f, TargetPitch, 0.01f);
        }

        [Test]
        public void HandleCameraRotation_PitchIsClampedToTopClamp()
        {
            _context.lookInput = new Vector2(0f, -500f);
            _utils.HandleCameraRotation(_context, _cine);
            Assert.AreEqual(60f, TargetPitch, 0.01f);
        }

        [Test]
        public void HandleCameraRotation_PitchIsClampedToBottomClamp()
        {
            _context.lookInput = new Vector2(0f, 500f);
            _utils.HandleCameraRotation(_context, _cine);
            Assert.AreEqual(-70f, TargetPitch, 0.01f);
        }

        [Test]
        public void HandleCameraRotation_YawWrapsWithinPlusMinus360()
        {
            _context.lookInput = new Vector2(400f, 0f);
            _utils.HandleCameraRotation(_context, _cine);
            Assert.AreEqual(40f, TargetYaw, 0.01f);
        }

        [Test]
        public void HandleCameraRotation_ControllerBelowThreshold_DoesNotRotate()
        {
            _context.isCurrentDeviceMouse = false;
            _context.lookInput = new Vector2(0.05f, 0.05f);
            _utils.HandleCameraRotation(_context, _cine);
            Assert.AreEqual(0f, TargetYaw, 0.01f);
            Assert.AreEqual(0f, TargetPitch, 0.01f);
        }

        // NOTE: the "controller above threshold rotates" case lives in
        // PlayMode (CameraUtilsPlayTests) — controller look scales input by
        // Time.deltaTime, which is 0 in EditMode tests.

        [Test]
        public void HandleCameraRotation_NoInput_KeepsRotation()
        {
            _context.lookInput = Vector2.zero;
            _utils.HandleCameraRotation(_context, _cine);
            Assert.AreEqual(0f, TargetYaw, 0.01f);
        }

        [Test]
        public void HandleCameraRotation_SensitivityScalesMouseInput()
        {
            _cine.lookSensivity = 2f;
            _context.lookInput = new Vector2(5f, 0f);
            _utils.HandleCameraRotation(_context, _cine);
            Assert.AreEqual(10f, TargetYaw, 0.01f);
        }
    }
}
