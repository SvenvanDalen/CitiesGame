using Cities.Gameplay.Controls;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Controls
{
    public class KeyNamesTests
    {
        [TestCase("<Keyboard>/upArrow", "Pijl omhoog")]
        [TestCase("<keyboard>/SPACE", "Spatie")]
        [TestCase("<Keyboard>/rightShift", "Rechter Shift")]
        [TestCase("<Keyboard>/numpad7", "Numeriek 7")]
        [TestCase("<Keyboard>/numpadPlus", "Numeriek +")]
        [TestCase("<Mouse>/middleButton", "Middelste muisknop")]
        public void TranslatesKnownKeys(string path, string expected)
        {
            Assert.That(KeyNames.For(path, "fallback"), Is.EqualTo(expected));
        }

        [TestCase("<Keyboard>/w")]
        [TestCase("<Keyboard>/numpad")]
        [TestCase("<Keyboard>/numpadX")]
        [TestCase("<Keyboard>/numpad12")]
        [TestCase("")]
        [TestCase(null)]
        public void OtherKeysKeepTheKeyboardName(string path)
        {
            Assert.That(KeyNames.For(path, "fallback"), Is.EqualTo("fallback"));
        }
    }
}
