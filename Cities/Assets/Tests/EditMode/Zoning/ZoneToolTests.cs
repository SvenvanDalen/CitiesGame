using Cities.Gameplay.Tools;
using Cities.Gameplay.Zoning;
using Cities.Simulation.Network;
using Cities.World.Grid;
using Cities.World.Picking;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Zoning
{
    /// <summary>Wegennet en kaart staan in native geheugen; draait daarom alleen in Unity.</summary>
    [Category("UnityRuntime")]
    public class ZoneToolTests
    {
        private static readonly WorldDimensions World = new WorldDimensions(2, 2, 16, 8f);

        private WorldMap _map;
        private RoadNetwork _network;
        private ZoneTool _tool;

        [SetUp]
        public void SetUp()
        {
            _map = new WorldMap(World);
            _network = new RoadNetwork(8);
            var a = _network.AddNode(new float3(128f, 0f, 0f));
            var b = _network.AddNode(new float3(128f, 0f, 255f));
            _network.AddSegment(a, b, Bezier3.Straight(new float3(128f, 0f, 0f), new float3(128f, 0f, 255f)), LaneProfiles.Road(1, bikeLanes: false, sidewalks: true), 13.9f);
            _tool = new ZoneTool(new ZoneService(_map, _network), _map, new ZonePreviewOverlay(_map));
        }

        [TearDown]
        public void TearDown()
        {
            _network.Dispose();
            _map.Dispose();
        }

        [Test]
        public void ARectangleNeedsTwoClicksAndOnlyZonesAllowedCells()
        {
            _tool.Mode = ZoneMode.Rectangle;

            var first = Click(new float3(18f * 8f + 4f, 0f, 20f * 8f + 4f));
            Assert.That(_map.Zones.GetForCell(new CellCoord(18, 20)).IsZoned, Is.False, "eerste klik zet alleen de hoek");
            var second = Click(new float3(20f * 8f + 4f, 0f, 21f * 8f + 4f));

            Assert.That(first.InvalidReason, Is.Null);
            Assert.That(second.Info, Is.EqualTo("6 cellen"));
            Assert.That(_map.Zones.GetForCell(new CellCoord(20, 21)).Type, Is.EqualTo(ZoneType.Residential));
        }

        [Test]
        public void FillFollowsTheRoadAndEraseRemovesTheZone()
        {
            _tool.Mode = ZoneMode.Fill;
            _tool.Type = ZoneType.Commercial;
            Click(new float3(18f * 8f + 4f, 0f, 10f * 8f + 4f));

            Assert.That(_map.Zones.GetForCell(new CellCoord(18, 10)).Type, Is.EqualTo(ZoneType.Commercial));
            Assert.That(_map.Zones.GetForCell(new CellCoord(19, 25)).Type, Is.EqualTo(ZoneType.Commercial), "langs de hele weg");
            Assert.That(_map.Zones.GetForCell(new CellCoord(30, 10)).IsZoned, Is.False, "te ver van de weg");

            _tool.Erase = true;
            Click(new float3(18f * 8f + 4f, 0f, 10f * 8f + 4f));

            Assert.That(_map.Zones.GetForCell(new CellCoord(18, 10)).IsZoned, Is.False);
        }

        [Test]
        public void OutsideTheMapIsRefused()
        {
            var preview = _tool.UpdatePreview(new ToolInput(Ground(new float3(-50f, 0f, 10f))));

            Assert.That(preview.InvalidReason, Is.EqualTo("Buiten de kaart"));
        }

        private ToolPreview Click(float3 point)
        {
            var preview = _tool.UpdatePreview(new ToolInput(Ground(point)));
            _tool.Apply(preview);
            return preview;
        }

        private static PickResult Ground(float3 point)
        {
            var ground = new PickHit(PickKind.Terrain, 0, point, 100f);
            return new PickResult(ground, ground);
        }
    }
}
