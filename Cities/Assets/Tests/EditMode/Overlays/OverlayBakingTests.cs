using System;
using System.Collections.Generic;
using Cities.Gameplay.Overlays;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Overlays
{
    public class OverlayBakingTests
    {
        private static readonly OverlayLegend Legend = new OverlayLegend("Test", "", 0f, 10f, OverlayColorScale.Hot);

        private static DelegateCellOverlaySource Cells(int x, int z, Func<int, int, float> value, Func<long> version = null) =>
            new DelegateCellOverlaySource("test", "Test", x, z, Legend, value, version);

        private sealed class EntitySource : IEntityOverlaySource
        {
            public List<OverlayEntityValue> Items { get; } = new List<OverlayEntityValue>();

            public long Version { get; set; }

            public string Id => "entities";

            public string Name => "Gebouwen";

            public OverlayTarget Target => OverlayTarget.Building;

            public OverlayLegend Legend => OverlayBakingTests.Legend;

            public void Collect(List<OverlayEntityValue> output) => output.AddRange(Items);
        }

        [Test]
        public void BakerKeepsTheFrontUntilTheBakeIsComplete()
        {
            var baker = new CellOverlayBaker();
            baker.Begin(Cells(4, 3, (x, z) => x));

            Assert.That(baker.IsBaking, Is.True);
            Assert.That(baker.Step(5), Is.False);
            Assert.That(baker.Progress, Is.EqualTo(5f / 12f).Within(1e-6f));
            Assert.That(baker.Front, Is.Empty, "nog geen volledige bake");
            Assert.That(baker.Step(100), Is.True);
            Assert.That(baker.IsBaking, Is.False);
            Assert.That(baker.Progress, Is.EqualTo(1f));
            Assert.That(baker.CompletedCount, Is.EqualTo(1));
            Assert.That(baker.Width, Is.EqualTo(4));
            Assert.That(baker.Height, Is.EqualTo(3));
            Assert.That(baker.Front, Has.Length.EqualTo(12));
        }

        [Test]
        public void BakedPixelsFollowTheLegend()
        {
            var baker = new CellOverlayBaker();
            baker.Begin(Cells(3, 2, (x, z) => (x == 0 && z == 0) ? 0f : (x == 2 && z == 1) ? 10f : float.NaN));
            baker.Step(100);

            Assert.That(baker.Front[0], Is.EqualTo(Legend.ColorOf(0f).Pack()));
            Assert.That(baker.Front[5], Is.EqualTo(Legend.ColorOf(10f).Pack()), "x=2, z=1 staat op rij 1");
            Assert.That(baker.Front[1], Is.EqualTo(OverlayColorScale.Hot.Missing.Pack()));
        }

        [Test]
        public void ASecondBakeSwapsBuffersAndABeginDropsTheRunningOne()
        {
            var baker = new CellOverlayBaker();
            baker.Begin(Cells(2, 2, (x, z) => 0f));
            baker.Step(100);
            var first = baker.Front;

            baker.Begin(Cells(2, 2, (x, z) => 10f));
            baker.Step(2);
            Assert.That(baker.Front, Is.SameAs(first), "voorbuffer blijft zichtbaar");
            baker.Begin(Cells(2, 2, (x, z) => 10f));
            Assert.That(baker.Progress, Is.EqualTo(0f));
            baker.Step(100);

            Assert.That(baker.Front, Is.Not.SameAs(first));
            Assert.That(baker.Front[0], Is.EqualTo(Legend.ColorOf(10f).Pack()));
            Assert.That(baker.CompletedCount, Is.EqualTo(2));
        }

        [Test]
        public void CancelStopsBakingAndStepWithoutASourceDoesNothing()
        {
            var baker = new CellOverlayBaker();
            Assert.That(baker.Step(10), Is.False);
            baker.Begin(Cells(2, 2, (x, z) => 1f));
            baker.Cancel();

            Assert.That(baker.IsBaking, Is.False);
            Assert.That(baker.Step(10), Is.False);
            Assert.That(baker.CompletedCount, Is.EqualTo(0));
            Assert.Throws<ArgumentNullException>(() => baker.Begin(null));
        }

        [Test]
        public void ZeroBudgetStillMakesProgress()
        {
            var baker = new CellOverlayBaker();
            baker.Begin(Cells(2, 1, (x, z) => 1f));

            Assert.That(baker.Step(0), Is.False);
            Assert.That(baker.Progress, Is.EqualTo(0.5f));
        }

        [Test]
        public void DelegateSourceReportsItsVersionAndValidatesArguments()
        {
            long version = 3;
            var source = Cells(2, 2, (x, z) => 1f, () => version);

            Assert.That(source.Version, Is.EqualTo(3));
            Assert.That(source.Target, Is.EqualTo(OverlayTarget.Cell));
            Assert.That(source.Value(1, 1), Is.EqualTo(1f));
            Assert.That(source.Id, Is.EqualTo("test"));
            Assert.That(source.Name, Is.EqualTo("Test"));
            Assert.That(source.Legend, Is.SameAs(Legend));
            Assert.That(Cells(2, 2, (x, z) => 1f).Version, Is.EqualTo(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Cells(0, 2, (x, z) => 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Cells(2, 0, (x, z) => 1f));
            Assert.Throws<ArgumentNullException>(() => new DelegateCellOverlaySource(null, "n", 1, 1, Legend, (x, z) => 0f));
            Assert.Throws<ArgumentNullException>(() => new DelegateCellOverlaySource("i", null, 1, 1, Legend, (x, z) => 0f));
            Assert.Throws<ArgumentNullException>(() => new DelegateCellOverlaySource("i", "n", 1, 1, null, (x, z) => 0f));
            Assert.Throws<ArgumentNullException>(() => new DelegateCellOverlaySource("i", "n", 1, 1, Legend, null));
        }

        [Test]
        public void RibbonForASegmentSpansFromToWithItsWidth()
        {
            var vertices = new List<float3>();
            var colors = new List<uint>();
            var indices = new List<int>();
            var values = new[] { new OverlayEntityValue(1, new float3(0f, 2f, 0f), new float3(10f, 2f, 0f), 4f, 10f) };

            var count = OverlayRibbonBuilder.Build(values, Legend, 0.5f, vertices, colors, indices);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(vertices, Has.Count.EqualTo(OverlayRibbonBuilder.VerticesPerRibbon));
            Assert.That(vertices[0].y, Is.EqualTo(2.5f));
            var minZ = Math.Min(Math.Min(vertices[0].z, vertices[1].z), Math.Min(vertices[2].z, vertices[3].z));
            var maxZ = Math.Max(Math.Max(vertices[0].z, vertices[1].z), Math.Max(vertices[2].z, vertices[3].z));
            var maxX = Math.Max(Math.Max(vertices[0].x, vertices[1].x), Math.Max(vertices[2].x, vertices[3].x));
            Assert.That(maxZ - minZ, Is.EqualTo(4f).Within(1e-4f));
            Assert.That(maxX, Is.EqualTo(10f).Within(1e-4f));
            Assert.That(colors, Has.Count.EqualTo(4));
            Assert.That(colors, Is.All.EqualTo(Legend.ColorOf(10f).Pack()));
            Assert.That(indices, Is.EqualTo(new[] { 0, 1, 2, 0, 2, 3 }));
        }

        [Test]
        public void RibbonForABuildingIsASquareAndMissingValuesAreSkipped()
        {
            var vertices = new List<float3>();
            var colors = new List<uint>();
            var indices = new List<int>();
            var point = new float3(50f, 0f, 60f);
            var values = new[]
            {
                new OverlayEntityValue(1, point, point, 8f, 5f),
                new OverlayEntityValue(2, point, point, 8f, float.NaN),
            };

            var count = OverlayRibbonBuilder.Build(values, Legend, 0f, vertices, colors, indices);

            Assert.That(count, Is.EqualTo(1));
            var width = Math.Max(vertices[0].x, vertices[2].x) - Math.Min(vertices[0].x, vertices[2].x);
            var depth = Math.Max(vertices[0].z, vertices[2].z) - Math.Min(vertices[0].z, vertices[2].z);
            Assert.That(width, Is.EqualTo(8f).Within(1e-4f));
            Assert.That(depth, Is.EqualTo(8f).Within(1e-4f));
        }

        [Test]
        public void SecondBuildReplacesThePreviousOutputAndNullsAreRejected()
        {
            var vertices = new List<float3> { float3.zero };
            var colors = new List<uint> { 1u };
            var indices = new List<int> { 7 };

            Assert.That(OverlayRibbonBuilder.Build(Array.Empty<OverlayEntityValue>(), Legend, 0f, vertices, colors, indices), Is.EqualTo(0));
            Assert.That(vertices, Is.Empty);
            Assert.That(colors, Is.Empty);
            Assert.That(indices, Is.Empty);
            Assert.Throws<ArgumentNullException>(() => OverlayRibbonBuilder.Build(null, Legend, 0f, vertices, colors, indices));
            Assert.Throws<ArgumentNullException>(() => OverlayRibbonBuilder.Build(Array.Empty<OverlayEntityValue>(), null, 0f, vertices, colors, indices));
        }

        [Test]
        public void EntitySourceCollectsIntoTheList()
        {
            var source = new EntitySource();
            source.Items.Add(new OverlayEntityValue(9, float3.zero, float3.zero, 1f, 2f));
            var list = new List<OverlayEntityValue>();

            source.Collect(list);

            Assert.That(list, Has.Count.EqualTo(1));
            Assert.That(list[0].Id, Is.EqualTo(9ul));
            Assert.That(list[0].Width, Is.EqualTo(1f));
            Assert.That(list[0].Value, Is.EqualTo(2f));
            Assert.That(list[0].From, Is.EqualTo(float3.zero));
            Assert.That(list[0].To, Is.EqualTo(float3.zero));
        }
    }
}
