using System.Linq;
using Cities.Gameplay.Tools;
using Cities.Simulation.Network;
using Cities.World.Grid;
using Cities.World.Picking;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Tools
{
    /// <summary>Het wegennet en de kaart staan in NativeLists/NativeArrays; draait daarom alleen in Unity.</summary>
    [Category("UnityRuntime")]
    public class RoadToolTests
    {
        private static readonly WorldDimensions Dimensions = new WorldDimensions(1, 1, 64, 8f);

        private RoadNetwork _network;
        private WorldMap _map;
        private RoadPreviewOverlay _overlay;
        private RoadTool _tool;

        [SetUp]
        public void SetUp()
        {
            _network = new RoadNetwork(8);
            _map = new WorldMap(Dimensions, 10f);
            _overlay = new RoadPreviewOverlay();
            _tool = new RoadTool(_network, _map, _overlay);
        }

        [TearDown]
        public void TearDown()
        {
            _map.Dispose();
            _network.Dispose();
        }

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

        [Test]
        public void ThePreviewShowsWhatTheChosenTypeCostsForTheLength()
        {
            _tool.Use("road.test", new RoadTypeSpec { CarLanes = 1, Sidewalks = true, SpeedLimitKmh = 50f, CostPerMeter = 500 });
            Click(100f, 100f);

            var preview = _tool.UpdatePreview(At(100f, 150f));

            Assert.That(preview.Cost, Is.EqualTo(25_000));
            Assert.That(_tool.TypeId, Is.EqualTo("road.test"));
            Assert.That(_tool.UpdatePreview(At(100f, 100f)).Cost, Is.EqualTo(0), "een marker kost niets");
        }

        [Test]
        public void TwoClicksLayAStraightRoadWithLengthAndAngleInTheInfo()
        {
            Click(100f, 100f);
            var preview = _tool.UpdatePreview(At(100f, 152f));

            Assert.That(preview.Info, Does.StartWith("50").And.Contain(" m · 0° · 0.0 m (0.0 %)"), "gesnapt op 5 m");
            _tool.Apply(preview);

            Assert.That(_network.SegmentCount, Is.EqualTo(1));
            Assert.That(_network.NodeCount, Is.EqualTo(2));
            Assert.That(_network.GetSegment(_network.Segments().Single()).Length, Is.EqualTo(50f).Within(1e-2f));
            Assert.That(_tool.IsDrawing, Is.False);
        }

        [Test]
        public void TheEndSnapsToExistingNodesAndTheStartContinuesFromThem()
        {
            Click(100f, 100f);
            Click(100f, 150f);
            Click(103f, 98f);
            var preview = _tool.UpdatePreview(At(200f, 150f));
            _tool.Apply(preview);

            Assert.That(_network.NodeCount, Is.EqualTo(3), "begin hergebruikt het knooppunt");
            Assert.That(_network.SegmentCount, Is.EqualTo(2));

            // Het eind op een bestaand knooppunt (andere kant) legt geen nieuw knooppunt aan.
            Click(100f, 150f);
            Assert.That(_tool.IsDrawing, Is.True);
            var close = _tool.UpdatePreview(At(200f, 148f));
            _tool.Apply(close);
            Assert.That(_network.NodeCount, Is.EqualTo(3));
        }

        [Test]
        public void ACurveNeedsAControlPoint()
        {
            _tool.Mode = RoadDrawMode.Curve;
            Click(100f, 100f);
            var guide = _tool.UpdatePreview(At(100f, 160f));
            _tool.Apply(guide);
            Assert.That(_network.SegmentCount, Is.EqualTo(0), "het buigpunt legt nog niets aan");
            Assert.That(guide.Info, Does.StartWith("Buigpunt"));

            var preview = _tool.UpdatePreview(At(160f, 160f));
            _tool.Apply(preview);

            var segment = _network.GetSegment(_network.Segments().Single());
            Assert.That(segment.Length, Is.GreaterThan(85f).And.LessThan(120f), "een boog ligt tussen de koorde en de twee benen");
            Assert.That(segment.Curve.Direction(0f).z, Is.GreaterThan(0.99f));
            Assert.That(segment.Curve.Direction(1f).x, Is.GreaterThan(0.99f));
        }

        [Test]
        public void FreeModeChainsSegmentsUntilCancelled()
        {
            _tool.Mode = RoadDrawMode.Free;
            Click(100f, 100f);
            Click(100f, 150f);
            Click(150f, 150f);

            Assert.That(_network.SegmentCount, Is.EqualTo(2));
            Assert.That(_network.NodeCount, Is.EqualTo(3));
            Assert.That(_tool.IsDrawing, Is.True);
            Assert.That(_tool.CancelStep(), Is.True);
            Assert.That(_tool.IsDrawing, Is.False);
            Assert.That(_tool.CancelStep(), Is.False);
        }

        [Test]
        public void InvalidSpotsGiveAReason()
        {
            Assert.That(_tool.UpdatePreview(At(-5f, 100f)).InvalidReason, Is.EqualTo("Buiten de kaart"));

            Click(100f, 100f);
            Assert.That(_tool.UpdatePreview(At(104f, 100f)).InvalidReason, Is.EqualTo("Te kort").Or.EqualTo("Begin en eind vallen samen"));
            Assert.That(_tool.UpdatePreview(At(100f, 2000f)).InvalidReason, Is.Not.Null);
            Assert.That(_tool.UpdatePreview(new ToolInput(new PickResult(PickHit.None, PickHit.None))).IsVisible, Is.False);
        }

        [Test]
        public void TheOverlayDrawsThePlannedRoadInGreenOrRed()
        {
            Click(100f, 100f);
            _tool.UpdatePreview(At(100f, 160f));
            var geometry = new Cities.Core.Diagnostics.DebugGeometry();
            _overlay.Collect(geometry, new Cities.Core.Diagnostics.DebugView(float3.zero, float3.zero, 1000f));
            Assert.That(geometry.Lines.All(l => l.Color == Cities.Core.Diagnostics.DebugColor.Good), Is.True);

            _tool.UpdatePreview(At(100f, 102f));
            geometry = new Cities.Core.Diagnostics.DebugGeometry();
            _overlay.Collect(geometry, new Cities.Core.Diagnostics.DebugView(float3.zero, float3.zero, 1000f));
            Assert.That(geometry.Lines.All(l => l.Color == Cities.Core.Diagnostics.DebugColor.Bad), Is.True);

            _tool.Deactivate();
            geometry = new Cities.Core.Diagnostics.DebugGeometry();
            _overlay.Collect(geometry, new Cities.Core.Diagnostics.DebugView(float3.zero, float3.zero, 1000f));
            Assert.That(geometry.Lines, Is.Empty);
        }
    }
}
