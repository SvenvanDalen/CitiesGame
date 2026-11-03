using System;
using Cities.Core.Persistence;
using Cities.Simulation.Network;
using Cities.Simulation.Pedestrians;
using Cities.Simulation.Vehicles;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Pedestrians
{
    public class WalkerSimulationTests
    {
        private PathNetwork _net;
        private WalkerSimulation _sim;
        private int _a;
        private int _c;

        // a --10 m pad-- b --7 m oversteek-- c
        private void Build(PedestrianCrossing crossing)
        {
            _net = new PathNetwork();
            _a = _net.AddNode(new float3(0f, 0f, 0f));
            var b = _net.AddNode(new float3(10f, 0f, 0f));
            _c = _net.AddNode(new float3(10f, 0f, 7f));
            _net.AddPath(_a, b, PathModes.Both);
            _sim = new WalkerSimulation(_net);
            _net.AddCrossing(b, _c, PathModes.Both, _sim.AddCrossing(crossing), 12f);
        }

        private int Walk(PathMode mode = PathMode.Walk) => _sim.Add(mode, _net.Route(_a, _c, mode));

        [Test]
        public void ACrossingAddsItsExpectedWaitToTheRoute()
        {
            Build(new PedestrianCrossing(CrossingKind.Zebra, 7f));

            var route = _net.Route(_a, _c, PathMode.Walk);

            Assert.That(route.Seconds, Is.EqualTo((17f / PathNetwork.WalkSpeed) + 12f).Within(1e-2f));
            Assert.That(route.Length, Is.EqualTo(17f).Within(1e-3f));
        }

        [Test]
        public void AWalkerArrivesWhenNoVehicleComes()
        {
            Build(new PedestrianCrossing(CrossingKind.Zebra, 7f));
            Walk();

            for (var i = 0; i < 20; i++)
            {
                _sim.Step(1f);
            }

            Assert.That(_sim.Walkers, Is.Empty);
            Assert.That(_sim.Crossings[0].Occupants, Is.Zero);
        }

        [Test]
        public void AWalkerWaitsForAVehicleThatIsTooClose()
        {
            var crossing = new PedestrianCrossing(CrossingKind.Zebra, 7f);
            Build(crossing);
            Walk();

            for (var i = 0; i < 20; i++)
            {
                _sim.Step(1f, _ => 3f);
            }

            Assert.That(_sim.Walkers, Has.Count.EqualTo(1));
            Assert.That(_sim.Walkers[0].Offset, Is.Zero, "wacht voor het zebrapad");
            Assert.That(crossing.VehiclesMustStop, Is.False);

            for (var i = 0; i < 20; i++)
            {
                _sim.Step(1f, _ => 60f);
            }

            Assert.That(_sim.Walkers, Is.Empty);
        }

        [Test]
        public void VehiclesMustStopWhileSomeoneIsOnTheCrossing()
        {
            var crossing = new PedestrianCrossing(CrossingKind.Zebra, 7f);
            Build(crossing);
            Walk();

            _sim.Step(8f);
            Assert.That(_sim.Walkers[0].OnCrossing, Is.True);
            Assert.That(crossing.VehiclesMustStop, Is.True);

            _sim.Step(8f);
            Assert.That(crossing.VehiclesMustStop, Is.False);
        }

        [Test]
        public void ACyclistCrossesFasterThanAWalker()
        {
            var crossing = new PedestrianCrossing(CrossingKind.Zebra, 7f);

            Assert.That(crossing.CrossingSeconds(PathMode.Bike), Is.LessThan(crossing.CrossingSeconds(PathMode.Walk)));
            Assert.That(crossing.MayEnter(PathMode.Bike, 6f), Is.True);
            Assert.That(crossing.MayEnter(PathMode.Walk, 6f), Is.False);
        }

        [Test]
        public void SignalCrossingOnlyOpensWhileVehiclesStayRedLongEnough()
        {
            var plan = new SignalPlan(2);
            var crossing = new PedestrianCrossing(CrossingKind.Signal, 7f, plan, 0);

            Assert.That(crossing.MayEnter(PathMode.Walk, float.PositiveInfinity), Is.False, "groep 0 is groen");

            plan.Tick(plan.GreenOf(0) + SignalPlan.DefaultClearance + 1f);
            Assert.That(plan.Phase, Is.EqualTo(1));
            Assert.That(crossing.MayEnter(PathMode.Walk, 0f), Is.True, "groep 1 groen, groep 0 nog lang rood");

            plan.Tick(plan.GreenOf(1) - 1f);
            Assert.That(crossing.MayEnter(PathMode.Walk, 0f), Is.False, "rood duurt niet lang genoeg meer");
        }

        [Test]
        public void ADisabledPlanFallsBackToTheGapRule()
        {
            var plan = new SignalPlan(1) { Enabled = false };
            var crossing = new PedestrianCrossing(CrossingKind.Signal, 7f, plan);

            Assert.That(crossing.MayEnter(PathMode.Walk, 1f), Is.False);
            Assert.That(crossing.MayEnter(PathMode.Walk, 60f), Is.True);
        }

        [Test]
        public void SecondsUntilOpenCountsTheClearanceAndOtherGroups()
        {
            var plan = new SignalPlan(3, 4f);
            plan.SetGreen(0, 10f);
            plan.SetGreen(1, 6f);
            plan.SetGreen(2, 8f);
            plan.Tick(3f);

            Assert.That(plan.SecondsUntilOpen(0), Is.Zero);
            Assert.That(plan.SecondsUntilOpen(1), Is.EqualTo(11f).Within(1e-4f));
            Assert.That(plan.SecondsUntilOpen(2), Is.EqualTo(21f).Within(1e-4f));
            plan.Tick(8f);
            Assert.That(plan.SecondsUntilOpen(0), Is.EqualTo(25f).Within(1e-4f), "in de ontruiming van groep 0");
        }

        [Test]
        public void InvalidArgumentsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PedestrianCrossing(CrossingKind.Zebra, 0f));
            Assert.Throws<ArgumentNullException>(() => new WalkerSimulation(null));
            Build(new PedestrianCrossing(CrossingKind.Zebra, 7f));
            Assert.Throws<ArgumentNullException>(() => _sim.AddCrossing(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => _sim.Step(0f));
            Assert.Throws<ArgumentException>(() => _net.AddCrossing(0, 1, PathModes.None, 0, 1f));
            Assert.That(_sim.Add(PathMode.Walk, _net.Route(_a, _a, PathMode.Walk)), Is.EqualTo(-1));
        }

        [Test]
        public void WalkersSurviveSavingAndLoadingMidCrossing()
        {
            var crossing = new PedestrianCrossing(CrossingKind.Zebra, 7f);
            Build(crossing);
            var id = Walk();
            Walk(PathMode.Bike);
            _sim.Step(10f);
            Assert.That(crossing.Occupants, Is.GreaterThan(0));
            var bytes = SaveBytes.Write(new SaveMetadata(), new WalkerSection(_sim));

            var loadedCrossing = new PedestrianCrossing(CrossingKind.Zebra, 7f);
            var loaded = new WalkerSimulation(_net);
            loaded.AddCrossing(loadedCrossing);
            SaveBytes.Read(bytes, new WalkerSection(loaded));

            Assert.That(loaded.Walkers, Has.Count.EqualTo(_sim.Walkers.Count));
            Assert.That(loadedCrossing.Occupants, Is.EqualTo(crossing.Occupants));
            Assert.That(loaded.NextId, Is.EqualTo(_sim.NextId));
            Assert.That(loaded.Walkers[0].Id, Is.EqualTo(_sim.Walkers[0].Id));
            Assert.That(id, Is.EqualTo(1));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new WalkerSection(loaded)), Is.EqualTo(bytes), "deterministisch opgeslagen");
        }

        [Test]
        public void ASaveThatDoesNotFitTheNetworkIsRejected()
        {
            Build(new PedestrianCrossing(CrossingKind.Zebra, 7f));
            Walk();
            var other = new WalkerSection(new WalkerSimulation(new PathNetwork()));

            Assert.Throws<SaveFormatException>(() => other.Read(SaveBytes.ReaderFor(w => new WalkerSection(_sim).Write(w)), 1));
            Assert.Throws<ArgumentNullException>(() => new WalkerSection(null));
        }
    }
}
