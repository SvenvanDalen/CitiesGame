using System;
using Cities.Simulation.Services;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Services
{
    public class RecreationTests
    {
        [Test]
        public void StrengthFallsOffLinearlyToTheEdge()
        {
            var park = new RecreationSite(RecreationKind.Park, float2.zero, 100f, 50);

            Assert.That(park.StrengthAt(float2.zero), Is.EqualTo(1f));
            Assert.That(park.StrengthAt(new float2(50f, 0f)), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(park.StrengthAt(new float2(500f, 0f)), Is.Zero);
        }

        [Test]
        public void AnEmptyMapGivesNoBonus()
        {
            var map = new RecreationMap();

            Assert.That(map.LandValueBonus(float2.zero), Is.Zero);
            Assert.That(map.HappinessBonus(float2.zero), Is.Zero);
        }

        [Test]
        public void AParkRaisesLandValueAndHappinessNearby()
        {
            var map = new RecreationMap();
            map.Add(new RecreationSite(RecreationKind.Park, float2.zero, 100f, 50));

            Assert.That(map.LandValueBonus(float2.zero), Is.EqualTo(RecreationMap.ParkLandValue).Within(1e-5f));
            Assert.That(map.HappinessBonus(float2.zero), Is.EqualTo(RecreationMap.ParkHappiness).Within(1e-5f));
            Assert.That(map.LandValueBonus(new float2(60f, 0f)), Is.LessThan(map.LandValueBonus(float2.zero)));
            Assert.That(map.LandValueBonus(new float2(500f, 0f)), Is.Zero);
        }

        [Test]
        public void LandValueStacksWithDiminishingReturnsButHappinessCountsTheBestPerKind()
        {
            var map = new RecreationMap();
            map.Add(new RecreationSite(RecreationKind.Park, float2.zero, 100f, 50));
            map.Add(new RecreationSite(RecreationKind.Park, float2.zero, 100f, 50));
            map.Add(new RecreationSite(RecreationKind.Plaza, float2.zero, 100f, 20));

            Assert.That(map.LandValueBonus(float2.zero), Is.GreaterThan(RecreationMap.ParkLandValue).And.LessThan(1f));
            Assert.That(map.HappinessBonus(float2.zero), Is.EqualTo(RecreationMap.ParkHappiness + RecreationMap.PlazaHappiness).Within(1e-5f));
        }

        [Test]
        public void EveryKindHasItsOwnBonusAndUpkeepAddsUp()
        {
            var map = new RecreationMap();
            foreach (RecreationKind kind in Enum.GetValues(typeof(RecreationKind)))
            {
                map.Add(new RecreationSite(kind, float2.zero, 10f, 10));
            }

            Assert.That(RecreationMap.LandValueOf(RecreationKind.Playground), Is.EqualTo(RecreationMap.PlaygroundLandValue));
            Assert.That(RecreationMap.LandValueOf(RecreationKind.SportsField), Is.EqualTo(RecreationMap.SportsFieldLandValue));
            Assert.That(RecreationMap.HappinessOf(RecreationKind.Playground), Is.EqualTo(RecreationMap.PlaygroundHappiness));
            Assert.That(RecreationMap.HappinessOf(RecreationKind.SportsField), Is.EqualTo(RecreationMap.SportsFieldHappiness));
            Assert.That(map.UpkeepPerMonth, Is.EqualTo(40));
            Assert.That(map.Remove(map.Sites[0]), Is.True);
            Assert.That(map.UpkeepPerMonth, Is.EqualTo(30));
        }

        [Test]
        public void BadInputIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RecreationSite(RecreationKind.Park, float2.zero, 0f, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RecreationSite(RecreationKind.Park, float2.zero, 10f, -1));
            Assert.Throws<ArgumentNullException>(() => new RecreationMap().Add(null));
        }
    }
}
