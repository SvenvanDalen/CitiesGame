using Cities.Core.Settings;
using Cities.Gameplay.Cameras;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Cameras
{
    public class CameraUserSettingsTests
    {
        [Test]
        public void DefaultsKeepTheBaseline()
        {
            var result = CameraUserSettings.Apply(CameraSettings.Default, new UserSettings());

            Assert.That(result.MouseOrbitSensitivity, Is.EqualTo(CameraSettings.Default.MouseOrbitSensitivity));
            Assert.That(result.EdgeScroll, Is.False);
            Assert.That(result.InvertZoom, Is.False);
            Assert.That(result.InvertOrbitY, Is.False);
        }

        [Test]
        public void SensitivityScalesTheSpeedsAndTogglesCarryOver()
        {
            var user = new UserSettings();
            user.Set(SettingsCatalog.CameraSensitivity, 2f);
            user.SetBool(SettingsCatalog.EdgeScroll, true);
            user.SetBool(SettingsCatalog.InvertZoom, true);
            user.SetBool(SettingsCatalog.InvertCameraY, true);

            var result = CameraUserSettings.Apply(CameraSettings.Default, user);

            Assert.That(result.MouseOrbitSensitivity, Is.EqualTo(CameraSettings.Default.MouseOrbitSensitivity * 2f));
            Assert.That(result.RotateSpeed, Is.EqualTo(CameraSettings.Default.RotateSpeed * 2f));
            Assert.That(result.EdgeScroll && result.InvertZoom && result.InvertOrbitY, Is.True);
        }

        [Test]
        public void InvertedZoomScrollsTheOtherWay()
        {
            var settings = CameraSettings.Default;
            settings.SmoothingHalfLife = 0f;
            settings.InvertZoom = true;
            var controller = new CameraController(settings, new CameraRig(new float3(5000f, 0f, 5000f), 0f, 45f, 500f), float2.zero, new float2(10_000f, 10_000f));

            controller.Update(new CameraInput { ScrollSteps = 1f }, 1f / 60f);

            Assert.That(controller.Current.Distance, Is.GreaterThan(500f));
        }

        [Test]
        public void NullSettingsAreRefused() =>
            Assert.Throws<System.ArgumentNullException>(() => CameraUserSettings.Apply(CameraSettings.Default, null));
    }
}
