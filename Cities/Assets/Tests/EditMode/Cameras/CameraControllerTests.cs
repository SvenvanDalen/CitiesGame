using System;
using Cities.Gameplay.Cameras;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Cameras
{
    public class CameraControllerTests
    {
        private const float Frame = 1f / 60f;
        private static readonly float2 BoundsMin = float2.zero;
        private static readonly float2 BoundsMax = new float2(10_000f, 10_000f);

        private static CameraSettings Instant()
        {
            var settings = CameraSettings.Default;
            settings.SmoothingHalfLife = 0f;
            return settings;
        }

        private static CameraController Create(CameraSettings settings, float yaw = 0f, float distance = 500f) =>
            new CameraController(settings, new CameraRig(new float3(5000f, 0f, 5000f), yaw, 45f, distance), BoundsMin, BoundsMax);

        [Test]
        public void RigPlacesCameraBehindAndAbovePivot()
        {
            var rig = new CameraRig(new float3(100f, 0f, 100f), 0f, 45f, 100f);

            Assert.That(rig.Position.y, Is.EqualTo(70.71f).Within(0.01f));
            Assert.That(rig.Position.z, Is.EqualTo(100f - 70.71f).Within(0.01f), "Kijkt naar noord, dus staat aan de zuidkant");
            Assert.That(rig.Height, Is.EqualTo(70.71f).Within(0.01f));
            Assert.That(math.length(rig.Forward), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void TopDownLooksStraightDown()
        {
            var rig = new CameraRig(float3.zero, 123f, 90f, 50f);

            Assert.That(rig.Forward.y, Is.EqualTo(-1f).Within(1e-5f));
            Assert.That(rig.Position.y, Is.EqualTo(50f).Within(1e-3f));
        }

        [Test]
        public void GroundAxesFollowYaw()
        {
            var east = new CameraRig(float3.zero, 90f, 45f, 10f);

            Assert.That(east.GroundForward.x, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(east.GroundRight.z, Is.EqualTo(-1f).Within(1e-5f));
        }

        [Test]
        public void KeyPanMovesRelativeToYawAndScalesWithDistance()
        {
            var near = Create(Instant(), 90f, 100f);
            var far = Create(Instant(), 90f, 1000f);
            var input = new CameraInput { Move = new float2(0f, 1f) };

            near.Update(input, 1f);
            far.Update(input, 1f);

            Assert.That(near.Current.Pivot.x - 5000f, Is.EqualTo(110f).Within(0.01f), "Vooruit is oost bij yaw 90");
            Assert.That(far.Current.Pivot.x - 5000f, Is.EqualTo(1100f).Within(0.1f));
            Assert.That(near.Current.Pivot.z, Is.EqualTo(5000f).Within(0.01f));
        }

        [Test]
        public void FastMultipliesPan()
        {
            var controller = Create(Instant());

            controller.Update(new CameraInput { Move = new float2(1f, 0f), Fast = true }, 0.1f);

            Assert.That(controller.Current.Pivot.x - 5000f, Is.EqualTo(500f * 1.1f * 3f * 0.1f).Within(0.01f));
        }

        [Test]
        public void ScrollZoomIsExponentialAndClamped()
        {
            var controller = Create(Instant());

            controller.Update(new CameraInput { ScrollSteps = 1f }, Frame);
            Assert.That(controller.Current.Distance, Is.EqualTo(500f * 0.88f).Within(0.01f));

            controller.Update(new CameraInput { ScrollSteps = 100f }, Frame);
            Assert.That(controller.Current.Distance, Is.EqualTo(CameraSettings.Default.MinDistance));

            controller.Update(new CameraInput { ScrollSteps = -200f }, Frame);
            Assert.That(controller.Current.Distance, Is.EqualTo(CameraSettings.Default.MaxDistance));
        }

        [Test]
        public void KeyZoomDependsOnTimeNotFrames()
        {
            var a = Create(Instant());
            var b = Create(Instant());

            for (var i = 0; i < 60; i++)
            {
                a.Update(new CameraInput { Zoom = 1f }, 1f / 60f);
            }

            for (var i = 0; i < 144; i++)
            {
                b.Update(new CameraInput { Zoom = 1f }, 1f / 144f);
            }

            Assert.That(a.Current.Distance, Is.EqualTo(b.Current.Distance).Within(0.5f));
            Assert.That(a.Current.Distance, Is.EqualTo(500f * math.exp(-1.5f)).Within(0.5f));
        }

        [Test]
        public void RotateAndTiltWithKeysAndMouse()
        {
            var controller = Create(Instant());

            controller.Update(new CameraInput { Rotate = 1f, Tilt = 1f }, 0.5f);
            Assert.That(controller.Current.Yaw, Is.EqualTo(45f).Within(1e-3f));
            Assert.That(controller.Current.Pitch, Is.EqualTo(75f).Within(1e-3f));

            controller.Update(new CameraInput { OrbitPixels = new float2(-400f, 100f) }, Frame);
            Assert.That(controller.Current.Yaw, Is.EqualTo(305f).Within(1e-3f), "45° min 100° draait over 0 heen naar 305°");
            Assert.That(controller.Current.Pitch, Is.EqualTo(50f).Within(1e-3f), "Muis omhoog kijkt vlakker");
        }

        [Test]
        public void PitchIsClamped()
        {
            var controller = Create(Instant());

            controller.Update(new CameraInput { Tilt = 10f }, 1f);
            Assert.That(controller.Current.Pitch, Is.EqualTo(CameraSettings.Default.MaxPitch));

            controller.Update(new CameraInput { Tilt = -10f }, 1f);
            Assert.That(controller.Current.Pitch, Is.EqualTo(CameraSettings.Default.MinPitch));
        }

        [Test]
        public void PivotStaysInsideBounds()
        {
            var controller = Create(Instant(), 0f, 6000f);

            controller.Update(new CameraInput { Move = new float2(-1f, -1f) }, 10f);

            Assert.That(controller.Current.Pivot.x, Is.EqualTo(0f));
            Assert.That(controller.Current.Pivot.z, Is.EqualTo(0f));
        }

        [Test]
        public void DragPanFollowsPointer()
        {
            var controller = Create(Instant(), 0f, 1000f);

            controller.Update(new CameraInput { DragPanPixels = new float2(100f, 0f), ScreenSize = new float2(1920f, 1080f) }, Frame);

            // Meters per pixel = 2 · 1000 · tan(22,5°) / 1080 ≈ 0,767; slepen naar rechts schuift de stad mee, dus het draaipunt naar links.
            Assert.That(controller.Current.Pivot.x - 5000f, Is.EqualTo(-76.7f).Within(0.1f));
        }

        [Test]
        public void DragPanWithoutScreenDoesNothing()
        {
            var controller = Create(Instant());

            controller.Update(new CameraInput { DragPanPixels = new float2(100f, 50f) }, Frame);

            Assert.That(controller.Current.Pivot, Is.EqualTo(new float3(5000f, 0f, 5000f)));
        }

        [Test]
        public void EdgeScrollOnlyWhenEnabledAndPointerInside()
        {
            var settings = Instant();
            var screen = new float2(1920f, 1080f);
            var off = Create(settings);
            off.Update(new CameraInput { PointerPosition = new float2(1919f, 540f), ScreenSize = screen }, 1f);
            Assert.That(off.Current.Pivot.x, Is.EqualTo(5000f));

            settings.EdgeScroll = true;
            var on = Create(settings);
            on.Update(new CameraInput { PointerPosition = new float2(1919f, 5f), ScreenSize = screen }, 0.1f);
            Assert.That(on.Current.Pivot.x, Is.GreaterThan(5000f), "Rechterrand");
            Assert.That(on.Current.Pivot.z, Is.LessThan(5000f), "Onderrand");

            var outside = Create(settings);
            outside.Update(new CameraInput { PointerPosition = new float2(2500f, 540f), ScreenSize = screen }, 1f);
            Assert.That(outside.Current.Pivot.x, Is.EqualTo(5000f), "Muis buiten het venster");

            var middle = Create(settings);
            middle.Update(new CameraInput { PointerPosition = new float2(960f, 540f), ScreenSize = screen }, 1f);
            Assert.That(middle.Current.Pivot, Is.EqualTo(new float3(5000f, 0f, 5000f)));
        }

        [Test]
        public void SmoothingHalvesRemainingDistancePerHalfLife()
        {
            var settings = CameraSettings.Default;
            settings.SmoothingHalfLife = 0.1f;
            var controller = Create(settings);
            controller.SetTarget(new CameraRig(new float3(6000f, 0f, 5000f), 0f, 45f, 500f));

            controller.Update(default, 0.1f);

            Assert.That(controller.Current.Pivot.x, Is.EqualTo(5500f).Within(0.5f));
            Assert.That(controller.Target.Pivot.x, Is.EqualTo(6000f));
        }

        [Test]
        public void SmoothedYawTakesShortestWay()
        {
            var settings = CameraSettings.Default;
            settings.SmoothingHalfLife = 0.1f;
            var controller = Create(settings, 350f);
            controller.SetTarget(new CameraRig(controller.Target.Pivot, 10f, 45f, 500f));

            controller.Update(default, 0.1f);

            Assert.That(controller.Current.Yaw, Is.EqualTo(0f).Within(0.01f).Or.EqualTo(360f).Within(0.01f));
        }

        [Test]
        public void TeleportSkipsSmoothingAndIsClamped()
        {
            var controller = Create(CameraSettings.Default);

            controller.Teleport(new CameraRig(new float3(-50f, 0f, 20_000f), -30f, 120f, 1f));

            Assert.That(controller.Current, Is.EqualTo(controller.Target));
            Assert.That(controller.Current.Pivot, Is.EqualTo(new float3(0f, 0f, 10_000f)));
            Assert.That(controller.Current.Yaw, Is.EqualTo(330f));
            Assert.That(controller.Current.Pitch, Is.EqualTo(CameraSettings.Default.MaxPitch));
            Assert.That(controller.Current.Distance, Is.EqualTo(CameraSettings.Default.MinDistance));
        }

        [Test]
        public void BoundsAndSettingsCanChange()
        {
            var controller = Create(Instant());

            controller.SetBounds(float2.zero, new float2(1000f, 1000f));
            Assert.That(controller.Target.Pivot.x, Is.EqualTo(1000f));

            var settings = Instant();
            settings.MaxDistance = 300f;
            controller.Settings = settings;
            Assert.That(controller.Target.Distance, Is.EqualTo(300f));
            Assert.That(controller.Settings.MaxDistance, Is.EqualTo(300f));
        }

        [Test]
        public void InvalidArgumentsAreRejected()
        {
            Assert.Throws<ArgumentException>(() => _ = new CameraController(Instant(), default, new float2(10f), float2.zero));
            var controller = Create(Instant());
            Assert.Throws<ArgumentException>(() => controller.SetBounds(new float2(10f), float2.zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => controller.Update(default, -1f));
        }

        [Test]
        public void AngleHelpers()
        {
            Assert.That(CameraRig.DeltaAngle(350f, 10f), Is.EqualTo(20f));
            Assert.That(CameraRig.DeltaAngle(10f, 350f), Is.EqualTo(-20f));
            Assert.That(CameraRig.DeltaAngle(0f, 540f), Is.EqualTo(180f));
            Assert.That(CameraRig.NormalizeYaw(-90f), Is.EqualTo(270f));
            Assert.That(CameraRig.NormalizeYaw(720f), Is.EqualTo(0f));
        }

        [Test]
        public void RigEqualityAndText()
        {
            var a = new CameraRig(new float3(1f, 2f, 3f), 10f, 45f, 100f);

            Assert.That(a == new CameraRig(new float3(1f, 2f, 3f), 10f, 45f, 100f), Is.True);
            Assert.That(a != a.WithPivot(float3.zero), Is.True);
            Assert.That(a.Equals((object)new CameraRig(new float3(1f, 2f, 3f), 10f, 45f, 100f)), Is.True);
            Assert.That(a.Equals("rig"), Is.False);
            Assert.That(a.GetHashCode(), Is.EqualTo(new CameraRig(new float3(1f, 2f, 3f), 10f, 45f, 100f).GetHashCode()));
            Assert.That(a.ToString(), Does.Contain("yaw 10°"));
        }
    }
}
