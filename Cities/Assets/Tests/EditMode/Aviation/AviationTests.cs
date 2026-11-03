using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;
using Cities.Simulation.Aviation;
using Cities.Simulation.Fields;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Aviation
{
    public class AviationTests
    {
        private static readonly AircraftClass[] Narrow = { AircraftClass.Narrowbody, AircraftClass.Narrowbody, AircraftClass.Narrowbody, AircraftClass.Narrowbody };

        private AirportNetwork _network;

        [SetUp]
        public void SetUp()
        {
            _network = new AirportNetwork();
        }

        [Test]
        public void BiggerAircraftNeedLongerRunways()
        {
            Assert.That(AviationRules.RequiredRunway(AircraftClass.Regional), Is.LessThan(AviationRules.RequiredRunway(AircraftClass.Narrowbody)));
            Assert.That(AviationRules.RequiredRunway(AircraftClass.Narrowbody), Is.LessThan(AviationRules.RequiredRunway(AircraftClass.Widebody)));
            Assert.That(AviationRules.CanOperate(AircraftClass.Widebody, 2500f), Is.False);
            Assert.That(AviationRules.CanOperate(AircraftClass.Narrowbody, 2500f), Is.True);
            Assert.That(AviationRules.IsRunwayOk(AviationRules.MinRunway - 1f), Is.False);
            Assert.That(AviationRules.IsRunwayOk(AviationRules.MaxRunway), Is.True);
            Assert.That(AviationRules.IsRunwayOk(AviationRules.MaxRunway + 1f), Is.False);
        }

        [Test]
        public void EveryClassHasConsistentNumbers()
        {
            foreach (AircraftClass kind in Enum.GetValues(typeof(AircraftClass)))
            {
                Assert.That(AviationRules.Seats(kind), Is.GreaterThan(0));
                Assert.That(AviationRules.PassengersPerMovement(kind), Is.EqualTo(AviationRules.Seats(kind) * AviationRules.LoadFactor).Within(1e-3f));
                Assert.That(AviationRules.TurnaroundTime(kind), Is.GreaterThan(AviationRules.RunwayOccupancy(kind)));
                Assert.That(AviationRules.GateMovementsPerHour(kind), Is.EqualTo(7200f / AviationRules.TurnaroundTime(kind)).Within(1e-3f));
                Assert.That(AviationRules.NoiseMultiplicity(kind), Is.GreaterThan(0));
            }

            const AircraftClass unknown = (AircraftClass)99;
            Assert.Throws<ArgumentOutOfRangeException>(() => AviationRules.RequiredRunway(unknown));
            Assert.Throws<ArgumentOutOfRangeException>(() => AviationRules.Seats(unknown));
            Assert.Throws<ArgumentOutOfRangeException>(() => AviationRules.TurnaroundTime(unknown));
            Assert.Throws<ArgumentOutOfRangeException>(() => AviationRules.RunwayOccupancy(unknown));
            Assert.Throws<ArgumentOutOfRangeException>(() => AviationRules.NoiseMultiplicity(unknown));
        }

        [Test]
        public void AnAirportRejectsInvalidParts()
        {
            Assert.Throws<ArgumentException>(() => new Airport(1, " ", float2.zero, 0f, 2500f, 1000f, Narrow));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Airport(1, "A", float2.zero, 0f, 500f, 1000f, Narrow));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Airport(1, "A", float2.zero, 0f, 2500f, 0f, Narrow));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Airport(1, "A", float2.zero, 0f, 2500f, 1e9f, Narrow));
            Assert.Throws<ArgumentException>(() => new Airport(1, "A", float2.zero, 0f, 2500f, 1000f, null));
            Assert.Throws<ArgumentException>(() => new Airport(1, "A", float2.zero, 0f, 2500f, 1000f, new AircraftClass[0]));
            Assert.Throws<ArgumentException>(() => new Airport(1, "A", float2.zero, 0f, 2500f, 1000f, new AircraftClass[AviationRules.MaxGates + 1]));
            Assert.Throws<ArgumentException>(() => new Airport(1, "A", new float2(float.NaN, 0f), 0f, 2500f, 1000f, Narrow));
            Assert.Throws<ArgumentException>(() => new Airport(1, "A", float2.zero, float.NaN, 2500f, 1000f, Narrow));
            Assert.Throws<ArgumentException>(
                () => new Airport(1, "A", float2.zero, 0f, 1300f, 1000f, new[] { AircraftClass.Widebody }),
                "geen enkele gate kan de baan gebruiken");
        }

        [Test]
        public void AGateWithATooShortRunwayStaysUnused()
        {
            var airport = new Airport(1, "A", float2.zero, 0f, 2500f, 50_000f, new[] { AircraftClass.Narrowbody, AircraftClass.Widebody });

            Assert.That(airport.UsableGateCount, Is.EqualTo(1));
            Assert.That(airport.LoudestClass, Is.EqualTo(AircraftClass.Narrowbody));
            Assert.That(airport.GateMovementsPerHour, Is.EqualTo(AviationRules.GateMovementsPerHour(AircraftClass.Narrowbody)).Within(1e-3f));
            Assert.That(airport.Gates, Has.Count.EqualTo(2));
        }

        [Test]
        public void TheGatesAreTheBottleneckOfASmallAirport()
        {
            var airport = new Airport(1, "A", float2.zero, 0f, 3000f, 50_000f, new[] { AircraftClass.Narrowbody, AircraftClass.Narrowbody });

            Assert.That(airport.MovementsPerHour, Is.EqualTo(airport.GateMovementsPerHour).Within(1e-3f));
            Assert.That(airport.MovementsPerHour, Is.LessThan(airport.RunwayMovementsPerHour));
            Assert.That(airport.AveragePassengersPerMovement, Is.EqualTo(AviationRules.PassengersPerMovement(AircraftClass.Narrowbody)).Within(1e-3f));
            Assert.That(airport.PassengersPerHour, Is.EqualTo(airport.MovementsPerHour * airport.AveragePassengersPerMovement).Within(1e-2f));
        }

        [Test]
        public void TheRunwayIsTheBottleneckOfALargeAirport()
        {
            var gates = Enumerable.Repeat(AircraftClass.Narrowbody, 40).ToArray();
            var airport = new Airport(1, "A", float2.zero, 0f, 3000f, 100_000f, gates);

            Assert.That(airport.RunwayMovementsPerHour, Is.EqualTo(3600f / AviationRules.RunwayOccupancy(AircraftClass.Narrowbody)).Within(1e-3f));
            Assert.That(airport.MovementsPerHour, Is.EqualTo(airport.RunwayMovementsPerHour).Within(1e-3f));
            Assert.That(airport.MovementsPerHour, Is.LessThan(airport.GateMovementsPerHour));
        }

        [Test]
        public void TheTerminalCapsThePassengers()
        {
            var airport = new Airport(1, "A", float2.zero, 0f, 3000f, 100f, Narrow);

            Assert.That(airport.PassengersPerHour, Is.EqualTo(100f).Within(1e-3f));
            Assert.That(airport.Utilisation(50f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(airport.Utilisation(5000f), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(airport.Utilisation(0f), Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void TheRunwayRunsAlongTheHeading()
        {
            var airport = new Airport(1, "A", new float2(100f, 200f), math.PI / 2f, 2000f, 1000f, Narrow);

            Assert.That(airport.RunwayStart.x, Is.EqualTo(100f).Within(1e-2f));
            Assert.That(airport.RunwayStart.y, Is.EqualTo(-800f).Within(1e-2f));
            Assert.That(airport.RunwayEnd.y, Is.EqualTo(1200f).Within(1e-2f));
        }

        [Test]
        public void AirportsMustBeFarEnoughApart()
        {
            var first = _network.Add("A", float2.zero, 0f, 2500f, 1000f, Narrow);
            _network.Add("B", new float2(AviationRules.MinAirportSpacing, 0f), 0f, 2500f, 1000f, Narrow);

            Assert.Throws<ArgumentException>(() => _network.Add("C", new float2(100f, 0f), 0f, 2500f, 1000f, Narrow));
            Assert.That(_network.Airports.Count(), Is.EqualTo(2));
            Assert.That(_network.Get(first).Name, Is.EqualTo("A"));
            Assert.Throws<ArgumentException>(() => _network.Get(99));
            Assert.That(_network.NextId, Is.EqualTo(3));
        }

        [Test]
        public void UpdatingAnAirportKeepsItsPlaceAndFailsAtomically()
        {
            var id = _network.Add("A", new float2(5f, 6f), 0.5f, 2500f, 1000f, Narrow);

            _network.Update(id, 3200f, 2000f, new[] { AircraftClass.Widebody });
            var airport = _network.Get(id);
            Assert.That(airport.RunwayLength, Is.EqualTo(3200f));
            Assert.That(airport.Position, Is.EqualTo(new float2(5f, 6f)));
            Assert.That(airport.Heading, Is.EqualTo(0.5f));

            Assert.Throws<ArgumentOutOfRangeException>(() => _network.Update(id, 100f, 2000f, Narrow));
            Assert.That(_network.Get(id).RunwayLength, Is.EqualTo(3200f));
            Assert.Throws<ArgumentException>(() => _network.Update(99, 3200f, 2000f, Narrow));
        }

        [Test]
        public void RemovingAnAirportForgetsItsStats()
        {
            var id = _network.Add("A", float2.zero, 0f, 2500f, 1000f, Narrow);

            Assert.That(_network.Remove(id), Is.True);
            Assert.That(_network.Remove(id), Is.False);
            Assert.Throws<ArgumentException>(() => _network.StatsOf(id));
        }

        [Test]
        public void ADemandBeyondTheCapacityIsOnlyServedUpToTheCapacity()
        {
            var id = _network.Add("A", float2.zero, 0f, 2500f, 100_000f, Narrow);
            var airport = _network.Get(id);

            _network.SetDemand(id, airport.PassengersPerHour * 2f);
            _network.Step(1800f);

            var stats = _network.StatsOf(id);
            Assert.That(stats.Passengers, Is.EqualTo(airport.PassengersPerHour * 0.5).Within(1e-2));
            Assert.That(stats.Movements, Is.EqualTo(airport.MovementsPerHour * 0.5).Within(1e-2));
            Assert.That(_network.Utilisation(id), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void ALowDemandIsFullyServed()
        {
            var id = _network.Add("A", float2.zero, 0f, 2500f, 100_000f, Narrow);

            _network.SetDemand(id, 360f);
            _network.Step(3600f);

            Assert.That(_network.StatsOf(id).Passengers, Is.EqualTo(360.0).Within(1e-3));
            Assert.Throws<ArgumentOutOfRangeException>(() => _network.SetDemand(id, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _network.SetDemand(id, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => _network.Step(-1f));
            Assert.Throws<ArgumentException>(() => _network.SetDemand(99, 1f));
        }

        [Test]
        public void AnIdleAirportMakesNoNoise()
        {
            var id = _network.Add("A", float2.zero, 0f, 2500f, 100_000f, Narrow);

            Assert.That(_network.NoiseSources(), Is.Empty);
            Assert.That(AirportNoise.Sources(_network.Get(id), 0f), Is.Empty);
            Assert.Throws<ArgumentNullException>(() => AirportNoise.Sources(null, 1f));
        }

        [Test]
        public void ABusyAirportMakesMoreNoiseThanAQuietOne()
        {
            var id = _network.Add("A", float2.zero, 0f, 3200f, 100_000f, Narrow);
            var airport = _network.Get(id);

            var quiet = AirportNoise.Sources(airport, 0.2f);
            var busy = AirportNoise.Sources(airport, 0.9f);

            Assert.That(quiet.All(s => s.Kind == NoiseKind.Airport), Is.True);
            Assert.That(busy.Length, Is.GreaterThan(quiet.Length));
            var point = new float2(0f, 1500f);
            Assert.That(NoiseModel.LevelAt(point, busy), Is.GreaterThan(NoiseModel.LevelAt(point, quiet)));
        }

        [Test]
        public void HeavyAircraftAreLouderThanRegionalOnes()
        {
            var regional = new Airport(1, "R", float2.zero, 0f, 3200f, 100_000f, new[] { AircraftClass.Regional });
            var heavy = new Airport(2, "W", float2.zero, 0f, 3200f, 100_000f, new[] { AircraftClass.Widebody });
            var point = new float2(0f, 2000f);

            var regionalLevel = NoiseModel.LevelAt(point, AirportNoise.Sources(regional, 0.3f));
            var heavyLevel = NoiseModel.LevelAt(point, AirportNoise.Sources(heavy, 0.3f));

            Assert.That(heavyLevel, Is.GreaterThan(regionalLevel + 2f));
        }

        [Test]
        public void TheNoiseFeedsTheNoiseModelAndFadesWithDistance()
        {
            var id = _network.Add("A", float2.zero, 0f, 3000f, 100_000f, Narrow);
            _network.SetDemand(id, 1000f);
            var sources = _network.NoiseSources();

            var near = NoiseModel.LevelAt(new float2(0f, 500f), sources);
            var far = NoiseModel.LevelAt(new float2(0f, 20_000f), sources);

            Assert.That(sources, Is.Not.Empty);
            Assert.That(near, Is.GreaterThan(far));
            Assert.That(NoiseModel.Annoyance(near), Is.GreaterThan(0.4f));
            Assert.That(AirportNoise.ContourRadius(45f), Is.GreaterThan(AirportNoise.ContourRadius(65f)));
            Assert.That(AirportNoise.ContourRadius(120f), Is.EqualTo(1f).Within(1e-3f));
        }

        [Test]
        public void TheSectionRoundTrips()
        {
            var a = _network.Add("Schiphol", new float2(100f, 200f), 0.3f, 3400f, 60_000f, new[] { AircraftClass.Widebody, AircraftClass.Regional });
            _network.Add("Lelystad", new float2(40_000f, 0f), 1f, 2400f, 8000f, Narrow);
            _network.SetDemand(a, 2500f);
            _network.Step(7200f);
            var section = new AirportSection(_network);
            var bytes = SaveBytes.Write(new SaveMetadata(), section);

            var restored = new AirportNetwork();
            restored.Add("Oud", float2.zero, 0f, 2500f, 1000f, Narrow);
            SaveBytes.Read(bytes, new AirportSection(restored));

            Assert.That(restored.Airports.Select(x => x.Name), Is.EqualTo(new[] { "Schiphol", "Lelystad" }));
            Assert.That(restored.Get(a).Gates, Is.EqualTo(new[] { AircraftClass.Widebody, AircraftClass.Regional }));
            Assert.That(restored.StatsOf(a).Passengers, Is.EqualTo(_network.StatsOf(a).Passengers));
            Assert.That(restored.StatsOf(a).Demand, Is.EqualTo(2500f));
            Assert.That(restored.NextId, Is.EqualTo(3));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new AirportSection(restored)), Is.EqualTo(bytes));
            Assert.That(section.Id, Is.EqualTo(AirportSection.SectionId));
            Assert.That(AirportSection.SectionId, Is.EqualTo("AIRP"));
            Assert.That(section.Version, Is.EqualTo(1));
            Assert.Throws<ArgumentNullException>(() => new AirportSection(null));
        }

        [Test]
        public void TheSectionRejectsImpossibleData()
        {
            var section = new AirportSection(_network);

            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => { w.Write(1); w.Write(-1); }), 1), "negatief aantal");
            Assert.Throws<SaveFormatException>(
                () => section.Read(SaveBytes.ReaderFor(w => { w.Write(1); w.Write(1); WriteAirport(w, 1, 0f, 2500f, 0, 1); }), 1),
                "ID niet kleiner dan de volgende");
            Assert.Throws<SaveFormatException>(
                () => section.Read(SaveBytes.ReaderFor(w => { w.Write(5); w.Write(1); WriteAirport(w, 1, 0f, 2500f, 0, 0); }), 1),
                "geen gates");
            Assert.Throws<SaveFormatException>(
                () => section.Read(SaveBytes.ReaderFor(w => { w.Write(5); w.Write(1); WriteAirport(w, 1, 0f, 2500f, 9, 1); }), 1),
                "onbekende soort");
            Assert.Throws<SaveFormatException>(
                () => section.Read(SaveBytes.ReaderFor(w => { w.Write(5); w.Write(1); WriteAirport(w, 1, -5f, 2500f, 0, 1); }), 1),
                "negatieve vraag");
            Assert.Throws<SaveFormatException>(
                () => section.Read(SaveBytes.ReaderFor(w => { w.Write(5); w.Write(1); WriteAirport(w, 1, 0f, 100f, 0, 1); }), 1),
                "baan te kort");
            Assert.Throws<SaveFormatException>(
                () => section.Read(SaveBytes.ReaderFor(w => { w.Write(5); w.Write(2); WriteAirport(w, 1, 0f, 2500f, 0, 1); WriteAirport(w, 1, 0f, 2500f, 0, 1); }), 1),
                "dubbele ID");
            Assert.Throws<SaveFormatException>(
                () => section.Read(SaveBytes.ReaderFor(w => { w.Write(5); w.Write(2); WriteAirport(w, 1, 0f, 2500f, 0, 1); WriteAirport(w, 2, 0f, 2500f, 0, 1); }), 1),
                "te dicht bij elkaar");
            Assert.That(_network.Airports, Is.Empty);
        }

        private static void WriteAirport(SaveWriter w, int id, float demand, float runway, byte gateKind, int gates)
        {
            w.Write(id);
            w.Write("Test");
            w.Write(0f);
            w.Write(0f);
            w.Write(0f);
            w.Write(runway);
            w.Write(1000f);
            w.Write(demand);
            w.Write(0.0);
            w.Write(0.0);
            w.Write(gates);
            for (var i = 0; i < gates; i++)
            {
                w.Write(gateKind);
            }
        }
    }
}
