using Cities.Gameplay.Tools;
using Cities.Simulation.Network;
using Cities.World.Picking;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Tools
{
    /// <summary>Het wegennet staat in NativeLists; draait daarom alleen in Unity.</summary>
    [Category("UnityRuntime")]
    public class DemolishToolTests
    {
        private RoadNetwork _network;
        private DemolishTool _tool;
        private Demolition? _last;

        [SetUp]
        public void SetUp()
        {
            _network = new RoadNetwork(8);
            _tool = new DemolishTool(_network, new RoadPreviewOverlay());
            _tool.Demolished += d => _last = d;
            _last = null;
        }

        [TearDown]
        public void TearDown() => _network.Dispose();

        private static ToolInput At(float x, float z) =>
            new ToolInput(new PickResult(PickHit.None, new PickHit(PickKind.Terrain, 0, new float3(x, 10f, z), 100f)));

        private ToolPreview Click(float x, float z)
        {
            var preview = _tool.UpdatePreview(At(x, z));
            if (preview.IsValid)
            {
                _tool.Apply(preview);
            }

            return preview;
        }

        /// <summary>Een kruispunt op (100, 100) met drie armen van 100 m.</summary>
        private void Cross()
        {
            var middle = _network.AddNode(new float3(100f, 10f, 100f));
            foreach (var end in new[] { new float3(200f, 10f, 100f), new float3(100f, 10f, 200f), new float3(0f, 10f, 100f) })
            {
                _network.AddSegment(middle, _network.AddNode(end), Bezier3.Straight(new float3(100f, 10f, 100f), end), LaneProfiles.Road(1, false, true), 13f);
            }
        }

        [Test]
        public void ASegmentNeedsTwoClicksToBeDemolished()
        {
            Cross();

            var first = Click(150f, 101f);
            Assert.That(first.Info, Does.Contain("klik om te kiezen").And.Contain("100 m"));
            Assert.That(_network.SegmentCount, Is.EqualTo(3));

            var second = _tool.UpdatePreview(At(150f, 101f));
            Assert.That(second.Info, Does.Contain("klik nogmaals om te slopen"));
            _tool.Apply(second);

            Assert.That(_network.SegmentCount, Is.EqualTo(2));
            Assert.That(_last.Value.Segments, Is.EqualTo(1));
            Assert.That(_last.Value.Length, Is.EqualTo(100f).Within(0.1f));
            Assert.That(_tool.IsSelected, Is.False);
        }

        [Test]
        public void ANodeTakesAllItsRoadsWithIt()
        {
            Cross();

            Click(102f, 99f);
            var preview = _tool.UpdatePreview(At(102f, 99f));
            Assert.That(preview.Info, Does.Contain("Kruispunt · 3 wegen"));
            _tool.Apply(preview);

            Assert.That(_network.SegmentCount, Is.EqualTo(0));
            Assert.That(_network.NodeCount, Is.EqualTo(0), "losse knooppunten verdwijnen mee");
            Assert.That(_last.Value.Segments, Is.EqualTo(3));
            Assert.That(_last.Value.Length, Is.EqualTo(300f).Within(0.1f));
        }

        [Test]
        public void ChoosingSomethingElseOrCancellingDropsTheSelection()
        {
            Cross();

            Click(150f, 101f);
            Click(100f, 150f);
            Assert.That(_network.SegmentCount, Is.EqualTo(3), "een andere weg is een nieuwe keuze, geen bevestiging");
            Assert.That(_tool.IsSelected, Is.True);
            Assert.That(_tool.CancelStep(), Is.True);
            Assert.That(_tool.IsSelected, Is.False);
            Assert.That(_tool.CancelStep(), Is.False);
        }

        [Test]
        public void EmptyGroundShowsNothingAndDoesNothing()
        {
            Cross();

            var preview = Click(60f, 160f);

            Assert.That(preview.IsVisible, Is.False);
            Assert.That(_network.SegmentCount, Is.EqualTo(3));
            Assert.Throws<System.ArgumentNullException>(() => new DemolishTool(null, new RoadPreviewOverlay()));
            Assert.Throws<System.ArgumentNullException>(() => new DemolishTool(_network, null));
        }
    }
}
