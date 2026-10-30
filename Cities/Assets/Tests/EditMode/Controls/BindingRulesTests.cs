using Cities.Gameplay.Controls;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Controls
{
    public class BindingRulesTests
    {
        [TestCase(BindingKind.CompositePart, "<Keyboard>/w", false, true)]
        [TestCase(BindingKind.CompositePart, "", false, true)]
        [TestCase(BindingKind.Composite, "2DVector", false, false)]
        [TestCase(BindingKind.Simple, "<Mouse>/rightButton", true, true)]
        [TestCase(BindingKind.Simple, "", true, true)]
        [TestCase(BindingKind.Simple, "<Mouse>/scroll/y", false, false)]
        [TestCase(BindingKind.Simple, "<Keyboard>/escape", true, false)]
        [TestCase(BindingKind.CompositePart, "<Keyboard>/Escape", false, false)]
        [TestCase(BindingKind.Simple, "<Mouse>/leftButton", true, false)]
        public void Rebindable(BindingKind kind, string path, bool button, bool expected)
        {
            var binding = new BindingInfo("Camera", "Action", 0, "part", path, kind, button);

            Assert.That(BindingRules.IsRebindable(binding), Is.EqualTo(expected));
        }

        [TestCase("<Keyboard>/w", true)]
        [TestCase("<keyboard>/numpad1", true)]
        [TestCase("<Mouse>/middleButton", true)]
        [TestCase("<Mouse>/forwardButton", true)]
        [TestCase("<Mouse>/leftButton", false)]
        [TestCase("<Mouse>/scroll/up", false)]
        [TestCase("<Mouse>/position", false)]
        [TestCase("<Keyboard>/anyKey", false)]
        [TestCase("<Keyboard>/escape", false)]
        [TestCase("<Gamepad>/buttonSouth", false)]
        [TestCase("<Keyboard>", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void Assignable(string path, bool expected)
        {
            Assert.That(BindingRules.IsAssignable(path), Is.EqualTo(expected));
        }

        [Test]
        public void PathsCompareWithoutCase()
        {
            Assert.That(BindingRules.SamePath("<Keyboard>/W", "<keyboard>/w"), Is.True);
            Assert.That(BindingRules.SamePath(null, string.Empty), Is.True);
            Assert.That(BindingRules.SamePath("<Keyboard>/w", "<Keyboard>/s"), Is.False);
        }
    }
}
