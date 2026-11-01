using System;
using Cities.Presentation.UI;
using NUnit.Framework;

namespace Cities.Tests.EditMode.UI
{
    public class ToolbarModelTests
    {
        private static ToolbarModel Model()
        {
            var model = new ToolbarModel();
            model.Add(ToolbarTab.Roads, new ToolbarEntry("road.straight", "Rechte weg"));
            model.Add(ToolbarTab.Zones, new ToolbarEntry("zone.res", "Wonen"));
            return model;
        }

        [Test]
        public void EveryTabHasADutchLabelAndStartsEmptyAndClosed()
        {
            var model = new ToolbarModel();

            Assert.That(model.OpenTab, Is.Null);
            foreach (var tab in ToolbarModel.Tabs)
            {
                Assert.That(ToolbarModel.LabelOf(tab), Is.Not.Empty);
                Assert.That(model.EntriesOf(tab), Is.Empty);
            }

            Assert.That(ToolbarModel.Tabs.Count, Is.EqualTo(7));
            Assert.That(ToolbarModel.LabelOf(ToolbarTab.Utilities), Is.EqualTo("Nutsvoorzieningen"));
        }

        [Test]
        public void ATabOpensItsPanelAndTheSameTabClosesIt()
        {
            var model = Model();
            var changes = 0;
            model.Changed += () => changes++;

            model.ToggleTab(ToolbarTab.Roads);
            Assert.That(model.OpenTab, Is.EqualTo(ToolbarTab.Roads));
            model.ToggleTab(ToolbarTab.Zones);
            Assert.That(model.OpenTab, Is.EqualTo(ToolbarTab.Zones));
            model.ToggleTab(ToolbarTab.Zones);
            Assert.That(model.OpenTab, Is.Null);
            Assert.That(changes, Is.EqualTo(3));
        }

        [Test]
        public void OnlyAnEntryOfTheOpenTabCanBeSelected()
        {
            var model = Model();

            Assert.That(model.Select("road.straight"), Is.False, "geen tab open");
            model.ToggleTab(ToolbarTab.Roads);
            Assert.That(model.Select("zone.res"), Is.False, "andere tab");
            Assert.That(model.Select("nope"), Is.False);
            Assert.That(model.SelectedId, Is.Null);
            Assert.That(model.Select("road.straight"), Is.True);
            Assert.That(model.SelectedId, Is.EqualTo("road.straight"));
        }

        [Test]
        public void CancelClearsTheSelectionFirstAndThenClosesThePanel()
        {
            var model = Model();
            model.ToggleTab(ToolbarTab.Roads);
            model.Select("road.straight");

            model.Cancel();
            Assert.That(model.SelectedId, Is.Null);
            Assert.That(model.OpenTab, Is.EqualTo(ToolbarTab.Roads));
            model.Cancel();
            Assert.That(model.OpenTab, Is.Null);
        }

        [Test]
        public void DuplicateIdsAndNullsAreRejected()
        {
            var model = Model();

            Assert.Throws<ArgumentException>(() => model.Add(ToolbarTab.Transit, new ToolbarEntry("road.straight", "x")));
            Assert.Throws<ArgumentNullException>(() => new ToolbarEntry(null, "x"));
            Assert.Throws<ArgumentNullException>(() => new ToolbarEntry("x", null));
        }
    }
}
