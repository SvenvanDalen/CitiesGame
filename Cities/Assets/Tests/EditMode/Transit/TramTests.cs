using System;
using Cities.Simulation.Transit;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Transit
{
    public class TramTests
    {
        [Test]
        public void OnTheRoadTheTramIsStuckBehindTrafficButBesideItRunsFree()
        {
            Assert.That(Tram.SpeedOn(TrackPlacement.OnRoad, 5f), Is.EqualTo(5f));
            Assert.That(Tram.SpeedOn(TrackPlacement.OnRoad, 30f), Is.EqualTo(Tram.TopSpeed));
            Assert.That(Tram.SpeedOn(TrackPlacement.BesideRoad, 5f), Is.EqualTo(Tram.TopSpeed));
            Assert.Throws<ArgumentOutOfRangeException>(() => Tram.SpeedOn(TrackPlacement.OnRoad, -1f));
        }

        [Test]
        public void StopsMustBeFarEnoughApart()
        {
            Assert.That(Tram.IsStopSpacingOk(float2.zero, new float2(299f, 0f)), Is.False);
            Assert.That(Tram.IsStopSpacingOk(float2.zero, new float2(0f, 300f)), Is.True);
        }

        [Test]
        public void RoadTrafficYieldsToAnApproachingTramOrOneOnTheJunction()
        {
            Assert.That(Tram.RoadMustYield(0f, false), Is.True, "tram staat op het kruispunt");
            Assert.That(Tram.RoadMustYield(Tram.PriorityDistance, true), Is.True);
            Assert.That(Tram.RoadMustYield(Tram.PriorityDistance + 1f, true), Is.False, "nog te ver weg");
            Assert.That(Tram.RoadMustYield(10f, false), Is.False, "tram rijdt van het kruispunt af");
            Assert.Throws<ArgumentOutOfRangeException>(() => Tram.RoadMustYield(-1f, true));
        }
    }
}
