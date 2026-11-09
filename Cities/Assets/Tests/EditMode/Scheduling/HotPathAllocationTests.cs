using System.Collections.Generic;
using Cities.Simulation.Fields;
using Cities.Simulation.Scheduling;
using Cities.Simulation.Vehicles;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Cities.Tests.EditMode.Scheduling
{
    /// <summary>
    /// Audit van de hot paths (E23-03): wat elke tick of elk frame draait mag na opwarmen niets op de managed heap zetten.
    /// Draait alleen in Unity, want de allocatie-constraint bestaat daar. Zie docs/AUDIT-HOT-PATHS.md voor wat bewust nog wel alloceert.
    /// </summary>
    [Category("UnityRuntime")]
    public sealed class HotPathAllocationTests
    {
        [Test]
        public void LaneTrafficStepDoesNotAllocate()
        {
            var driver = new Idm(30f, 1.5f, 2f);
            var cars = new List<VehicleAgent>();
            for (var i = 9; i >= 0; i--)
            {
                cars.Add(new VehicleAgent { Position = 20f + (i * 15f), Speed = 10f, Length = 4.5f, Driver = driver });
            }

            LaneTraffic.Step(cars, 500f, false, 0.1f);

            Assert.That(() => LaneTraffic.Step(cars, 500f, false, 0.1f), Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void CongestionOccupancyDoesNotAllocate()
        {
            var cars = new List<VehicleAgent> { new VehicleAgent { Position = 10f, Speed = 5f, Length = 4.5f, Driver = new Idm(30f, 1.5f, 2f) } };
            Congestion.Occupancy(cars, 100f);

            Assert.That(() => { Congestion.Occupancy(cars, 100f); }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void IdmAccelerationDoesNotAllocate()
        {
            var idm = new Idm(30f, 1.5f, 2f);
            idm.Acceleration(10f, 20f, 8f);

            Assert.That(() => { idm.Acceleration(10f, 20f, 8f); }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void DiffusionStepDoesNotAllocate()
        {
            var settings = new DiffusionSettings(0.1f, 0.01f, new float2(0.02f, 0f));
            DiffusionStep.Next(1f, new float4(1f), settings);

            Assert.That(
                () => { _ = DiffusionStep.Next(1f, new float4(1f, 2f, 3f, 4f), settings) + DiffusionStep.Neighbours(5, 10, 10).x; },
                Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void SimulationTiersDoNotAllocate()
        {
            var tiers = new SimulationTiers(20, 20, 100f, 300f);
            tiers.Visit(new float2(1000f, 1000f), 0);
            tiers.ShouldStep(5, 1, out _);

            Assert.That(
                () =>
                {
                    tiers.Visit(new float2(1000f, 1000f), 2);
                    tiers.ShouldStep(5, 3, out _);
                    tiers.TierAt(new float2(900f, 900f), 3);
                },
                Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void SystemTimingsRecordDoesNotAllocateAfterTheFirstSample()
        {
            var timings = new SystemTimings();
            var system = "Verkeer";
            timings.Record(system, 1d);
            timings.Record(system, 1d);

            Assert.That(() => { timings.Record(system, 2d); }, Is.Not.AllocatingGCMemory());
        }
    }
}
