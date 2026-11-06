using Cities.Presentation.UI;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Cities.Tests.EditMode.UI
{
    public class ToolbarHotkeysTests
    {
        [Test]
        public void EveryTabHasItsOwnKeyAndTRemainsFree()
        {
            var seen = new System.Collections.Generic.HashSet<Key>();
            foreach (var tab in ToolbarModel.Tabs)
            {
                var key = ToolbarHotkeys.KeyOf(tab);
                Assert.That(seen.Add(key), Is.True, tab.ToString());
                Assert.That(key, Is.Not.EqualTo(Key.T));
                Assert.That(ToolbarHotkeys.TabFor(key), Is.EqualTo(tab));
            }

            Assert.That(ToolbarHotkeys.TabFor(Key.T), Is.Null);
        }

        [TestCase(Key.Digit1, 0)]
        [TestCase(Key.Digit9, 8)]
        [TestCase(Key.Numpad3, 2)]
        [TestCase(Key.Digit0, -1)]
        [TestCase(Key.A, -1)]
        public void DigitsPickEntries(Key key, int index) => Assert.That(ToolbarHotkeys.EntryIndex(key), Is.EqualTo(index));

        [Test]
        public void TooltipShowsHotkeyAndCostOnlyWhenKnown()
        {
            Assert.That(ToolbarHotkeys.Tooltip("Weg", null, 0), Is.EqualTo("Weg"));
            Assert.That(ToolbarHotkeys.Tooltip("Weg", "1", 0), Is.EqualTo("Weg [1]"));
            Assert.That(ToolbarHotkeys.Tooltip("Centrale", "2", 120000), Is.EqualTo("Centrale [2] · € 120.000"));
        }
    }
}
