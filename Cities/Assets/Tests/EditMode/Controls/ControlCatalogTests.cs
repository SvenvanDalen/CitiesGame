using System.Linq;
using Cities.Gameplay.Controls;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Controls
{
    public class ControlCatalogTests
    {
        [Test]
        public void GroupsCompositePartsIntoRowsWithPrimaryAndAlternative()
        {
            var rows = ControlCatalog.Build(ControlTestData.Bindings());

            var up = ControlTestData.Row(rows, "Camera/Move/up");

            Assert.That(up.Label, Is.EqualTo("Vooruit"));
            Assert.That(up.Category, Is.EqualTo(ControlLabels.CategoryCamera));
            Assert.That(up.Slots.Select(s => s.Binding.DefaultPath), Is.EqualTo(new[] { "<Keyboard>/w", "<Keyboard>/upArrow" }));
            Assert.That(up.Map, Is.EqualTo("Camera"));
            Assert.That(up.Action, Is.EqualTo("Move"));
        }

        [Test]
        public void LeavesOutAxesPositionsAndCompositeHeaders()
        {
            var rows = ControlCatalog.Build(ControlTestData.Bindings());

            Assert.That(rows.Select(r => r.Key), Has.None.StartsWith("Camera/Scroll").And.None.StartsWith("Camera/Pointer"));
            Assert.That(rows.SelectMany(r => r.Slots), Has.None.Matches<ControlSlot>(s => s.Binding.Kind == BindingKind.Composite));
        }

        [Test]
        public void OrdersRowsLikeTheLabelTable()
        {
            var rows = ControlCatalog.Build(ControlTestData.Bindings());

            Assert.That(rows.Select(r => r.Key), Is.EqualTo(new[]
            {
                "Camera/Move/up", "Camera/Move/down", "Camera/Move/left", "Camera/Move/right",
                "Camera/Rotate/negative", "Camera/Rotate/positive", "Camera/OrbitDrag",
                "Game/Pause", "Game/Menu",
            }));
        }

        [Test]
        public void EscapeOnTheMenuIsShownButFixed()
        {
            var menu = ControlTestData.Row(ControlCatalog.Build(ControlTestData.Bindings()), "Game/Menu");

            Assert.That(menu.Slots[0].Rebindable, Is.False);
            Assert.That(menu.Slots[1].Rebindable, Is.True);
        }

        [Test]
        public void UnlabeledRowsGoLastWithTheirKey()
        {
            var bindings = ControlTestData.Bindings();
            bindings.Insert(0, new BindingInfo("Game", "Screenshot", 0, null, "<Keyboard>/f12", BindingKind.Simple, true));

            var rows = ControlCatalog.Build(bindings);

            Assert.That(rows[rows.Count - 1].Key, Is.EqualTo("Game/Screenshot"));
            Assert.That(rows[rows.Count - 1].Label, Is.EqualTo("Game/Screenshot"));
            Assert.That(ControlCatalog.FindUnlabeled(rows), Is.EqualTo(new[] { "Game/Screenshot" }));
        }

        [Test]
        public void LabelLookup()
        {
            Assert.That(ControlLabels.TryGet("Game/Pause", out var label, out var order), Is.True);
            Assert.That(label.Category, Is.EqualTo(ControlLabels.CategorySimulation));
            Assert.That(ControlLabels.All[order].Key, Is.EqualTo("Game/Pause"));
            Assert.That(ControlLabels.TryGet("Game/Unknown", out _, out var missing), Is.False);
            Assert.That(missing, Is.EqualTo(int.MaxValue));
            Assert.That(ControlLabels.All.Select(l => l.Key), Is.Unique);
        }

        [Test]
        public void RowKeyIncludesThePartOfComposites()
        {
            Assert.That(new BindingInfo("Camera", "Tilt", 1, "positive", "<Keyboard>/r", BindingKind.CompositePart, false).RowKey, Is.EqualTo("Camera/Tilt/positive"));
            Assert.That(new BindingInfo("Game", "Pause", 0, null, null, BindingKind.Simple, true).RowKey, Is.EqualTo("Game/Pause"));
            Assert.That(new BindingInfo("Game", "Pause", 0, null, null, BindingKind.Simple, true).DefaultPath, Is.Empty);
        }
    }
}
