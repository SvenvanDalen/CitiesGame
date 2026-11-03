using System;
using Cities.Gameplay.Cameras;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Cameras
{
    public class FollowCameraControllerTests
    {
        private sealed class DummyTarget : IFollowTarget
        {
            public bool IsValid { get; set; } = true;

            public float3 Position { get; set; } = new float3(100f, 0f, 200f);

            public float Yaw { get; set; } = 90f;

            public float EyeHeight { get; set; } = 1.6f;
        }

        [Test]
        public void FollowNullThrows()
        {
            Assert.Throws<ArgumentNullException>(() => new FollowCameraController().Follow(null, FollowMode.Chase));
        }

        [Test]
        public void FirstPersonSitsAtEyeHeightAndLooksAlongTarget()
        {
            var target = new DummyTarget();
            var camera = new FollowCameraController();
            camera.Follow(target, FollowMode.FirstPerson);

            Assert.That(camera.IsFollowing);
            Assert.That(camera.Current.Position.y, Is.EqualTo(1.6f).Within(1e-4f));
            Assert.That(camera.Current.Position.x, Is.EqualTo(100f).Within(1e-4f));
            Assert.That(camera.Current.Yaw, Is.EqualTo(90f).Within(1e-3f));
            Assert.That(camera.Current.Pitch, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void ChaseSitsBehindAndAboveTarget()
        {
            var camera = new FollowCameraController();
            camera.Follow(new DummyTarget { Yaw = 0f }, FollowMode.Chase);

            var position = camera.Current.Position;
            Assert.That(position.z, Is.LessThan(200f - 5f));
            Assert.That(position.y, Is.GreaterThan(FollowCameraController.ChaseHeight));
        }

        [Test]
        public void CameraFollowsMovingTarget()
        {
            var target = new DummyTarget();
            var camera = new FollowCameraController();
            camera.Follow(target, FollowMode.FirstPerson);

            target.Position = new float3(110f, 0f, 200f);
            for (var i = 0; i < 60; i++)
            {
                Assert.That(camera.Update(float2.zero, 0.05f));
            }

            Assert.That(camera.Current.Position.x, Is.EqualTo(110f).Within(0.01f));
        }

        [Test]
        public void TurnsAlongWithTargetTheShortWay()
        {
            var target = new DummyTarget { Yaw = 350f };
            var camera = new FollowCameraController();
            camera.Follow(target, FollowMode.FirstPerson);

            target.Yaw = 10f;
            camera.Update(float2.zero, 0.01f);

            Assert.That(CameraRig.DeltaAngle(camera.Current.Yaw, 10f), Is.GreaterThan(0f).And.LessThan(20f));
        }

        [Test]
        public void LookDeltaTurnsViewAndPitchIsLimited()
        {
            var camera = new FollowCameraController();
            camera.Follow(new DummyTarget { Yaw = 0f }, FollowMode.FirstPerson);

            for (var i = 0; i < 100; i++)
            {
                camera.Update(new float2(0f, 10f), 0.1f);
            }

            Assert.That(camera.Current.Pitch, Is.EqualTo(FollowCameraController.LookPitchLimit).Within(0.1f));

            camera.Update(new float2(-90f, 0f), 10f);
            Assert.That(camera.Current.Yaw, Is.EqualTo(270f).Within(0.1f));
        }

        [Test]
        public void ChaseCannotLookBelowGround()
        {
            var camera = new FollowCameraController();
            camera.Follow(new DummyTarget(), FollowMode.Chase);

            camera.Update(new float2(0f, -500f), 10f);

            Assert.That(camera.Current.Pitch, Is.GreaterThanOrEqualTo(-0.001f));
        }

        [Test]
        public void ToggleModeSwitchesAndResetsLook()
        {
            var camera = new FollowCameraController();
            camera.Follow(new DummyTarget { Yaw = 0f }, FollowMode.Chase);
            camera.Update(new float2(40f, 0f), 10f);

            camera.ToggleMode();
            camera.Update(float2.zero, 10f);

            Assert.That(camera.Mode, Is.EqualTo(FollowMode.FirstPerson));
            Assert.That(camera.Current.Yaw, Is.EqualTo(0f).Within(0.1f));
            camera.ToggleMode();
            Assert.That(camera.Mode, Is.EqualTo(FollowMode.Chase));
        }

        [Test]
        public void StopsWhenTargetDisappears()
        {
            var target = new DummyTarget();
            var camera = new FollowCameraController();
            camera.Follow(target, FollowMode.FirstPerson);

            target.IsValid = false;

            Assert.That(camera.Update(float2.zero, 0.016f), Is.False);
            Assert.That(camera.IsFollowing, Is.False);
            Assert.That(camera.Update(float2.zero, 0.016f), Is.False);
        }

        [Test]
        public void StopEndsFollowing()
        {
            var camera = new FollowCameraController();
            camera.Follow(new DummyTarget(), FollowMode.Chase);
            camera.Stop();

            Assert.That(camera.IsFollowing, Is.False);
        }

        [Test]
        public void NegativeFrameTimeThrows()
        {
            var camera = new FollowCameraController();
            camera.Follow(new DummyTarget(), FollowMode.Chase);

            Assert.Throws<ArgumentOutOfRangeException>(() => camera.Update(float2.zero, -1f));
        }
    }
}
