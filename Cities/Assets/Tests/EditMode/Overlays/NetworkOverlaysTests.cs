using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Gameplay.Overlays;
using Cities.Simulation.Network;
using Cities.Simulation.Utilities;
using Cities.World.Terrain;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Overlays
{
    public class NetworkOverlaysTests
    {
        private static List<OverlayEntityValue> Collect(IEntityOverlaySource source)
        {
            var list = new List<OverlayEntityValue>();
            source.Collect(list);
            return list;
        }

        private static UtilityNetwork TwoNodes(out int supplier, out int consumer)
        {
            var network = new UtilityNetwork(0f);
            supplier = network.Add(new float2(0f, 0f), 5f, 0f);
            consumer = network.Add(new float2(40f, 0f), 0f, 10f);
            network.Link(supplier, consumer);
            network.Tick();
            return network;
        }

        [Test]
        public void ANetworkBecomesNodesAndPipesColouredByWhatIsDelivered()
        {
            var network = TwoNodes(out var supplier, out var consumer);
            var terrain = new FlatTerrain(10f);

            var values = Collect(NetworkOverlays.Power(network, terrain));

            Assert.That(values, Has.Count.EqualTo(3), "twee knopen en één leiding");
            Assert.That(values.Select(v => v.Id).Distinct().Count(), Is.EqualTo(3), "elke ID is uniek");
            var pipe = values.Single(v => !v.From.Equals(v.To));
            Assert.That(pipe.Width, Is.EqualTo(NetworkOverlays.PipeWidth));
            Assert.That(pipe.Value, Is.EqualTo(50f).Within(1e-3f), "de helft van de vraag wordt geleverd");
            Assert.That(pipe.From.y, Is.GreaterThan(10f), "een stukje boven het terrein");
            var node = values.Single(v => v.Id == (ulong)consumer);
            Assert.That(node.Width, Is.EqualTo(NetworkOverlays.NodeSize));
            Assert.That(node.Value, Is.EqualTo(50f).Within(1e-3f));
            Assert.That(values.Single(v => v.Id == (ulong)supplier).Value, Is.EqualTo(50f).Within(1e-3f), "de tevredenheid geldt voor het hele net");
        }

        [Test]
        public void EveryNetworkHasItsOwnKeyAndAColorScaleWhereHighIsGood()
        {
            var network = TwoNodes(out _, out _);
            var terrain = FlatTerrain.Zero;

            Assert.That(NetworkOverlays.Power(network, terrain).Id, Is.EqualTo(NetworkOverlays.PowerId));
            Assert.That(NetworkOverlays.Water(network, terrain).Id, Is.EqualTo(NetworkOverlays.WaterId));
            var sewage = NetworkOverlays.Sewage(network, terrain);
            Assert.That(sewage.Id, Is.EqualTo(NetworkOverlays.SewageId));
            Assert.That(sewage.Target, Is.EqualTo(OverlayTarget.Segment));
            Assert.That(sewage.Legend.ColorOf(0f).R, Is.GreaterThan(sewage.Legend.ColorOf(100f).R), "0% is rood, 100% groen");
        }

        [Test]
        public void ARemovedNodeLeavesTheOverlay()
        {
            var network = TwoNodes(out _, out var consumer);
            network.Remove(consumer);

            Assert.That(Collect(NetworkOverlays.Water(network, FlatTerrain.Zero)), Has.Count.EqualTo(1));
        }

        [Test]
        public void GarbageSitesShowTheirFillLevel()
        {
            var bin = new WasteBin(100f);
            bin.Produce(40f);
            var overfull = new WasteBin(10f);
            overfull.Produce(50f);
            var sites = new List<WasteSite> { new WasteSite(7, new float2(3f, 4f), bin), new WasteSite(8, new float2(9f, 9f), overfull) };
            var source = NetworkOverlays.Garbage(() => sites, new FlatTerrain(2f));

            var values = Collect(source);

            Assert.That(source.Target, Is.EqualTo(OverlayTarget.Building));
            Assert.That(values.Single(v => v.Id == 7).Value, Is.EqualTo(40f).Within(1e-3f));
            Assert.That(values.Single(v => v.Id == 8).Value, Is.EqualTo(100f), "een volle bak blijft op 100");
            Assert.That(values[0].From, Is.EqualTo(values[0].To), "een gebouw is een vlak");
        }

        [Test]
        public void TheVersionComesFromTheCounter()
        {
            long version = 3;
            var source = NetworkOverlays.Power(TwoNodes(out _, out _), FlatTerrain.Zero, () => version);

            Assert.That(source.Version, Is.EqualTo(3));
            version = 4;
            Assert.That(source.Version, Is.EqualTo(4));
            Assert.That(NetworkOverlays.Power(TwoNodes(out _, out _), FlatTerrain.Zero).Version, Is.EqualTo(0));
        }

        [Test]
        public void InvalidArgumentsAreRejected()
        {
            var network = TwoNodes(out _, out _);

            Assert.Throws<ArgumentNullException>(() => NetworkOverlays.Power(null, FlatTerrain.Zero));
            Assert.Throws<ArgumentNullException>(() => NetworkOverlays.Power(network, null));
            Assert.Throws<ArgumentNullException>(() => NetworkOverlays.Garbage(null, FlatTerrain.Zero));
            Assert.Throws<ArgumentNullException>(() => NetworkOverlays.Garbage(() => new List<WasteSite>(), null));
            Assert.Throws<ArgumentNullException>(() => NetworkOverlays.FillPercent(null));
            Assert.Throws<ArgumentNullException>(() => new WasteSite(1, float2.zero, null));
        }

        [Test]
        public void ACurveIsCutIntoRibbonsOfAtMostTheMaximumLength()
        {
            var output = new List<OverlayEntityValue>();
            var curve = Bezier3.Straight(new float3(0f, 0f, 0f), new float3(100f, 0f, 0f));

            var pieces = CurveRibbons.Add(output, 5, curve, 6f, 80f);

            Assert.That(pieces, Is.InRange(4, 5), "100 m in stukken van ongeveer 25 m");
            Assert.That(output, Has.Count.EqualTo(pieces));
            Assert.That(output.All(v => v.Id == 5 && v.Value == 80f && v.Width == 6f), Is.True);
            Assert.That(output.All(v => math.distance(v.From, v.To) <= CurveRibbons.MaxPieceLength + 1e-3f), Is.True);
            Assert.That(output[0].From.x, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(output[pieces - 1].To.x, Is.EqualTo(100f).Within(1e-3f));
            Assert.That(output[1].From, Is.EqualTo(output[0].To), "de linten sluiten op elkaar aan");
        }

        [Test]
        public void AShortCurveIsOneRibbon()
        {
            var output = new List<OverlayEntityValue>();

            Assert.That(CurveRibbons.Add(output, 1, Bezier3.Straight(float3.zero, new float3(0f, 0f, 3f)), 4f, 1f), Is.EqualTo(1));
            Assert.Throws<ArgumentNullException>(() => CurveRibbons.Add(null, 1, Bezier3.Straight(float3.zero, new float3(0f, 0f, 3f)), 4f, 1f));
        }

        [Test]
        public void AnEntitySourceNeedsAnEntityTarget()
        {
            var legend = new OverlayLegend("t", "u", 0f, 1f, OverlayColorScale.Hot);

            Assert.Throws<ArgumentException>(() => new EntityOverlaySource("a", "A", OverlayTarget.Cell, legend, _ => { }));
            Assert.Throws<ArgumentNullException>(() => new EntityOverlaySource(null, "A", OverlayTarget.Segment, legend, _ => { }));
            Assert.Throws<ArgumentNullException>(() => new EntityOverlaySource("a", null, OverlayTarget.Segment, legend, _ => { }));
            Assert.Throws<ArgumentNullException>(() => new EntityOverlaySource("a", "A", OverlayTarget.Segment, null, _ => { }));
            Assert.Throws<ArgumentNullException>(() => new EntityOverlaySource("a", "A", OverlayTarget.Segment, legend, null));
            var source = new EntityOverlaySource("a", "A", OverlayTarget.Segment, legend, _ => { });
            Assert.Throws<ArgumentNullException>(() => source.Collect(null));
        }
    }
}
