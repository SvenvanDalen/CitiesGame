using System;
using Cities.Core.Ids;
using Cities.World.Picking;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Picking
{
    public class PickingServiceTests
    {
        /// <summary>Laag die op een vaste afstand raakt (of niet).</summary>
        private sealed class FixedLayer : IPickLayer
        {
            public FixedLayer(PickKind kind, float distance, bool solid = false, ulong id = 1)
            {
                Kind = kind;
                Distance = distance;
                IsSolid = solid;
                Id = id;
            }

            public PickKind Kind { get; }

            public bool IsSolid { get; }

            public float Distance { get; set; }

            public ulong Id { get; }

            public float LastLimit { get; private set; }

            public bool Raycast(in PickRay ray, float maxDistance, out PickHit hit)
            {
                LastLimit = maxDistance;
                hit = new PickHit(Kind, Id, ray.Point(Distance), Distance);
                return !float.IsNaN(Distance) && Distance <= maxDistance;
            }
        }

        private static readonly PickRay Ray = new PickRay(new float3(0f, 500f, 0f), new float3(0f, -1f, 0f));

        [Test]
        public void TheHighestKindUnderTheCursorWins()
        {
            var service = new PickingService();
            service.AddLayer(new FixedLayer(PickKind.Terrain, 500f, solid: true));
            service.AddLayer(new FixedLayer(PickKind.Segment, 499.9f));
            service.AddLayer(new FixedLayer(PickKind.Citizen, 498.2f));

            var result = service.Pick(Ray, PickKinds.All);

            Assert.That(result.Hit.Kind, Is.EqualTo(PickKind.Citizen));
            Assert.That(result.Ground.Kind, Is.EqualTo(PickKind.Terrain));
            Assert.That(result.HasGround, Is.True);
        }

        [Test]
        public void ARoadSlightlyBelowTheTerrainIsStillVisible()
        {
            var service = new PickingService(absoluteTolerance: 0.75f, relativeTolerance: 0f);
            service.AddLayer(new FixedLayer(PickKind.Terrain, 500f, solid: true));
            var road = new FixedLayer(PickKind.Segment, 500.5f);
            service.AddLayer(road);

            Assert.That(service.Pick(Ray, PickKinds.All).Hit.Kind, Is.EqualTo(PickKind.Segment));

            road.Distance = 501f;
            Assert.That(service.Pick(Ray, PickKinds.All).Hit.Kind, Is.EqualTo(PickKind.Terrain), "Te ver onder het terrein");
        }

        [Test]
        public void ABuildingHidesWhatIsBehindIt()
        {
            var service = new PickingService();
            service.AddLayer(new FixedLayer(PickKind.Terrain, 500f, solid: true));
            service.AddLayer(new FixedLayer(PickKind.Building, 470f, solid: true, id: 9));
            var node = new FixedLayer(PickKind.Node, 499.9f);
            service.AddLayer(node);

            var all = service.Pick(Ray, PickKinds.All);
            var nodesOnly = service.Pick(Ray, PickKinds.Node);

            Assert.That(all.Hit.Kind, Is.EqualTo(PickKind.Building));
            Assert.That(all.Hit.Id, Is.EqualTo(9UL));
            Assert.That(nodesOnly.Hit.IsHit, Is.False, "Ook als de tool geen gebouwen zoekt");
            Assert.That(nodesOnly.Ground.Distance, Is.EqualTo(500f), "Het terreinpunt blijft bekend");
            Assert.That(node.LastLimit, Is.LessThan(472f));
        }

        [Test]
        public void ARoadInFrontOfABuildingWins()
        {
            var service = new PickingService();
            service.AddLayer(new FixedLayer(PickKind.Building, 480f, solid: true));
            service.AddLayer(new FixedLayer(PickKind.Segment, 460f));

            Assert.That(service.Pick(Ray, PickKinds.All).Hit.Kind, Is.EqualTo(PickKind.Segment), "Brug voor een gebouw");
        }

        [Test]
        public void SameKindPicksTheNearest()
        {
            var service = new PickingService();
            service.AddLayer(new FixedLayer(PickKind.Vehicle, 300f, id: 1));
            service.AddLayer(new FixedLayer(PickKind.Vehicle, 200f, id: 2));
            service.AddLayer(new FixedLayer(PickKind.Vehicle, 400f, id: 3));

            Assert.That(service.Pick(Ray, PickKinds.All).Hit.Id, Is.EqualTo(2UL));
        }

        [Test]
        public void TheMaskFiltersKinds()
        {
            var service = new PickingService();
            service.AddLayer(new FixedLayer(PickKind.Terrain, 500f, solid: true));
            service.AddLayer(new FixedLayer(PickKind.Citizen, 499f));

            Assert.That(service.Pick(Ray, PickKinds.Terrain).Hit.Kind, Is.EqualTo(PickKind.Terrain));
            Assert.That(service.Pick(Ray, PickKinds.None).Hit.IsHit, Is.False);
            Assert.That(service.Pick(Ray, PickKinds.Agents).Hit.Kind, Is.EqualTo(PickKind.Citizen));
        }

        [Test]
        public void NothingHitGivesAnEmptyResult()
        {
            var service = new PickingService();
            service.AddLayer(new FixedLayer(PickKind.Terrain, float.NaN, solid: true));

            var result = service.Pick(Ray, PickKinds.All);

            Assert.That(result.Hit.IsHit, Is.False);
            Assert.That(result.HasGround, Is.False);
            Assert.That(result.Hit.ToString(), Is.EqualTo("Niets"));
        }

        [Test]
        public void TheNearestTerrainLayerIsTheGround()
        {
            var service = new PickingService();
            service.AddLayer(new FixedLayer(PickKind.Terrain, 480f, solid: true));
            service.AddLayer(new FixedLayer(PickKind.Terrain, 470f, solid: true));
            service.AddLayer(new FixedLayer(PickKind.Terrain, 490f, solid: true));

            Assert.That(service.Pick(Ray, PickKinds.None).Ground.Distance, Is.EqualTo(470f));
        }

        [Test]
        public void LayersCanBeRemovedAndAreChecked()
        {
            var service = new PickingService();
            var citizen = new FixedLayer(PickKind.Citizen, 100f);
            var token = service.AddLayer(citizen);

            Assert.That(service.Layers, Has.Count.EqualTo(1));
            Assert.Throws<ArgumentException>(() => service.AddLayer(citizen));
            Assert.Throws<ArgumentNullException>(() => service.AddLayer(null));
            Assert.Throws<ArgumentException>(() => service.AddLayer(new FixedLayer(PickKind.None, 1f)));

            token.Dispose();
            token.Dispose();
            Assert.That(service.Layers, Is.Empty);
            Assert.That(service.Pick(Ray, PickKinds.All).Hit.IsHit, Is.False);
        }

        [Test]
        public void TolerancesAreValidated()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new PickingService(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new PickingService(0f, float.NaN));
            var service = new PickingService(0.5f, 0.01f);
            Assert.That(service.AbsoluteTolerance, Is.EqualTo(0.5f));
            Assert.That(service.RelativeTolerance, Is.EqualTo(0.01f));
        }

        [Test]
        public void RaysMasksAndHitsBehave()
        {
            var ray = new PickRay(float3.zero, new float3(0f, 0f, 3f), 10f);
            Assert.That(ray.Direction, Is.EqualTo(new float3(0f, 0f, 1f)));
            Assert.That(ray.Point(2f), Is.EqualTo(new float3(0f, 0f, 2f)));
            Assert.That(ray.Length, Is.EqualTo(10f));
            Assert.Throws<ArgumentException>(() => _ = new PickRay(float3.zero, float3.zero));
            Assert.Throws<ArgumentException>(() => _ = new PickRay(float3.zero, new float3(float.NaN, 0f, 0f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new PickRay(float3.zero, new float3(0f, 1f, 0f), 0f));

            Assert.That(PickKindsExtensions.Of(PickKind.Node), Is.EqualTo(PickKinds.Node));
            Assert.That(PickKindsExtensions.Of(PickKind.None), Is.EqualTo(PickKinds.None));
            Assert.That(PickKinds.Network.Includes(PickKind.Segment), Is.True);
            Assert.That(PickKinds.Network.Includes(PickKind.Building), Is.False);
            Assert.That(PickKinds.All.Includes(PickKind.None), Is.False);

            var building = new EntityId<BuildingKind>(42, 3);
            var hit = new PickHit(PickKind.Building, building.Packed, new float3(1f, 2f, 3f), 12.34f);
            Assert.That(hit.As<BuildingKind>(), Is.EqualTo(building));
            Assert.That(hit.ToString(), Does.StartWith("Building ").And.Contain("12"));
            Assert.That(new PickResult(hit, PickHit.None).HasGround, Is.False);
        }
    }
}
