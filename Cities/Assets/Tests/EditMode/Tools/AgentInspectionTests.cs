using System;
using System.Collections.Generic;
using Cities.Core.Ids;
using Cities.Gameplay.Tools;
using Cities.Simulation.Citizens;
using Cities.Simulation.Vehicles;
using Cities.World.Picking;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Tools
{
    public class AgentInspectionTests
    {
        private static readonly EntityId<BuildingKind> Building = new EntityId<BuildingKind>(3, 1);

        private static Citizen Healthy() => new Citizen { Age = 30, Health = 80, Happiness = 80, Home = Building, Work = Building };

        private static NearestAgentLocator<CitizenSample> Citizens(params CitizenSample[] samples) =>
            new NearestAgentLocator<CitizenSample>(() => samples, s => s.Position);

        private static NearestAgentLocator<VehicleSample> Vehicles(params VehicleSample[] samples) =>
            new NearestAgentLocator<VehicleSample>(() => samples, s => s.Position);

        [Test]
        public void TheNearestAgentWithinReachIsFound()
        {
            var locator = Citizens(
                new CitizenSample(new float3(10f, 0f, 0f), 1, Healthy(), Activity.Work),
                new CitizenSample(new float3(11f, 5f, 0f), 2, Healthy(), Activity.Work),
                new CitizenSample(new float3(40f, 0f, 0f), 3, Healthy(), Activity.Work));

            Assert.That(locator.TryFindNearest(new float3(10.8f, 0f, 0f), 3f, out var found), Is.True);
            Assert.That(found.Id, Is.EqualTo(2u), "Hoogte telt niet mee");
            Assert.That(locator.TryFindNearest(new float3(25f, 0f, 0f), 3f, out _), Is.False);
        }

        [Test]
        public void ALocatorNeedsItsInputs()
        {
            Assert.Throws<ArgumentNullException>(() => new NearestAgentLocator<int>(null, _ => float3.zero));
            Assert.Throws<ArgumentNullException>(() => new NearestAgentLocator<int>(() => Array.Empty<int>(), null));
            Assert.Throws<ArgumentNullException>(() => new CitizenInspection(null));
            Assert.Throws<ArgumentNullException>(() => new VehicleInspection(null));
        }

        [Test]
        public void AHealthyCitizenHasNoProblems()
        {
            Assert.That(InspectionProblems.For(Healthy()), Is.Empty);
        }

        [Test]
        public void ACitizenReportsEveryProblem()
        {
            var problems = InspectionProblems.For(new Citizen { Age = 30, Health = 10, Happiness = 5 });

            Assert.That(problems, Is.EquivalentTo(new[] { "Dakloos", "Werkzoekend", "Slechte gezondheid", "Ontevreden" }));
        }

        [Test]
        public void ACitizenReportShowsNumbersAndProblems()
        {
            var source = new CitizenInspection(Citizens(new CitizenSample(new float3(5f, 1f, 5f), 42, new Citizen { Age = 30, Health = 90, Happiness = 20, Home = Building }, Activity.Sleep)));

            Assert.That(source.TryInspect(new float3(5f, 0f, 6f), out var report), Is.True);
            Assert.That(report.Kind, Is.EqualTo(InspectionKind.Citizen));
            Assert.That(report.Title, Is.EqualTo("Burger 42"));
            Assert.That(report.Details, Has.Some.Contains("30 jaar").And.Some.Contains("gezondheid 90").And.Some.Contains("geen werk").And.Some.Contains("woont in gebouw 3"));
            Assert.That(report.Problems, Is.EquivalentTo(new[] { "Werkzoekend", "Ontevreden" }));
            Assert.That(report.ToString(), Does.Contain("! Werkzoekend, Ontevreden"));
            Assert.That(source.TryInspect(new float3(90f, 0f, 90f), out _), Is.False);
        }

        [Test]
        public void ACitizenWithoutHomeShowsNoResidence()
        {
            var source = new CitizenInspection(Citizens(new CitizenSample(float3.zero, 1, new Citizen { Age = 30, Health = 90, Happiness = 90, Work = Building }, Activity.Work)));

            Assert.That(source.TryInspect(float3.zero, out var report), Is.True);
            Assert.That(report.Details, Has.Some.EqualTo("geen woning").And.Some.Contains("werkt in gebouw 3"));
        }

        [TestCase(0f, 0)]
        [TestCase(9.9f, 0)]
        [TestCase(10f, 1)]
        [TestCase(59.9f, 1)]
        [TestCase(60f, 1)]
        public void AStandingVehicleIsFlaggedAfterTenSecondsAndGridlockedAfterAMinute(float stopped, int count)
        {
            var problems = InspectionProblems.For(new VehicleSample(float3.zero, 1, VehicleCategory.Van, 0f, 14f, stopped));

            Assert.That(problems, Has.Count.EqualTo(count));
            if (stopped >= 60f)
            {
                Assert.That(problems, Has.Member("Vastgelopen"));
            }
            else if (count > 0)
            {
                Assert.That(problems, Has.Member("Staat stil"));
            }
        }

        [Test]
        public void AVehicleReportShowsSpeedAgainstTheLimit()
        {
            var source = new VehicleInspection(Vehicles(new VehicleSample(new float3(0f, 0f, 2f), 7, VehicleCategory.Bus, 10f, 13.9f, 0f)));

            Assert.That(source.TryInspect(float3.zero, out var report), Is.True);
            Assert.That(report.Kind, Is.EqualTo(InspectionKind.Vehicle));
            Assert.That(report.Details, Is.EqualTo(new[] { "Bus", "36 km/u", "limiet 50 km/u" }));
            Assert.That(report.HasProblems, Is.False);
            Assert.That(source.TryInspect(new float3(50f, 0f, 0f), out _), Is.False);
        }

        [Test]
        public void AgentsAddedFirstWinOverRoadsAndBuildings()
        {
            var tool = new InspectTool(new Static("gebouw"));
            tool.AddSource(new Static("burger"), first: true);
            tool.AddSource(new Static("laatste"));
            var input = new ToolInput(new PickResult(PickHit.None, new PickHit(PickKind.Terrain, 0, new float3(1f, 0f, 1f), 10f)));

            var preview = tool.UpdatePreview(input);

            Assert.That(preview.Info, Is.EqualTo("burger"));
            Assert.That(tool.Current.HasValue, Is.True);
            Assert.That(tool.Current.Value.Title, Is.EqualTo("burger"));
            Assert.Throws<ArgumentNullException>(() => tool.AddSource(null));
        }

        [Test]
        public void TheCurrentReportClearsWhenNothingIsPointedAt()
        {
            var tool = new InspectTool(new Static("gebouw"));
            var input = new ToolInput(new PickResult(PickHit.None, new PickHit(PickKind.Terrain, 0, new float3(1f, 0f, 1f), 10f)));
            tool.UpdatePreview(input);

            tool.UpdatePreview(new ToolInput(new PickResult(PickHit.None, PickHit.None)));

            Assert.That(tool.Current.HasValue, Is.False);
        }

        [Test]
        public void AReportWithoutProblemsPrintsNoWarning()
        {
            var report = new InspectionReport(InspectionKind.Road, float3.zero, "Weg", new List<string> { "100 m" }, new List<string>());

            Assert.That(report.ToString(), Is.EqualTo("Weg · 100 m"));
            Assert.That(new InspectionReport(InspectionKind.Road, float3.zero, "Weg", null, new[] { "Vol" }).ToString(), Is.EqualTo("Weg · ! Vol"));
        }

        private sealed class Static : IInspectionSource
        {
            private readonly string _title;

            public Static(string title) => _title = title;

            public bool TryInspect(float3 point, out InspectionReport report)
            {
                report = new InspectionReport(point, _title);
                return true;
            }
        }
    }
}
