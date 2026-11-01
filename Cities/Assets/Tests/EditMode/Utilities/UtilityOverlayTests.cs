using System;
using System.Linq;
using Cities.Core.Diagnostics;
using Cities.Simulation.Utilities;
using Cities.World.Terrain;
using Cities.World.Utilities;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Utilities
{
    public class UtilityOverlayTests
    {
        private static readonly DebugView View = new DebugView(new float3(0f, 100f, 0f), new float3(0f, 0f, 0f), 500f);

        [Test]
        public void TheNetworkExposesItsNodesPositionsAndLinks()
        {
            var network = new UtilityNetwork(0f);
            var a = network.Add(new float2(1f, 2f), 10f, 0f);
            var b = network.Add(new float2(5f, 6f), 0f, 4f);
            network.Link(a, b);
            network.Remove(b);

            Assert.That(network.Nodes.ToArray(), Is.EqualTo(new[] { a }));
            Assert.That(network.PositionOf(a), Is.EqualTo(new float2(1f, 2f)));
            Assert.That(network.LinksOf(a), Is.Empty, "verwijderde knoop heeft geen leiding meer");
            Assert.Throws<ArgumentException>(() => network.PositionOf(b));
        }

        [Test]
        public void InspectionGivesSupplyDemandNodesAndOverload()
        {
            var network = new UtilityNetwork(0f);
            var plant = network.Add(float2.zero, 100f, 0f);
            var house = network.Add(new float2(10f, 0f), 0f, 150f);
            network.Link(plant, house);
            network.Tick();

            var load = network.Inspect(network.NetworkOf(plant));

            Assert.That(load.Supply, Is.EqualTo(100f));
            Assert.That(load.Demand, Is.EqualTo(150f));
            Assert.That(load.Nodes, Is.EqualTo(2));
            Assert.That(load.Utilization, Is.EqualTo(1.5f).Within(1e-5f));
            Assert.That(load.IsOverloaded, Is.True);
            Assert.That(UtilityOverlay.Describe(load), Does.Contain("tekort").And.Contain("2 knopen"));
            Assert.Throws<ArgumentOutOfRangeException>(() => network.Inspect(7));
        }

        [Test]
        public void UtilizationHandlesNetworksWithoutSupply()
        {
            Assert.That(new NetworkLoad(0f, 0f, 1).Utilization, Is.Zero);
            Assert.That(new NetworkLoad(0f, 5f, 1).Utilization, Is.EqualTo(float.PositiveInfinity));
            Assert.That(UtilityOverlay.Describe(new NetworkLoad(10f, 5f, 1)), Does.Not.Contain("tekort"));
        }

        [Test]
        public void ColoursFollowTheSatisfaction()
        {
            Assert.That(UtilityOverlay.ColorFor(1f), Is.EqualTo(DebugColor.Good));
            Assert.That(UtilityOverlay.ColorFor(0.7f), Is.EqualTo(DebugColor.Warning));
            Assert.That(UtilityOverlay.ColorFor(0.2f), Is.EqualTo(DebugColor.Bad));
        }

        [Test]
        public void TheOverlayDrawsCrossesAndPipesInViewOnly()
        {
            var network = new UtilityNetwork(0f);
            var plant = network.Add(float2.zero, 10f, 0f);
            var house = network.Add(new float2(20f, 0f), 0f, 30f);
            network.Add(new float2(9000f, 0f), 0f, 1f);
            network.Link(plant, house);
            network.Tick();
            var overlay = new UtilityOverlay("Stroom", network, new FlatTerrain(0f));
            var geometry = new DebugGeometry();

            overlay.Collect(geometry, View);

            Assert.That(overlay.Name, Is.EqualTo("Stroom"));
            Assert.That(geometry.Lines.Count, Is.EqualTo(5), "twee kruisjes van twee lijnen plus één leiding; de ver weg gelegen knoop niet");
            Assert.That(geometry.Lines.Count(l => l.Color == DebugColor.Info), Is.EqualTo(1));
            Assert.That(geometry.Lines.Count(l => l.Color == DebugColor.Bad), Is.EqualTo(4), "het hele net krijgt een derde van de vraag, dus beide kruisjes zijn rood");
        }

        [Test]
        public void NullArgumentsAreRejected()
        {
            var network = new UtilityNetwork(0f);

            Assert.Throws<ArgumentNullException>(() => new UtilityOverlay(null, network, new FlatTerrain(0f)));
            Assert.Throws<ArgumentNullException>(() => new UtilityOverlay("x", null, new FlatTerrain(0f)));
            Assert.Throws<ArgumentNullException>(() => new UtilityOverlay("x", network, null));
            Assert.Throws<ArgumentNullException>(() => new UtilityOverlay("x", network, new FlatTerrain(0f)).Collect(null, View));
        }
    }
}
