using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Gameplay.Overlays;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Overlays
{
    public sealed class EconomyOverlaysTests
    {
        private static void AddOne(List<OverlayEntityValue> output) =>
            output.Add(new OverlayEntityValue(1, float3.zero, float3.zero, 10f, 50f));

        [Test]
        public void NoSourcesMakesNoOverlays()
        {
            Assert.That(EconomyOverlays.Create(4, 4, new EconomySources()), Is.Empty);
            Assert.Throws<ArgumentNullException>(() => EconomyOverlays.Create(4, 4, null));
        }

        [Test]
        public void EveryFilledSourceMakesAnOverlay()
        {
            var all = new EconomySources
            {
                ResourceRichness = (x, z) => 0.5f,
                District = (x, z) => 1f,
                TransitCoverage = (x, z) => 1f,
                Tourism = AddOne,
                Trade = AddOne,
            };

            var overlays = EconomyOverlays.Create(4, 4, all);

            Assert.That(overlays.Select(s => s.Id), Is.EquivalentTo(new[]
            {
                EconomyOverlays.ResourcesId, EconomyOverlays.DistrictsId, EconomyOverlays.TransitId, EconomyOverlays.TourismId, EconomyOverlays.TradeId,
            }));
            Assert.That(overlays.Count(s => s.Target == OverlayTarget.Cell), Is.EqualTo(3));
            Assert.That(overlays.Count(s => s.Target == OverlayTarget.Building), Is.EqualTo(2));
        }

        [Test]
        public void ResourcesAndTransitBecomePercentagesAndAreClamped()
        {
            var sources = new EconomySources { ResourceRichness = (x, z) => x == 0 ? 0.25f : 3f, TransitCoverage = (x, z) => -1f };
            var overlays = EconomyOverlays.Create(4, 4, sources).Cast<ICellOverlaySource>().ToDictionary(s => s.Id);

            Assert.That(overlays[EconomyOverlays.ResourcesId].Value(0, 0), Is.EqualTo(25f).Within(0.01f));
            Assert.That(overlays[EconomyOverlays.ResourcesId].Value(1, 0), Is.EqualTo(100f));
            Assert.That(overlays[EconomyOverlays.TransitId].Value(0, 0), Is.Zero);
        }

        [Test]
        public void CellsWithoutADistrictStayEmptyAndHighNumbersAreCapped()
        {
            var sources = new EconomySources
            {
                MaxDistrict = 4,
                District = (x, z) => x switch { 0 => 0f, 1 => 2f, 2 => 9f, _ => float.NaN },
            };
            var districts = (ICellOverlaySource)EconomyOverlays.Create(4, 4, sources).Single();

            Assert.That(float.IsNaN(districts.Value(0, 0)), Is.True);
            Assert.That(districts.Value(1, 0), Is.EqualTo(2f));
            Assert.That(districts.Value(2, 0), Is.EqualTo(4f));
            Assert.That(float.IsNaN(districts.Value(3, 0)), Is.True);
        }
    }
}
