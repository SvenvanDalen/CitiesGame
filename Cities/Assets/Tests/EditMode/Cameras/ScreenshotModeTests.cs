using System;
using Cities.Gameplay.Cameras;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Cameras
{
    public sealed class ScreenshotModeTests
    {
        [Test]
        public void FreeLookWidensPitchAndDistanceLimits()
        {
            var free = ScreenshotMode.FreeLook(CameraSettings.Default);

            Assert.That(free.MinPitch, Is.EqualTo(1f));
            Assert.That(free.MaxPitch, Is.EqualTo(89.9f));
            Assert.That(free.MinDistance, Is.LessThan(CameraSettings.Default.MinDistance));
            Assert.That(free.TerrainClearance, Is.LessThan(CameraSettings.Default.TerrainClearance));
            Assert.That(free.EdgeScroll, Is.False);
        }

        [Test]
        public void FreeLookIsGentlerThanNormal()
        {
            var normal = CameraSettings.Default;
            var free = ScreenshotMode.FreeLook(normal);

            Assert.That(free.RotateSpeed, Is.LessThan(normal.RotateSpeed));
            Assert.That(free.TiltSpeed, Is.LessThan(normal.TiltSpeed));
            Assert.That(free.MouseOrbitSensitivity, Is.LessThan(normal.MouseOrbitSensitivity));
            Assert.That(free.SmoothingHalfLife, Is.GreaterThanOrEqualTo(0.15f));
        }

        [Test]
        public void FreeLookKeepsValuesAlreadyBeyondItsLimits()
        {
            var normal = CameraSettings.Default;
            normal.MinDistance = 1f;
            normal.TerrainClearance = 0.5f;
            normal.SmoothingHalfLife = 0.5f;

            var free = ScreenshotMode.FreeLook(normal);

            Assert.That(free.MinDistance, Is.EqualTo(1f));
            Assert.That(free.TerrainClearance, Is.EqualTo(0.5f));
            Assert.That(free.SmoothingHalfLife, Is.EqualTo(0.5f));
        }

        [TestCase(1920, 1080, 3840, 2)]
        [TestCase(1920, 1080, 7680, 4)]
        [TestCase(1920, 1080, 1000, 1)]
        [TestCase(1920, 1080, 100000, 8)]
        [TestCase(3840, 2160, 30000, 4)]
        [TestCase(5000, 5000, 100000, 3)]
        public void SuperSizeReachesTargetWithinLimits(int w, int h, int target, int expected)
        {
            Assert.That(ScreenshotMode.SuperSize(w, h, target), Is.EqualTo(expected));
        }

        [Test]
        public void SuperSizeRejectsEmptyScreen()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ScreenshotMode.SuperSize(0, 1080, 3840));
            Assert.Throws<ArgumentOutOfRangeException>(() => ScreenshotMode.SuperSize(1920, 0, 3840));
        }

        [Test]
        public void FileNameContainsTimestamp()
        {
            Assert.That(ScreenshotMode.FileName(new DateTime(2026, 11, 3, 14, 15, 0)), Is.EqualTo("stad-20261103-141500.png"));
        }
    }
}
