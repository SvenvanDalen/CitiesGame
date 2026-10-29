using Cities.Gameplay.Controls;
using Cities.Presentation.Settings;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Controls
{
    public class ControlsSettingsTextTests
    {
        private FakeControlBindings _bindings;

        [SetUp]
        public void SetUp() => _bindings = new FakeControlBindings();

        [Test]
        public void CompletedWithoutConflict()
        {
            var outcome = Rebind("Camera/Move/up", 0, "<Keyboard>/i");

            Assert.That(Text(outcome), Is.EqualTo("‘Vooruit’ gebruikt nu I."));
        }

        [Test]
        public void CompletedWithSwapNamesTheOtherAction()
        {
            var outcome = Rebind("Camera/Move/up", 0, "<Keyboard>/space");

            Assert.That(Text(outcome), Is.EqualTo("‘Vooruit’ gebruikt nu space. Die toets hoorde bij ‘Pauze aan of uit’; dat heeft nu W."));
        }

        [Test]
        public void SwapFromAnEmptySlotSaysTheOtherHasNoKey()
        {
            var outcome = Rebind("Camera/Rotate/negative", 1, "<Keyboard>/q");

            Assert.That(Text(outcome), Does.EndWith("dat heeft nu geen toets meer."));
        }

        [Test]
        public void UnchangedAndCanceled()
        {
            Assert.That(Text(Rebind("Camera/Move/up", 0, "<Keyboard>/w")), Is.EqualTo("‘Vooruit’ houdt W."));

            RebindOutcome canceled = default;
            _bindings.StartRebind(Slot("Camera/Move/up", 0), o => canceled = o);
            _bindings.CancelRebind();
            Assert.That(Text(canceled), Is.EqualTo(ControlsSettingsText.Canceled));
        }

        [Test]
        public void ListeningAndResetMessagesUseTheLabel()
        {
            Assert.That(ControlsSettingsText.Listening("Vooruit"), Does.Contain("‘Vooruit’"));
            Assert.That(ControlsSettingsText.RowReset("Vooruit"), Does.StartWith("‘Vooruit’"));
        }

        private RebindOutcome Rebind(string key, int slot, string path)
        {
            RebindOutcome result = default;
            _bindings.StartRebind(Slot(key, slot), o => result = o);
            _bindings.Press(path);
            return result;
        }

        private string Text(RebindOutcome outcome) => ControlsSettingsText.Outcome(outcome, _bindings.GetDisplayName);

        private SlotRef Slot(string key, int slot) => new SlotRef(ControlTestData.Row(_bindings.Rows, key), slot);
    }
}
