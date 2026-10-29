using Cities.Gameplay.Controls;
using Cities.Presentation.Settings;
using Cities.Presentation.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Cities.Tests.EditMode.Controls
{
    /// <summary>Bouwt UI Toolkit-elementen; draait daarom alleen in Unity.</summary>
    [Category("UnityRuntime")]
    public class ControlsSettingsPanelTests
    {
        private FakeControlBindings _bindings;
        private ControlsSettingsPanel _panel;

        [SetUp]
        public void SetUp()
        {
            _bindings = new FakeControlBindings();
            _panel = new ControlsSettingsPanel(_bindings);
        }

        [TearDown]
        public void TearDown() => _panel.Dispose();

        [Test]
        public void ShowsEveryRowWithItsKeysAndCategoryHeadings()
        {
            Assert.That(_panel.Query(className: UiClasses.Keycap).ToList().Count, Is.EqualTo(_bindings.Rows.Count * 2));
            Assert.That(_panel.GetKeyButton("Camera/Move/up", 0).text, Is.EqualTo("W"));
            Assert.That(_panel.GetKeyButton("Camera/Rotate/negative", 1).text, Is.EqualTo("—"));
            Assert.That(_panel.Query<Label>(className: UiClasses.SettingsSection).ToList().ConvertAll(l => l.text),
                Is.EqualTo(new[] { ControlLabels.CategoryCamera, ControlLabels.CategorySimulation, ControlLabels.CategoryGeneral }));
            Assert.That(_panel.GetKeyButton("Game/Unknown", 0), Is.Null);
        }

        [Test]
        public void FixedKeyIsDisabled()
        {
            Assert.That(_panel.GetKeyButton("Game/Menu", 0).enabledSelf, Is.False);
            Assert.That(_panel.GetKeyButton("Game/Menu", 0).tooltip, Is.EqualTo(ControlsSettingsText.FixedTooltip));
            Assert.That(_panel.GetKeyButton("Game/Menu", 1).enabledSelf, Is.True);
        }

        [Test]
        public void RebindingShowsWaitingStateThenTheNewKey()
        {
            var slot = new SlotRef(ControlTestData.Row(_bindings.Rows, "Camera/Move/up"), 0);

            _panel.BeginRebind(slot);

            var key = _panel.GetKeyButton("Camera/Move/up", 0);
            Assert.That(key.text, Is.EqualTo(ControlsSettingsText.Waiting));
            Assert.That(key.ClassListContains(UiClasses.KeycapListening), Is.True);
            Assert.That(_panel.GetKeyButton("Game/Pause", 0).enabledSelf, Is.False, "Andere toetsen uit tijdens wachten");

            _bindings.Press("<Keyboard>/space");

            Assert.That(key.text, Is.EqualTo("space"));
            Assert.That(key.ClassListContains(UiClasses.KeycapListening), Is.False);
            Assert.That(key.ClassListContains(UiClasses.KeycapCustom), Is.True);
            Assert.That(_panel.GetKeyButton("Game/Pause", 0).text, Is.EqualTo("W"));
            Assert.That(_panel.StatusText, Does.Contain("Pauze aan of uit"));
        }

        [Test]
        public void ResetButtonsAppearForCustomizedRows()
        {
            var resetAll = _panel.Q<CityButton>("reset-all");
            var row = _panel.Q("Camera/Move/up");
            Assert.That(resetAll.enabledSelf, Is.False);
            Assert.That(row.Q<CityButton>("reset").style.visibility.value, Is.EqualTo(Visibility.Hidden));

            _panel.BeginRebind(new SlotRef(ControlTestData.Row(_bindings.Rows, "Camera/Move/up"), 0));
            _bindings.Press("<Keyboard>/i");

            Assert.That(resetAll.enabledSelf, Is.True);
            Assert.That(row.Q<CityButton>("reset").style.visibility.value, Is.EqualTo(Visibility.Visible));

            _bindings.ResetAll();

            Assert.That(_panel.GetKeyButton("Camera/Move/up", 0).text, Is.EqualTo("W"));
            Assert.That(resetAll.enabledSelf, Is.False);
        }

        [Test]
        public void DoneCancelsAndRequestsClose()
        {
            var closed = false;
            _panel.CloseRequested += () => closed = true;
            _panel.BeginRebind(new SlotRef(ControlTestData.Row(_bindings.Rows, "Game/Pause"), 1));

            _panel.Done();

            Assert.That(closed, Is.True);
            Assert.That(_bindings.IsRebinding, Is.False);
            Assert.That(_panel.StatusText, Is.EqualTo(ControlsSettingsText.Canceled));
        }
    }
}
