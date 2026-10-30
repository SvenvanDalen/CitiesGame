using System;
using System.Linq;
using Cities.Core.Diagnostics;
using Cities.Core.Ids;
using Cities.World.Picking;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Picking
{
    public class PickingOverlayTests
    {
        private static readonly DebugView View = new DebugView(new float3(0f, 300f, -300f), float3.zero, 800f);

        [Test]
        public void AHitGetsACrossAndALabel()
        {
            var building = new EntityId<BuildingKind>(12, 3);
            var result = new PickResult(
                new PickHit(PickKind.Building, building.Packed, new float3(10f, 30f, 10f), 400f),
                new PickHit(PickKind.Terrain, 0, new float3(10f, 0f, 12f), 410f));
            var geometry = new DebugGeometry();

            new PickingOverlay(() => result).Collect(geometry, View);

            Assert.That(geometry.Labels.Single().Text, Is.EqualTo("Gebouw 12 (v3)"));
            Assert.That(geometry.Labels[0].Color, Is.EqualTo(DebugColor.Info));
            Assert.That(geometry.Lines.Count(l => l.Color == DebugColor.Info), Is.EqualTo(3));
            Assert.That(geometry.Lines.Count(l => l.Color == DebugColor.Neutral), Is.EqualTo(3), "Klein kruis op het terrein");
        }

        [Test]
        public void TerrainShowsItsPosition()
        {
            var ground = new PickHit(PickKind.Terrain, 0, new float3(1234.4f, 56.78f, 987.6f), 300f);
            var geometry = new DebugGeometry();

            new PickingOverlay(() => new PickResult(ground, ground)).Collect(geometry, View);

            Assert.That(geometry.Labels.Single().Text, Is.EqualTo("Terrein 1234, 988 op 56.8 m"));
            Assert.That(geometry.Lines, Has.Count.EqualTo(3), "Geen tweede kruis voor de grond");
        }

        [Test]
        public void NothingDrawsNothing()
        {
            var geometry = new DebugGeometry();

            new PickingOverlay(() => default).Collect(geometry, View);

            Assert.That(geometry.Lines, Is.Empty);
            Assert.That(geometry.Labels, Is.Empty);
        }

        [TestCase(PickKind.Terrain, "Terrein")]
        [TestCase(PickKind.Building, "Gebouw")]
        [TestCase(PickKind.Segment, "Weg")]
        [TestCase(PickKind.Node, "Knooppunt")]
        [TestCase(PickKind.Vehicle, "Voertuig")]
        [TestCase(PickKind.Citizen, "Burger")]
        [TestCase(PickKind.None, "Niets")]
        public void KindsHaveDutchNames(PickKind kind, string expected)
        {
            Assert.That(PickingOverlay.NameOf(kind), Is.EqualTo(expected));
        }

        [TestCase(PickKind.Segment, DebugColor.Accent)]
        [TestCase(PickKind.Node, DebugColor.Accent)]
        [TestCase(PickKind.Vehicle, DebugColor.Warning)]
        [TestCase(PickKind.Citizen, DebugColor.Good)]
        public void KindsHaveTheirOwnColor(PickKind kind, DebugColor expected)
        {
            var geometry = new DebugGeometry();
            var result = new PickResult(new PickHit(kind, 1, float3.zero, 10f), PickHit.None);

            new PickingOverlay(() => result).Collect(geometry, View);

            Assert.That(geometry.Labels.Single().Color, Is.EqualTo(expected));
        }

        [Test]
        public void ArgumentsAreChecked()
        {
            Assert.Throws<ArgumentNullException>(() => _ = new PickingOverlay(null));
            var overlay = new PickingOverlay(() => default);
            Assert.That(overlay.Name, Is.EqualTo(PickingOverlay.OverlayName));
            Assert.Throws<ArgumentNullException>(() => overlay.Collect(null, View));
            Assert.That(PickingOverlay.Describe(PickHit.None), Is.EqualTo("Niets"));
        }
    }
}
