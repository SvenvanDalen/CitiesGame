using System;
using Cities.Core.Persistence;
using Cities.Simulation.Freight.Terminals;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Freight
{
    public class TerminalTests
    {
        private static TerminalSpec Port(int berths = 1, float crane = 100f, float gate = 50f, float storage = 500f, float maxDock = 5f) =>
            new TerminalSpec(TerminalKind.Port, berths, crane, gate, storage, maxDock);

        private static TerminalSpec Rail() => new TerminalSpec(TerminalKind.RailTerminal, 2, 80f, 40f, 300f, 3f);

        private static FreightTerminal NewTerminal(TerminalSpec spec) => new TerminalNetwork().Add(1, float3.zero, spec);

        [Test]
        public void AShipUnloadsIntoStorageLimitedByTheCrane()
        {
            var terminal = NewTerminal(Port());
            var ship = terminal.Arrive(CarrierMode.Ship, 400f, 250f, 0f);

            terminal.Step(1f);

            Assert.That(terminal.Level, Is.EqualTo(100f).Within(0.01f));
            Assert.That(ship.State, Is.EqualTo(CarrierState.Docked));
            terminal.Step(1f);
            terminal.Step(1f);
            Assert.That(terminal.Level, Is.EqualTo(250f).Within(0.01f));
            Assert.That(ship.State, Is.EqualTo(CarrierState.Departed));
            Assert.That(terminal.FromCarriers, Is.EqualTo(250f).Within(0.01f));
        }

        [Test]
        public void TrucksPickUpWhatTheShipDroppedWithinTheGateCapacity()
        {
            var terminal = NewTerminal(Port());
            terminal.Arrive(CarrierMode.Ship, 400f, 200f, 0f);
            terminal.Step(1f);
            terminal.Step(1f);

            Assert.That(terminal.LoadTruck(30f), Is.EqualTo(30f).Within(0.01f));
            Assert.That(terminal.LoadTruck(30f), Is.EqualTo(20f).Within(0.01f), "poort geeft hoogstens 50 per dag");
            terminal.Step(1f);
            Assert.That(terminal.LoadTruck(500f), Is.EqualTo(50f).Within(0.01f));
            Assert.That(terminal.ToTrucks, Is.EqualTo(100f).Within(0.01f));
            Assert.That(terminal.Level, Is.EqualTo(100f).Within(0.01f));
        }

        [Test]
        public void TrucksFeedATrainViaTheTerminal()
        {
            var terminal = NewTerminal(Rail());
            terminal.Step(1f);
            Assert.That(terminal.ReceiveFromTruck(60f), Is.EqualTo(40f).Within(0.01f), "poort beperkt");
            terminal.Step(1f);
            Assert.That(terminal.ReceiveFromTruck(20f), Is.EqualTo(20f).Within(0.01f));
            var train = terminal.Arrive(CarrierMode.Train, 100f, 0f, 60f);

            terminal.Step(1f);

            Assert.That(train.Loaded, Is.EqualTo(60f).Within(0.01f));
            Assert.That(train.State, Is.EqualTo(CarrierState.Departed));
            Assert.That(terminal.Level, Is.EqualTo(0f).Within(0.01f));
            Assert.That(terminal.Refused, Is.EqualTo(20f).Within(0.01f));
            var departed = terminal.TakeDeparted();
            Assert.That(departed, Has.Count.EqualTo(1));
            Assert.That(terminal.Carriers, Is.Empty);
        }

        [Test]
        public void ShipToTruckAndBackRoundTrips()
        {
            var terminal = NewTerminal(Port(storage: 1000f, gate: 1000f));
            terminal.Arrive(CarrierMode.Ship, 300f, 100f, 0f);
            terminal.Step(1f);
            Assert.That(terminal.LoadTruck(100f), Is.EqualTo(100f).Within(0.01f));
            Assert.That(terminal.ReceiveFromTruck(80f), Is.EqualTo(80f).Within(0.01f));
            var ship = terminal.Arrive(CarrierMode.Ship, 300f, 0f, 80f);
            terminal.Step(1f);
            Assert.That(ship.Loaded, Is.EqualTo(80f).Within(0.01f));
            Assert.That(ship.Load, Is.EqualTo(80f).Within(0.01f));
            Assert.That(terminal.ToCarriers, Is.EqualTo(80f).Within(0.01f));
        }

        [Test]
        public void ShipsQueueForABerthAndWait()
        {
            var terminal = NewTerminal(Port(berths: 1, crane: 50f));
            var first = terminal.Arrive(CarrierMode.Ship, 400f, 100f, 0f);
            var second = terminal.Arrive(CarrierMode.Ship, 400f, 50f, 0f);

            terminal.Step(1f);

            Assert.That(terminal.DockedCount, Is.EqualTo(1));
            Assert.That(terminal.QueuedCount, Is.EqualTo(1));
            Assert.That(second.WaitedDays, Is.EqualTo(1f).Within(0.001f));
            terminal.Step(1f);
            Assert.That(first.State, Is.EqualTo(CarrierState.Departed));
            terminal.Step(1f);
            Assert.That(second.State, Is.EqualTo(CarrierState.Departed));
            Assert.That(terminal.DepartedCount, Is.EqualTo(2));
        }

        [Test]
        public void FullStorageBlocksUnloadingUntilTheShipLeavesAnyway()
        {
            var terminal = NewTerminal(Port(storage: 40f, maxDock: 2f));
            var ship = terminal.Arrive(CarrierMode.Ship, 400f, 300f, 0f);

            terminal.Step(1f);
            Assert.That(terminal.Level, Is.EqualTo(40f).Within(0.01f));
            Assert.That(ship.State, Is.EqualTo(CarrierState.Docked));
            terminal.Step(1f);

            Assert.That(ship.State, Is.EqualTo(CarrierState.Departed), "na de maximale ligtijd");
            Assert.That(ship.Load, Is.EqualTo(260f).Within(0.01f));
            Assert.That(terminal.ReceiveFromTruck(10f), Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void ALoadedShipWaitsForOutboundCargoThatIsNotThereYet()
        {
            var terminal = NewTerminal(Port());
            var ship = terminal.Arrive(CarrierMode.Ship, 200f, 0f, 100f);
            terminal.Step(1f);
            Assert.That(ship.State, Is.EqualTo(CarrierState.Docked));
            terminal.Step(1f);
            terminal.Step(1f);
            terminal.Step(1f);
            terminal.Step(1f);
            Assert.That(ship.State, Is.EqualTo(CarrierState.Departed));
            Assert.That(ship.Loaded, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void NearestFindsTheClosestTerminalOfAKind()
        {
            var network = new TerminalNetwork();
            network.Add(1, new float3(100, 0, 0), Port());
            var near = network.Add(2, new float3(10, 0, 0), Port());
            network.Add(3, new float3(1, 0, 0), Rail());

            Assert.That(network.Nearest(TerminalKind.Port, float3.zero), Is.SameAs(near));
            Assert.That(network.Nearest(TerminalKind.RailTerminal, float3.zero).Owner, Is.EqualTo(3UL));
            Assert.That(new TerminalNetwork().Nearest(TerminalKind.Port, float3.zero), Is.Null);
            network.Step(1f);
            Assert.That(network.Get(0).GateBudget, Is.EqualTo(50f).Within(0.01f));
        }

        [Test]
        public void InputsAreChecked()
        {
            var network = new TerminalNetwork();
            var terminal = network.Add(1, float3.zero, Port());
            Assert.Throws<ArgumentException>(() => terminal.Arrive(CarrierMode.Train, 10f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => terminal.Arrive(CarrierMode.Ship, 10f, 20f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => terminal.Arrive(CarrierMode.Ship, 10f, 0f, 20f));
            Assert.Throws<ArgumentOutOfRangeException>(() => terminal.ReceiveFromTruck(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => terminal.LoadTruck(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => terminal.Step(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => network.Step(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => network.Get(5));
            Assert.Throws<ArgumentNullException>(() => network.Add(1, float3.zero, null));
            Assert.Throws<ArgumentNullException>(() => new TerminalSection(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TerminalSpec(TerminalKind.Port, 0, 1f, 1f, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TerminalSpec(TerminalKind.Port, 1, 0f, 1f, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TerminalSpec((TerminalKind)9, 1, 1f, 1f, 1f, 1f));
        }

        [Test]
        public void TerminalsSurviveSavingAndLoading()
        {
            var network = new TerminalNetwork();
            var port = network.Add(7, new float3(1, 2, 3), Port(berths: 1, crane: 40f));
            network.Add(8, new float3(9, 0, 0), Rail());
            port.Arrive(CarrierMode.Ship, 300f, 150f, 20f);
            port.Arrive(CarrierMode.Ship, 300f, 60f, 0f);
            network.Step(1f);
            port.ReceiveFromTruck(10f);
            var bytes = SaveBytes.Write(new SaveMetadata(), new TerminalSection(network));

            var loaded = new TerminalNetwork();
            SaveBytes.Read(bytes, new TerminalSection(loaded));

            Assert.That(loaded.Terminals.Count, Is.EqualTo(2));
            var copy = loaded.Get(0);
            Assert.That(copy.Owner, Is.EqualTo(7UL));
            Assert.That(copy.Position, Is.EqualTo(new float3(1, 2, 3)));
            Assert.That(copy.Level, Is.EqualTo(port.Level).Within(0.001f));
            Assert.That(copy.Carriers.Count, Is.EqualTo(2));
            Assert.That(copy.QueuedCount, Is.EqualTo(1));
            Assert.That(copy.Arrive(CarrierMode.Ship, 10f, 0f, 0f).Id, Is.EqualTo(2));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new TerminalSection(loaded)), Is.Not.EqualTo(bytes));
            var again = new TerminalNetwork();
            SaveBytes.Read(bytes, new TerminalSection(again));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new TerminalSection(again)), Is.EqualTo(bytes), "deterministisch opgeslagen");
        }
    }
}
