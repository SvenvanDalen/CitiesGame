using System;
using System.Collections.Generic;
using Cities.Gameplay.Controls;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Controls
{
    public class RebindPlannerTests
    {
        private IReadOnlyList<ControlRow> _rows;
        private PathTable _paths;

        [SetUp]
        public void SetUp()
        {
            _rows = ControlCatalog.Build(ControlTestData.Bindings());
            _paths = new PathTable();
        }

        [Test]
        public void FreeKeyOnlyChangesTheTarget()
        {
            var target = Slot("Camera/Move/up", 0);

            var changes = Plan(target, "<Keyboard>/i");

            Assert.That(changes, Has.Count.EqualTo(1));
            Assert.That(changes[0].Path, Is.EqualTo("<Keyboard>/i"));
            Assert.That(changes[0].Swapped, Is.False);
            Assert.That(changes[0].IsDefault, Is.False);
        }

        [Test]
        public void KeyInUseSwapsWithTheOtherAction()
        {
            var changes = Plan(Slot("Camera/Move/up", 0), "<Keyboard>/space");
            _paths.Apply(changes);

            Assert.That(changes, Has.Count.EqualTo(2));
            Assert.That(changes[1].Swapped, Is.True);
            Assert.That(_paths.Get(Slot("Camera/Move/up", 0).Value.Binding), Is.EqualTo("<Keyboard>/space"));
            Assert.That(_paths.Get(Slot("Game/Pause", 0).Value.Binding), Is.EqualTo("<Keyboard>/w"));
        }

        [Test]
        public void SwappingBackRestoresDefaults()
        {
            _paths.Apply(Plan(Slot("Camera/Move/up", 0), "<Keyboard>/space"));

            var back = Plan(Slot("Camera/Move/up", 0), "<Keyboard>/w");

            Assert.That(back, Has.All.Matches<BindingChange>(c => c.IsDefault));
        }

        [Test]
        public void KeyFromAnEmptySlotLeavesTheOtherEmpty()
        {
            // Q zit op 'tegen de klok in' (primair); het alternatieve slot is leeg.
            var changes = Plan(Slot("Camera/Rotate/negative", 1), "<Keyboard>/q");
            _paths.Apply(changes);

            Assert.That(_paths.Get(Slot("Camera/Rotate/negative", 1).Value.Binding), Is.EqualTo("<Keyboard>/q"));
            Assert.That(_paths.Get(Slot("Camera/Rotate/negative", 0).Value.Binding), Is.Empty);
            Assert.That(changes[1].Swapped, Is.True);
        }

        [Test]
        public void SameKeyIsNoChange()
        {
            Assert.That(Plan(Slot("Camera/Move/up", 0), "<keyboard>/W"), Is.Empty);
        }

        [Test]
        public void ClearingASlotNeverConflicts()
        {
            var changes = Plan(Slot("Camera/OrbitDrag", 0), string.Empty);

            Assert.That(changes, Has.Count.EqualTo(1));
            Assert.That(changes[0].Path, Is.Empty);
            Assert.That(Plan(Slot("Camera/OrbitDrag", 1), null), Is.Empty, "Al leeg");
        }

        [Test]
        public void FixedSlotsAreNotConflictsAndCannotBeTargets()
        {
            var conflict = RebindPlanner.FindConflict(_rows, _paths.Get, default, "<Keyboard>/escape");

            Assert.That(conflict.IsValid, Is.False);
            Assert.Throws<ArgumentException>(() => Plan(Slot("Game/Menu", 0), "<Keyboard>/m"));
            Assert.Throws<ArgumentException>(() => Plan(default, "<Keyboard>/m"));
        }

        [Test]
        public void ArgumentsAreChecked()
        {
            var target = Slot("Camera/Move/up", 0);

            Assert.Throws<ArgumentNullException>(() => RebindPlanner.Plan(null, _paths.Get, target, "<Keyboard>/i"));
            Assert.Throws<ArgumentNullException>(() => RebindPlanner.Plan(_rows, null, target, "<Keyboard>/i"));
        }

        [Test]
        public void LocateFindsTheRowAndSlotOfABinding()
        {
            var pause = Slot("Game/Pause", 1);

            var found = RebindPlanner.Locate(_rows, pause.Value.Binding);

            Assert.That(found.Row, Is.SameAs(pause.Row));
            Assert.That(found.Slot, Is.EqualTo(1));
            Assert.That(RebindPlanner.Locate(_rows, new BindingInfo("Game", "Pause", 9, null, null, BindingKind.Simple, true)).IsValid, Is.False);
        }

        private IReadOnlyList<BindingChange> Plan(SlotRef target, string path) => RebindPlanner.Plan(_rows, _paths.Get, target, path);

        private SlotRef Slot(string key, int slot) => new SlotRef(ControlTestData.Row(_rows, key), slot);
    }
}
