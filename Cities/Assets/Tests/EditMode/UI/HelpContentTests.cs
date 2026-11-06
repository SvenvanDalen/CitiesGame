using Cities.Presentation.UI;
using NUnit.Framework;

namespace Cities.Tests.EditMode.UI
{
    public class HelpContentTests
    {
        [Test]
        public void ListsEveryCategoryWithItsKey()
        {
            var lines = HelpContent.Lines();

            Assert.That(lines, Does.Contain("R  Wegen"));
            Assert.That(lines, Does.Contain("G  Terrein"));
            Assert.That(lines.Count, Is.GreaterThan(ToolbarModel.Tabs.Count));
        }
    }
}
