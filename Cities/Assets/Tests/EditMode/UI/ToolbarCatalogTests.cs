using Cities.Presentation.UI;
using NUnit.Framework;

namespace Cities.Tests.EditMode.UI
{
    public class ToolbarCatalogTests
    {
        [TestCase("Centrale", ToolbarTab.Utilities)]
        [TestCase("Stortplaats", ToolbarTab.Utilities)]
        [TestCase("Politie", ToolbarTab.Services)]
        [TestCase("Bushalte", ToolbarTab.Transit)]
        [TestCase("Mijn", ToolbarTab.Industry)]
        [TestCase("Onbekend", ToolbarTab.Services)]
        [TestCase(null, ToolbarTab.Services)]
        public void ToolsLandInTheirCategory(string name, ToolbarTab expected)
            => Assert.That(ToolbarCatalog.TabFor(name), Is.EqualTo(expected));

        [Test]
        public void EveryTabHasALabel()
        {
            foreach (var tab in ToolbarModel.Tabs)
            {
                Assert.That(ToolbarModel.LabelOf(tab), Is.Not.Empty);
            }
        }
    }
}
