using Cities.Presentation.UI;
using NUnit.Framework;

namespace Cities.Tests.EditMode.UI
{
    public class UiClassesTests
    {
        [TestCase(ButtonVariant.Primary, "c-button--primary")]
        [TestCase(ButtonVariant.Ghost, "c-button--ghost")]
        [TestCase(ButtonVariant.Danger, "c-button--danger")]
        [TestCase(ButtonVariant.Secondary, null)]
        public void ButtonVariantClasses(ButtonVariant variant, string expected)
        {
            Assert.That(UiClasses.ForVariant(variant), Is.EqualTo(expected));
        }

        [TestCase(UiStatus.Success, "c-badge--success")]
        [TestCase(UiStatus.Warning, "c-badge--warning")]
        [TestCase(UiStatus.Danger, "c-badge--danger")]
        [TestCase(UiStatus.Info, "c-badge--info")]
        [TestCase(UiStatus.Neutral, null)]
        public void StatusClasses(UiStatus status, string expected)
        {
            Assert.That(UiClasses.ForStatus(status), Is.EqualTo(expected));
        }

        [TestCase(MeterColor.Residential, "c-meter--residential")]
        [TestCase(MeterColor.Commercial, "c-meter--commercial")]
        [TestCase(MeterColor.Industrial, "c-meter--industrial")]
        [TestCase(MeterColor.Office, "c-meter--office")]
        [TestCase(MeterColor.Accent, null)]
        public void MeterColorClasses(MeterColor color, string expected)
        {
            Assert.That(UiClasses.ForMeterColor(color), Is.EqualTo(expected));
        }

        [TestCase(0.4f, 0.4f)]
        [TestCase(-1f, 0f)]
        [TestCase(3f, 1f)]
        [TestCase(float.NaN, 0f)]
        public void MeterValueIsClamped(float input, float expected)
        {
            Assert.That(CityMeter.Clamp01(input), Is.EqualTo(expected));
        }
    }
}
