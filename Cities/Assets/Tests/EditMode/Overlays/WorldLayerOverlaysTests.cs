using System.Linq;
using Cities.Gameplay.Overlays;
using Cities.World.Grid;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Overlays
{
    /// <summary>Gebruikt NativeArray-lagen en draait dus alleen in Unity.</summary>
    [Category("UnityRuntime")]
    public class WorldLayerOverlaysTests
    {
        [Test]
        public void SourcesReadTheLayersOfTheMap()
        {
            using (var map = new WorldMap(new WorldDimensions(1, 1, 8, 8f), 20f))
            {
                map.LandValue[1, 1] = 250f;
                map.GroundPollution[0, 0] = 0.5f;
                map.WaterDepth[2, 3] = 4f;
                map.Zones[1, 2] = new ZoneCell(ZoneType.Commercial, ZoneDensity.High);
                var sources = WorldLayerOverlays.Create(map).ToDictionary(s => s.Id);

                Assert.That(sources.Keys, Is.EquivalentTo(new[]
                {
                    WorldLayerOverlays.HeightId,
                    WorldLayerOverlays.LandValueId,
                    WorldLayerOverlays.GroundPollutionId,
                    WorldLayerOverlays.WaterDepthId,
                    WorldLayerOverlays.ZonesId,
                }));
                var height = (ICellOverlaySource)sources[WorldLayerOverlays.HeightId];
                Assert.That(height.CellsX, Is.EqualTo(8));
                Assert.That(height.Value(3, 3), Is.EqualTo(20f).Within(1e-4f));
                Assert.That(((ICellOverlaySource)sources[WorldLayerOverlays.LandValueId]).Value(5, 5), Is.EqualTo(250f), "blok 1,1 van 4×4 cellen");
                Assert.That(((ICellOverlaySource)sources[WorldLayerOverlays.GroundPollutionId]).Value(0, 3), Is.EqualTo(50f));
                Assert.That(((ICellOverlaySource)sources[WorldLayerOverlays.WaterDepthId]).Value(2, 3), Is.EqualTo(4f));
                var zones = (ICellOverlaySource)sources[WorldLayerOverlays.ZonesId];
                Assert.That(zones.Value(1, 2), Is.EqualTo(2f));
                Assert.That(zones.Value(0, 0), Is.NaN, "zonder zone geen gegevens");
            }
        }

        [Test]
        public void NullMapIsRejected() =>
            Assert.Throws<System.ArgumentNullException>(() => WorldLayerOverlays.Create(null));
    }
}
