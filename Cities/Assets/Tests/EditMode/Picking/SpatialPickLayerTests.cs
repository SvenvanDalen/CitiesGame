using System;
using System.Diagnostics;
using Cities.Core.Spatial;
using Cities.World.Grid;
using Cities.World.Picking;
using Cities.World.Terrain;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Picking
{
    /// <summary>Gebruikt native containers en draait daarom alleen in Unity.</summary>
    [Category("UnityRuntime")]
    public class SpatialPickLayerTests
    {
        private static readonly float3 Down = new float3(0f, -1f, 0f);
        private static readonly GridGeometry Geometry = GridGeometry.Covering(float2.zero, new float2(2048f, 2048f), 64f);

        [Test]
        public void BoxesAreHitWhereTheyStand()
        {
            using (var layer = new BoxPickLayer(Geometry))
            {
                layer.Set(new PickBox(7, new float3(100f, 10f, 100f), new float3(8f, 10f, 5f), 0f));
                layer.Set(new PickBox(8, new float3(300f, 30f, 100f), new float3(8f, 30f, 8f), math.radians(45f)));

                Assert.That(layer.Raycast(new PickRay(new float3(105f, 500f, 102f), Down), 1000f, out var hit), Is.True);
                Assert.That(hit.Id, Is.EqualTo(7UL));
                Assert.That(hit.Kind, Is.EqualTo(PickKind.Building));
                Assert.That(hit.Point.y, Is.EqualTo(20f).Within(1e-3f), "Op het dak");
                Assert.That(layer.Raycast(new PickRay(new float3(300f, 500f, 110f), Down), 1000f, out hit), Is.True, "Gedraaid reikt hij verder");
                Assert.That(hit.Id, Is.EqualTo(8UL));
                Assert.That(layer.Raycast(new PickRay(new float3(200f, 500f, 100f), Down), 1000f, out _), Is.False);
                Assert.That(layer.IsSolid, Is.True);
            }
        }

        [Test]
        public void TheNearestBoxAlongASlantedRayWins()
        {
            using (var layer = new BoxPickLayer(Geometry))
            {
                // Twee torens achter elkaar; de straal gaat schuin door beide.
                layer.Set(new PickBox(1, new float3(500f, 50f, 500f), new float3(10f, 50f, 10f), 0f));
                layer.Set(new PickBox(2, new float3(700f, 50f, 500f), new float3(10f, 50f, 10f), 0f));
                var ray = new PickRay(new float3(1000f, 60f, 500f), new float3(-1f, 0f, 0f));

                Assert.That(layer.Raycast(ray, 2000f, out var hit), Is.True);
                Assert.That(hit.Id, Is.EqualTo(2UL));
                Assert.That(hit.Distance, Is.EqualTo(290f).Within(1e-3f));
                Assert.That(layer.Raycast(ray, 200f, out _), Is.False, "Binnen 200 m staat niets");
            }
        }

        [Test]
        public void ObjectsMoveChangeAndDisappear()
        {
            using (var layer = new AgentPickLayer(PickKind.Vehicle, Geometry, margin: 0f, maxMargin: 0f))
            {
                layer.Set(new PickAgent(5, new float3(100f, 0f, 100f), 1f, 1.5f));
                Assert.That(Hit(layer, 100f, 100f), Is.EqualTo(5UL));

                layer.Set(new PickAgent(5, new float3(900f, 0f, 900f), 1f, 1.5f));
                Assert.That(Hit(layer, 100f, 100f), Is.Zero, "Weg van de oude plek");
                Assert.That(Hit(layer, 900f, 900f), Is.EqualTo(5UL));
                Assert.That(layer.Count, Is.EqualTo(1));
                Assert.That(layer.Contains(5), Is.True);

                Assert.That(layer.Remove(5), Is.True);
                Assert.That(layer.Remove(5), Is.False);
                Assert.That(Hit(layer, 900f, 900f), Is.Zero);
                Assert.That(layer.Count, Is.Zero);
            }
        }

        [Test]
        public void ASegmentCanConsistOfSeveralPieces()
        {
            using (var layer = new CapsulePickLayer(PickKind.Segment, Geometry, margin: 0f, maxMargin: 0f))
            {
                // Een bocht in drie stukken; daarna rechtgetrokken tot één stuk.
                var curve = new[]
                {
                    new PickCapsule(3, new float3(100f, 0f, 100f), new float3(200f, 0f, 120f), 4f),
                    new PickCapsule(3, new float3(200f, 0f, 120f), new float3(280f, 0f, 180f), 4f),
                    new PickCapsule(3, new float3(280f, 0f, 180f), new float3(320f, 0f, 280f), 4f),
                };
                layer.Set(3, curve);
                Assert.That(Hit(layer, 300f, 230f), Is.EqualTo(3UL));

                layer.Set(3, new[] { new PickCapsule(3, new float3(100f, 0f, 100f), new float3(400f, 0f, 100f), 4f) });
                Assert.That(Hit(layer, 300f, 230f), Is.Zero, "Oude stukken zijn weg");
                Assert.That(Hit(layer, 300f, 101f), Is.EqualTo(3UL));

                layer.Set(3, ReadOnlySpan<PickCapsule>.Empty);
                Assert.That(layer.Count, Is.Zero, "Geen stukken is verwijderen");
                Assert.Throws<ArgumentException>(() => layer.Set(4, new[] { new PickCapsule(5, float3.zero, float3.zero, 1f) }));
            }
        }

        [Test]
        public void FarAwayThinRoadsGetAMargin()
        {
            using (var layer = new CapsulePickLayer(PickKind.Segment, Geometry, margin: 0.003f, maxMargin: 4f))
            {
                layer.Set(new PickCapsule(1, new float3(0f, 0f, 1000f), new float3(2000f, 0f, 1000f), 3f));

                // Van 1000 m afstand: 3 m straal plus 3 m marge.
                var origin = new float3(1000f, 600f, 200f);
                var target = new float3(1000f, 0f, 1008f);
                Assert.That(layer.Raycast(new PickRay(origin, target - origin), 5000f, out var hit), Is.True);
                Assert.That(hit.Id, Is.EqualTo(1UL));

                target = new float3(1000f, 0f, 1015f);
                Assert.That(layer.Raycast(new PickRay(origin, target - origin), 5000f, out _), Is.False, "Te ver ernaast");
            }
        }

        [Test]
        public void ARealHitBeatsAMarginHitAndTheCentreWinsAmongMargins()
        {
            using (var layer = new AgentPickLayer(PickKind.Citizen, Geometry, margin: 0.01f, maxMargin: 3f))
            {
                layer.Set(new PickAgent(1, new float3(500f, 0f, 500f), 0.3f, 1.8f));
                layer.Set(new PickAgent(2, new float3(501.5f, 0f, 500f), 0.3f, 1.8f));
                layer.Set(new PickAgent(3, new float3(497f, 0f, 500f), 0.3f, 1.8f));

                Assert.That(Hit(layer, 500.1f, 500f, 300f), Is.EqualTo(1UL), "Echte treffer");
                Assert.That(Hit(layer, 500.9f, 500f, 300f), Is.EqualTo(2UL), "Dichter bij 2 dan bij 1");
                Assert.That(Hit(layer, 497.8f, 500f, 300f), Is.EqualTo(3UL));
            }
        }

        [Test]
        public void ClearEmptiesTheLayerAndRaysOutsideTheGridWork()
        {
            using (var layer = new BoxPickLayer(Geometry))
            {
                layer.Set(new PickBox(1, new float3(10f, 5f, 10f), new float3(5f), 0f));
                var fromOutside = new PickRay(new float3(-500f, 5f, 10f), new float3(1f, 0f, 0f));
                Assert.That(layer.Raycast(fromOutside, 2000f, out var hit), Is.True);
                Assert.That(hit.Distance, Is.EqualTo(505f).Within(1e-3f));

                layer.Clear();
                Assert.That(layer.Count, Is.Zero);
                Assert.That(layer.Raycast(fromOutside, 2000f, out _), Is.False);
                layer.Set(new PickBox(1, new float3(10f, 5f, 10f), new float3(5f), 0f));
                Assert.That(layer.Count, Is.EqualTo(1), "Na Clear weer bruikbaar");
            }
        }

        [Test]
        public void LayersCheckTheirArguments()
        {
            Assert.Throws<ArgumentException>(() => new CapsulePickLayer(PickKind.None, Geometry).Dispose());
            Assert.Throws<ArgumentOutOfRangeException>(() => new AgentPickLayer(PickKind.Citizen, Geometry, margin: -1f).Dispose());
        }

        [Test]
        public void PickingAFullCityStaysWithinBudget()
        {
            var dimensions = WorldDimensions.Default;
            var size = new float2(dimensions.SizeX, dimensions.SizeZ);
            var random = new Unity.Mathematics.Random(12345);
            using (var map = new WorldMap(dimensions, 20f))
            using (var buildings = new BoxPickLayer(GridGeometry.Covering(float2.zero, size, 32f), 100_000))
            using (var roads = new CapsulePickLayer(PickKind.Segment, GridGeometry.Covering(float2.zero, size, 32f), 60_000))
            using (var cars = new AgentPickLayer(PickKind.Vehicle, GridGeometry.Covering(float2.zero, size, 32f), 50_000))
            using (var people = new AgentPickLayer(PickKind.Citizen, GridGeometry.Covering(float2.zero, size, 32f), 100_000))
            {
                // Een stad van ongeveer 6 bij 6 km rond het midden.
                var city = new float2(6000f, 6000f);
                for (ulong i = 1; i <= 100_000; i++)
                {
                    var p = random.NextFloat2(city, city + 6000f);
                    var half = new float3(random.NextFloat(4f, 15f), random.NextFloat(4f, 40f), random.NextFloat(4f, 15f));
                    buildings.Set(new PickBox(i, new float3(p.x, 20f + half.y, p.y), half, random.NextFloat(0f, math.PI)));
                }

                for (ulong i = 1; i <= 60_000; i++)
                {
                    var a = random.NextFloat2(city, city + 6000f);
                    var b = a + random.NextFloat2(-60f, 60f);
                    roads.Set(new PickCapsule(i, new float3(a.x, 20.2f, a.y), new float3(b.x, 20.2f, b.y), 4f));
                }

                for (ulong i = 1; i <= 100_000; i++)
                {
                    var p = random.NextFloat2(city, city + 6000f);
                    people.Set(new PickAgent(i, new float3(p.x, 20f, p.y), 0.3f, 1.8f));
                    if (i <= 50_000)
                    {
                        var c = random.NextFloat2(city, city + 6000f);
                        cars.Set(new PickAgent(i, new float3(c.x, 20f, c.y), 1f, 1.5f));
                    }
                }

                var service = new PickingService();
                service.AddLayer(new TerrainPickLayer(new WorldMapTerrain(map), size));
                service.AddLayer(buildings);
                service.AddLayer(roads);
                service.AddLayer(cars);
                service.AddLayer(people);

                // Camera op 400 m hoogte, schuin naar beneden, zoals tijdens het bouwen.
                const int Picks = 2000;
                var rays = new PickRay[Picks];
                for (var i = 0; i < Picks; i++)
                {
                    var target = random.NextFloat2(city, city + 6000f);
                    var origin = new float3(target.x - 300f, 420f, target.y - 300f);
                    rays[i] = new PickRay(origin, new float3(target.x, 20f, target.y) - origin);
                }

                service.Pick(rays[0], PickMask.All);
                var hits = 0;
                var watch = Stopwatch.StartNew();
                foreach (var ray in rays)
                {
                    hits += service.Pick(ray, PickMask.All).Hit.IsHit ? 1 : 0;
                }

                watch.Stop();
                var perPick = watch.Elapsed.TotalMilliseconds / Picks;
                UnityEngine.Debug.Log($"Picking: {perPick:0.000} ms per pick over {Picks} stralen");
                Assert.That(hits, Is.EqualTo(Picks));
                Assert.That(perPick, Is.LessThan(1.0), "Budget voor picking per frame in de editor (docs/PERFORMANCE.md)");
            }
        }

        private static ulong Hit(IPickLayer layer, float x, float z, float height = 50f)
        {
            return layer.Raycast(new PickRay(new float3(x, height, z), Down), 1000f, out var hit) ? hit.Id : 0UL;
        }
    }
}
