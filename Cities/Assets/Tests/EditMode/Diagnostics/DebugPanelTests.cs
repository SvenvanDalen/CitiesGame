using Cities.Core.Diagnostics;
using Cities.Presentation.Debugging;
using Cities.Presentation.UI;
using Cities.Simulation.Scheduling;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Cities.Tests.EditMode.Diagnostics
{
    /// <summary>Bouwt UI Toolkit-elementen; draait daarom alleen in Unity.</summary>
    [Category("UnityRuntime")]
    public class DebugPanelTests
    {
        private DebugRegistry _registry;
        private DebugPanel _panel;

        [SetUp]
        public void SetUp()
        {
            _registry = new DebugRegistry();
            _panel = new DebugPanel(_registry);
        }

        [TearDown]
        public void TearDown() => _panel.Dispose();

        [Test]
        public void TimingsAreShownAgainstTheTickBudget()
        {
            var timings = new SystemTimings();
            timings.Record(SystemTimings.TickName, 1.5);
            timings.Record("Verkeer", 0.4);
            timings.Record("Burgers", 1.8);

            _panel.Refresh(timings, 10_000);

            // Budget bij 10.000 inwoners is 2 ms per tick.
            Assert.That(_panel.TimingText("Verkeer"), Is.EqualTo("0,40 ms (max 0,40 ms)"));
            Assert.That(_panel.TimingColor("Verkeer"), Is.EqualTo(MeterColor.Success));
            Assert.That(_panel.TimingColor("Burgers"), Is.EqualTo(MeterColor.Warning));
            Assert.That(_panel.TimingColor(SystemTimings.TickName), Is.EqualTo(MeterColor.Success));
            Assert.That(_panel.Q<Label>("budget").text, Does.Contain("2,00 ms"));

            timings.Record("Burgers", 5.0);
            _panel.Refresh(timings, 10_000);
            Assert.That(_panel.TimingColor("Burgers"), Is.EqualTo(MeterColor.Danger));
        }

        [Test]
        public void WithoutMeasurementsTheEmptyHintShows()
        {
            _panel.Refresh(null, 0);
            _panel.Refresh(new SystemTimings(), 0);

            Assert.That(_panel.Q<Label>("empty").text, Is.EqualTo(DebugPanelText.NoSystems));
            Assert.That(_panel.Q("systems").childCount, Is.EqualTo(1));
        }

        [Test]
        public void CountersAndOverlayTogglesFollowTheRegistry()
        {
            var agents = 1250000L;
            _registry.AddCounter("Simulatie", "Agents", () => agents);
            _registry.AddOverlay(new Cities.World.Grid.CellGridOverlay(Cities.World.Grid.WorldDimensions.Default, Cities.World.Terrain.FlatTerrain.Zero));

            _panel.Refresh(null, 0);

            var counters = _panel.Q("counters");
            Assert.That(counters.Q<Label>("value").text, Is.EqualTo("1.250.000"));
            agents = 3;
            _panel.Refresh(null, 0);
            Assert.That(counters.Q<Label>("value").text, Is.EqualTo("3"));

            Assert.That(_panel.Q<Toggle>("Cellen").value, Is.False);

            // Aanzetten via de sneltoets (registry) zet de schakelaar na de volgende verversing ook aan.
            _registry.SetEnabled("Cellen", true);
            _panel.Refresh(null, 0);
            Assert.That(_panel.Q<Toggle>("Cellen").value, Is.True);
        }
    }
}
