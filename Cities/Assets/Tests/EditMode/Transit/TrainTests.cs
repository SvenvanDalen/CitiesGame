using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;
using Cities.Simulation.Transit;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Transit
{
    public class TrainTests
    {
        private TransitNetwork _transit;
        private RailNetwork _rail;
        private int _n1;
        private int _n2;
        private int _n3;
        private int _n4;
        private int _t1;
        private int _t2;
        private int _t3;

        [SetUp]
        public void SetUp()
        {
            // Een T-splitsing: n1 - n2 - n3 in een rechte lijn en een zijtak van n2 naar n4. n2 is dus een wissel met stam t1.
            _transit = new TransitNetwork();
            _rail = new RailNetwork(_transit);
            _n1 = _rail.AddNode(new float2(0f, 0f));
            _n2 = _rail.AddNode(new float2(2000f, 0f));
            _n3 = _rail.AddNode(new float2(4000f, 0f));
            _n4 = _rail.AddNode(new float2(2000f, 2000f));
            _t1 = _rail.AddTrack(_n1, _n2);
            _t2 = _rail.AddTrack(_n2, _n3);
            _t3 = _rail.AddTrack(_n2, _n4);
        }

        private void AddStations()
        {
            _rail.AddStation(_n1, TrainKind.Passenger, 300f);
            _rail.AddStation(_n3, TrainKind.Passenger, 300f);
            _rail.AddStation(_n4, TrainKind.Freight, 200f);
        }

        private int AddGoods()
        {
            var goods = _rail.AddNode(new float2(-2000f, 0f));
            _rail.AddTrack(goods, _n1);
            _rail.AddStation(goods, TrainKind.Freight, 400f);
            return goods;
        }

        [Test]
        public void TheRulesDifferPerKindOfTrain()
        {
            Assert.That(Train.TopSpeed(TrainKind.Passenger), Is.GreaterThan(Train.TopSpeed(TrainKind.Freight)));
            Assert.That(Train.Acceleration(TrainKind.Passenger), Is.GreaterThan(Train.Acceleration(TrainKind.Freight)));
            Assert.That(Train.Length(TrainKind.Passenger, 4), Is.EqualTo(Train.LocomotiveLength + 4 * Train.PassengerCarLength));
            Assert.That(Train.Length(TrainKind.Freight, 10), Is.EqualTo(Train.LocomotiveLength + 10 * Train.FreightWagonLength));
            Assert.That(Train.Capacity(TrainKind.Passenger, 5), Is.EqualTo(500));
            Assert.That(Train.Capacity(TrainKind.Freight, 5), Is.EqualTo(250));
            Assert.Throws<ArgumentOutOfRangeException>(() => Train.Length(TrainKind.Passenger, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Train.Capacity(TrainKind.Freight, Train.MaxCars + 1));
        }

        [Test]
        public void ThePlatformLimitsTheNumberOfCars()
        {
            Assert.That(Train.MaxCarsFor(TrainKind.Passenger, 120f), Is.EqualTo(4));
            Assert.That(Train.MaxCarsFor(TrainKind.Passenger, 20f), Is.Zero);
            Assert.That(Train.MaxCarsFor(TrainKind.Freight, 10000f), Is.EqualTo(Train.MaxCars));
            Assert.Throws<ArgumentOutOfRangeException>(() => Train.CheckPlatform(10f));
            Assert.DoesNotThrow(() => Train.CheckPlatform(Train.MinPlatformLength));
        }

        [Test]
        public void ATrainReachesTopSpeedOnlyOnALongStretch()
        {
            Assert.That(Train.TravelTime(TrainKind.Passenger, 0f), Is.Zero);
            var shortRide = Train.TravelTime(TrainKind.Freight, 100f);
            Assert.That(shortRide, Is.EqualTo(2f * math.sqrt(100f / Train.FreightAcceleration)).Within(1e-3f));

            var distance = 50000f;
            var expected = distance / Train.PassengerSpeed + Train.PassengerSpeed / Train.PassengerAcceleration;
            Assert.That(Train.TravelTime(TrainKind.Passenger, distance), Is.EqualTo(expected).Within(5e-2f));
            Assert.That(Train.TravelTime(TrainKind.Freight, distance), Is.GreaterThan(Train.TravelTime(TrainKind.Passenger, distance)));
            Assert.Throws<ArgumentOutOfRangeException>(() => Train.TravelTime(TrainKind.Passenger, -1f));
        }

        [Test]
        public void TheBarriersCloseBeforeTheTrainArrivesAndOpenAfterTheLastCar()
        {
            var speed = Train.PassengerSpeed;
            var length = 100f;
            Assert.That(Train.ShouldClose(speed * Train.CrossingWarningTime + 1f, speed, length), Is.False, "te vroeg");
            Assert.That(Train.ShouldClose(speed * Train.CrossingWarningTime, speed, length), Is.True);
            Assert.That(Train.ShouldClose(-50f, speed, length), Is.True, "de trein rijdt er nog over");
            Assert.That(Train.ShouldClose(-(length + Train.CrossingClearDistance) - 1f, speed, length), Is.False, "voorbij");
            Assert.That(Train.CrossingClosedTime(TrainKind.Passenger, 3), Is.EqualTo(Train.CrossingWarningTime + (Train.Length(TrainKind.Passenger, 3) + Train.CrossingClearDistance) / Train.PassengerSpeed).Within(1e-3f));
            Assert.That(Train.CrossingClosedTime(TrainKind.Freight, 3), Is.GreaterThan(Train.CrossingClosedTime(TrainKind.Passenger, 3)));
        }

        [Test]
        public void TracksNeedRealNodesRoomAndALength()
        {
            Assert.That(_rail.Degree(_n2), Is.EqualTo(3));
            Assert.That(_rail.GetTrack(_t1).Length, Is.EqualTo(2000f).Within(1e-3f));
            Assert.That(_rail.GetTrack(_t1).Other(_n1), Is.EqualTo(_n2));
            Assert.That(_rail.GetTrack(_t1).Other(_n2), Is.EqualTo(_n1));
            Assert.That(_rail.TracksAt(_n2).Select(t => t.Id), Is.EqualTo(new[] { _t1, _t2, _t3 }));

            Assert.Throws<ArgumentException>(() => _rail.AddTrack(_n1, _n2), "dubbel spoor");
            Assert.Throws<ArgumentException>(() => _rail.AddTrack(_n1, 99), "onbekende knoop");
            var near = _rail.AddNode(new float2(10f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _rail.AddTrack(_n1, near), "te kort");
            Assert.Throws<ArgumentException>(() => _rail.AddTrack(_n1, _n1), "met zichzelf");
            var extra = _rail.AddNode(new float2(2000f, -2000f));
            Assert.Throws<ArgumentException>(() => _rail.AddTrack(_n2, extra), "een vierde spoor past niet");
            Assert.That(_rail.Tracks.Count(), Is.EqualTo(3), "een mislukt spoor laat niets achter");
            Assert.Throws<ArgumentException>(() => _rail.GetTrack(99));
            Assert.Throws<ArgumentException>(() => _rail.PositionOf(99));
            Assert.That(_rail.HasNode(_n1), Is.True);
            Assert.Throws<ArgumentNullException>(() => new RailNetwork(null));
            Assert.That(_rail.Transit, Is.SameAs(_transit));
        }

        [Test]
        public void ASwitchHasAStemAndASelectedBranch()
        {
            Assert.That(_rail.IsSwitch(_n2), Is.True);
            Assert.That(_rail.IsSwitch(_n1), Is.False);
            Assert.That(_rail.StemOf(_n2), Is.EqualTo(_t1));
            Assert.That(_rail.SelectedTrack(_n2), Is.EqualTo(_t2));
            Assert.That(_rail.Switches.ToArray(), Is.EqualTo(new[] { new KeyValuePair<int, int>(_n2, _t2) }));

            Assert.That(_rail.TrySetSwitch(_n2, _t3), Is.True);
            Assert.That(_rail.SelectedTrack(_n2), Is.EqualTo(_t3));
            Assert.That(_rail.TrySetSwitch(_n2, _t3), Is.True, "al goed gelegd");

            Assert.Throws<ArgumentException>(() => _rail.TrySetSwitch(_n2, _t1), "de stam is geen tak");
            Assert.Throws<ArgumentException>(() => _rail.TrySetSwitch(_n1, _t1), "geen wissel");
            Assert.Throws<ArgumentException>(() => _rail.StemOf(_n1));
            Assert.Throws<ArgumentException>(() => _rail.SelectedTrack(_n1));
            var extra = _rail.AddNode(new float2(9000f, 0f));
            var far = _rail.AddNode(new float2(12000f, 0f));
            var other = _rail.AddTrack(extra, far);
            Assert.Throws<ArgumentException>(() => _rail.TrySetSwitch(_n2, other), "ligt niet aan de wissel");
        }

        [Test]
        public void ATrainFollowsTheSwitchAndIsBlockedByAWrongOne()
        {
            Assert.That(_rail.NextTrack(_n2, _t1), Is.EqualTo(_t2), "van de stam naar de gekozen tak");
            Assert.That(_rail.NextTrack(_n2, _t2), Is.EqualTo(_t1), "van de gekozen tak naar de stam");
            Assert.That(_rail.NextTrack(_n2, _t3), Is.EqualTo(-1), "verkeerd gelegd");
            _rail.TrySetSwitch(_n2, _t3);
            Assert.That(_rail.NextTrack(_n2, _t1), Is.EqualTo(_t3));
            Assert.That(_rail.NextTrack(_n1, _t1), Is.EqualTo(-1), "einde van het spoor");
            Assert.That(_rail.NextTrack(_n3, _t2), Is.EqualTo(-1));
            var mid = _rail.AddNode(new float2(4000f, 3000f));
            var next = _rail.AddTrack(_n3, mid);
            Assert.That(_rail.NextTrack(_n3, _t2), Is.EqualTo(next), "gewone knoop gaat door");
        }

        [Test]
        public void PathsKeepToTheRulesOfASwitch()
        {
            Assert.That(_rail.FindPath(_n1, _n3), Is.EqualTo(new[] { _n1, _n2, _n3 }));
            Assert.That(_rail.FindPath(_n3, _n1), Is.EqualTo(new[] { _n3, _n2, _n1 }));
            Assert.That(_rail.FindPath(_n1, _n4), Is.EqualTo(new[] { _n1, _n2, _n4 }));
            Assert.That(_rail.FindPath(_n3, _n4), Is.Empty, "van tak naar tak kan niet");
            Assert.That(_rail.FindPath(_n2, _n2), Is.EqualTo(new[] { _n2 }));
            var island = _rail.AddNode(new float2(9000f, 9000f));
            Assert.That(_rail.FindPath(_n1, island), Is.Empty);
        }

        [Test]
        public void AStationNeedsAFreeNodeAndRoom()
        {
            AddStations();

            Assert.That(_rail.IsStation(_n1), Is.True);
            Assert.That(_rail.GetStation(_n1).Kind, Is.EqualTo(TrainKind.Passenger));
            Assert.That(_rail.GetStation(_n1).PlatformLength, Is.EqualTo(300f));
            Assert.That(_rail.GetStation(_n4).Stop, Is.EqualTo(-1), "een goederenstation is geen halte");
            Assert.That(_transit.Stops.Count(), Is.EqualTo(2));
            Assert.That(_rail.NodeOfStop(_rail.GetStation(_n3).Stop), Is.EqualTo(_n3));
            Assert.That(_rail.NodeOfStop(99), Is.EqualTo(-1));
            Assert.That(_rail.Stations.Count(), Is.EqualTo(3));

            Assert.Throws<ArgumentException>(() => _rail.AddStation(_n1, TrainKind.Freight, 100f), "al een station");
            Assert.Throws<ArgumentException>(() => _rail.AddStation(_n2, TrainKind.Freight, 100f), "op een wissel");
            Assert.Throws<ArgumentException>(() => _rail.GetStation(_n2));
            var near = _rail.AddNode(new float2(500f, 0f));
            Assert.Throws<ArgumentException>(() => _rail.AddStation(near, TrainKind.Passenger, 100f), "te dicht bij een station");
            Assert.That(_transit.Stops.Count(), Is.EqualTo(2), "een mislukt station laat geen halte achter");
            Assert.Throws<ArgumentOutOfRangeException>(() => _rail.AddStation(near, TrainKind.Freight, 10f), "te kort perron");
            Assert.DoesNotThrow(() => _rail.AddStation(near, TrainKind.Freight, 100f), "een goederenstation hoeft geen afstand");
        }

        [Test]
        public void AStationNodeTakesAtMostTwoTracks()
        {
            var end = _rail.AddNode(new float2(-2000f, 0f));
            _rail.AddTrack(end, _n1);
            _rail.AddStation(_n1, TrainKind.Freight, 100f);
            var third = _rail.AddNode(new float2(0f, 2000f));

            Assert.Throws<ArgumentException>(() => _rail.AddTrack(_n1, third));
            Assert.That(_rail.TryRemoveNode(_n1), Is.False, "heeft nog spoor en een station");
            Assert.That(_rail.TryRemoveNode(third), Is.True);
        }

        [Test]
        public void AStationInUseCannotBeRemoved()
        {
            AddStations();
            var stop = _rail.GetStation(_n1).Stop;
            var line = _rail.AddLine("IC1", new[] { stop, _rail.GetStation(_n3).Stop }, false, 2, 600f);

            Assert.That(_rail.TryRemoveStation(_n1), Is.False, "de lijn gebruikt de halte");
            Assert.That(_rail.TryRemoveStation(99), Is.False);
            _transit.RemoveLine(line);
            var train = _rail.AddTrain(TrainKind.Passenger, _n1, _n3, 2);
            Assert.That(_rail.TryRemoveStation(_n1), Is.False, "een trein rijdt erlangs");
            _rail.RemoveTrain(train);
            Assert.That(_rail.TryRemoveStation(_n1), Is.True);
            Assert.That(_rail.IsStation(_n1), Is.False);
            Assert.That(_transit.HasStop(stop), Is.False);
            Assert.That(_rail.TryRemoveStation(_n4), Is.True, "goederenstation heeft geen halte");
        }

        [Test]
        public void APassengerLineNeedsConnectedTrainStations()
        {
            AddStations();
            var s1 = _rail.GetStation(_n1).Stop;
            var s3 = _rail.GetStation(_n3).Stop;
            var surface = _transit.AddStop(new float2(100f, 100f));
            var island = _rail.AddNode(new float2(20000f, 0f));
            var islandNode = _rail.AddNode(new float2(23000f, 0f));
            _rail.AddStation(island, TrainKind.Passenger, 200f);
            _rail.AddTrack(island, islandNode);
            var sIsland = _rail.GetStation(island).Stop;

            var line = _rail.AddLine("IC1", new[] { s1, s3 }, false, 2, 600f);
            Assert.That(_transit.GetLine(line).Stops, Is.EqualTo(new[] { s1, s3 }));
            Assert.DoesNotThrow(() => _rail.CheckLine(new[] { s1, s3 }, true));

            Assert.Throws<ArgumentException>(() => _rail.AddLine("bus", new[] { s1, surface }, false, 1, 60f), "geen treinstation");
            Assert.Throws<ArgumentException>(() => _rail.AddLine("eiland", new[] { s1, sIsland }, false, 1, 60f), "niet verbonden");
            Assert.Throws<ArgumentException>(() => _rail.CheckLine(new[] { s1, s3, sIsland }, false));
            Assert.Throws<ArgumentException>(() => _rail.CheckLine(new[] { sIsland, s1 }, true));
            Assert.Throws<ArgumentNullException>(() => _rail.CheckLine(null, false));
        }

        [Test]
        public void ATrainRunsBetweenTwoStationsOfItsKind()
        {
            AddStations();
            var goods = AddGoods();
            var passenger = _rail.AddTrain(TrainKind.Passenger, _n1, _n3, 4);
            var freight = _rail.AddTrain(TrainKind.Freight, goods, _n4, 2);

            Assert.That(_rail.GetTrain(passenger).Cars, Is.EqualTo(4));
            Assert.That(_rail.GetTrain(passenger).Route, Is.EqualTo(new[] { _n1, _n2, _n3 }));
            Assert.That(_rail.GetTrain(passenger).Length, Is.EqualTo(Train.Length(TrainKind.Passenger, 4)));
            Assert.That(_rail.Trains.Count(), Is.EqualTo(2));
            Assert.That(freight, Is.Not.EqualTo(passenger));
        }

        [Test]
        public void ATrainIsRejectedWhenItDoesNotFit()
        {
            AddStations();
            var island = _rail.AddNode(new float2(20000f, 0f));
            var islandEnd = _rail.AddNode(new float2(23000f, 0f));
            _rail.AddStation(island, TrainKind.Passenger, 200f);
            _rail.AddTrack(island, islandEnd);

            Assert.Throws<ArgumentException>(() => _rail.AddTrain(TrainKind.Freight, _n1, _n3, 2), "passagiersstations");
            Assert.Throws<ArgumentException>(() => _rail.AddTrain(TrainKind.Passenger, _n1, _n2, 2), "geen station");
            Assert.Throws<ArgumentOutOfRangeException>(() => _rail.AddTrain(TrainKind.Passenger, _n1, _n3, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => _rail.AddTrain(TrainKind.Passenger, _n1, _n3, 20), "past niet langs het perron");
            Assert.Throws<InvalidOperationException>(() => _rail.AddTrain(TrainKind.Passenger, _n1, island, 2), "geen route");
            Assert.That(_rail.Trains, Is.Empty);
        }

        [Test]
        public void AFreightTrainSetsTheSwitchForItsRoute()
        {
            AddStations();
            var goods = AddGoods();

            Assert.That(_rail.SelectedTrack(_n2), Is.EqualTo(_t2));
            var train = _rail.AddTrain(TrainKind.Freight, goods, _n4, 8);

            Assert.That(_rail.SelectedTrack(_n2), Is.EqualTo(_t3), "de wissel ligt naar het goederenstation");
            Assert.That(_rail.GetTrain(train).Route, Is.EqualTo(new[] { goods, _n1, _n2, _n4 }));
            Assert.That(_rail.RouteLength(_rail.GetTrain(train)), Is.EqualTo(2000f + 2000f + 2000f).Within(1e-2f));
        }

        [Test]
        public void ATrainDrivesAndTurnsAroundAtTheEnd()
        {
            AddStations();
            var id = _rail.AddTrain(TrainKind.Passenger, _n1, _n3, 3);
            var train = _rail.GetTrain(id);

            _rail.Advance(10f);
            Assert.That(train.Position, Is.EqualTo(10f * Train.PassengerSpeed).Within(1e-3f));
            Assert.That(_rail.TrainsOn(_t1), Is.EqualTo(new[] { train }));
            Assert.That(_rail.TrainsOn(_t2), Is.Empty);
            Assert.That(_rail.IsOccupied(_t1), Is.True);
            Assert.That(_rail.IsOccupied(_t3), Is.False);

            _rail.Advance(100f);
            Assert.That(train.Route, Is.EqualTo(new[] { _n3, _n2, _n1 }), "omgekeerd");
            Assert.That(train.Position, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => _rail.Advance(-1f));
            Assert.Throws<ArgumentException>(() => _rail.GetTrain(99));
        }

        [Test]
        public void ASwitchUnderATrainCannotBeThrown()
        {
            AddStations();
            _rail.AddTrain(TrainKind.Passenger, _n1, _n3, 3);
            _rail.Advance(40f);

            Assert.That(_rail.TrySetSwitch(_n2, _t3), Is.False, "de trein staat op een spoor van de wissel");
            Assert.That(_rail.SelectedTrack(_n2), Is.EqualTo(_t2));
            Assert.That(_rail.TryRemoveTrack(_t1), Is.False, "in gebruik");
        }

        [Test]
        public void ARouteCannotBeStartedOverABusySwitch()
        {
            AddStations();
            var goods = AddGoods();
            _rail.AddTrain(TrainKind.Passenger, _n1, _n3, 3);
            _rail.Advance(40f);

            Assert.Throws<InvalidOperationException>(() => _rail.AddTrain(TrainKind.Freight, goods, _n4, 1));
            Assert.That(_rail.Trains.Count(), Is.EqualTo(1));
        }

        [Test]
        public void TrackThatIsFreeCanBeRemoved()
        {
            Assert.That(_rail.TryRemoveTrack(99), Is.False);
            Assert.That(_rail.TryRemoveTrack(_t3), Is.True);
            Assert.That(_rail.IsSwitch(_n2), Is.False, "zonder derde spoor is het geen wissel meer");
            Assert.That(_rail.Degree(_n2), Is.EqualTo(2));
            Assert.That(_rail.NextTrack(_n2, _t1), Is.EqualTo(_t2));
        }

        [Test]
        public void ACrossingNeedsRoomOnTheTrack()
        {
            var id = _rail.AddCrossing(_t1, 1000f, 7);

            Assert.That(_rail.GetCrossing(id).Road, Is.EqualTo(7));
            Assert.That(_rail.GetCrossing(id).IsClosed, Is.False);
            Assert.That(_rail.CrossingsOnRoad(7).Select(c => c.Id), Is.EqualTo(new[] { id }));
            Assert.That(_rail.CrossingsOnRoad(8), Is.Empty);

            Assert.Throws<ArgumentOutOfRangeException>(() => _rail.AddCrossing(_t1, 5f, 8), "te dicht bij de knoop");
            Assert.Throws<ArgumentOutOfRangeException>(() => _rail.AddCrossing(_t1, 1998f, 8));
            Assert.Throws<ArgumentException>(() => _rail.AddCrossing(_t1, 1010f, 8), "te dicht bij een andere overweg");
            Assert.Throws<ArgumentException>(() => _rail.AddCrossing(99, 100f, 8));
            Assert.That(_rail.TryRemoveTrack(_t1), Is.False, "er ligt een overweg op");
            Assert.That(_rail.RemoveCrossing(id), Is.True);
            Assert.That(_rail.RemoveCrossing(id), Is.False);
            Assert.Throws<ArgumentException>(() => _rail.GetCrossing(id));
        }

        [Test]
        public void TheBarriersCloseForAnApproachingTrainAndOpenAfterwards()
        {
            AddStations();
            var near = _rail.AddCrossing(_t1, 1000f, 1);
            var far = _rail.AddCrossing(_t2, 1900f, 2);
            var unused = _rail.AddCrossing(_t3, 1000f, 3);

            _rail.AddTrain(TrainKind.Passenger, _n1, _n3, 3);
            _rail.UpdateCrossings();
            Assert.That(_rail.GetCrossing(near).IsClosed, Is.True, "1000 m is binnen de waarschuwingstijd");
            Assert.That(_rail.GetCrossing(far).IsClosed, Is.False, "nog ver weg");
            Assert.That(_rail.GetCrossing(unused).IsClosed, Is.False, "niet op de route");

            _rail.Advance(26f);
            Assert.That(_rail.GetCrossing(near).IsClosed, Is.False, "de laatste wagon is voorbij");
        }

        [Test]
        public void TheBarriersAlsoWorkWhenTheTrainRunsTheOtherWay()
        {
            AddStations();
            var crossing = _rail.AddCrossing(_t2, 100f, 1);
            _rail.AddTrain(TrainKind.Passenger, _n3, _n1, 3);

            _rail.Advance(10f);
            Assert.That(_rail.GetCrossing(crossing).IsClosed, Is.False, "nog 1460 m");
            _rail.Advance(4f);
            Assert.That(_rail.GetCrossing(crossing).IsClosed, Is.True, "1284 m is binnen de waarschuwingstijd");
        }

        [Test]
        public void RemovingATrainOpensTheBarriers()
        {
            AddStations();
            var crossing = _rail.AddCrossing(_t1, 1000f, 1);
            var train = _rail.AddTrain(TrainKind.Passenger, _n1, _n3, 3);
            _rail.UpdateCrossings();
            Assert.That(_rail.GetCrossing(crossing).IsClosed, Is.True);

            Assert.That(_rail.RemoveTrain(train), Is.True);
            Assert.That(_rail.GetCrossing(crossing).IsClosed, Is.False);
            Assert.That(_rail.RemoveTrain(train), Is.False);
        }

        [Test]
        public void ARideTimeFollowsTheRoute()
        {
            AddStations();

            Assert.That(_rail.RideTime(TrainKind.Passenger, _n1, _n3), Is.EqualTo(Train.TravelTime(TrainKind.Passenger, 4000f)).Within(1e-2f));
            Assert.That(_rail.RideTime(TrainKind.Passenger, _n3, _n4), Is.EqualTo(-1f), "geen route");
        }

        [Test]
        public void TheSectionRoundTripsTheWholeRailNetwork()
        {
            AddStations();
            var goods = AddGoods();
            _rail.AddLine("IC1", new[] { _rail.GetStation(_n1).Stop, _rail.GetStation(_n3).Stop }, false, 2, 600f);
            _rail.AddCrossing(_t1, 1000f, 5);
            _rail.AddTrain(TrainKind.Passenger, _n1, _n3, 3);
            _rail.AddTrain(TrainKind.Freight, goods, _n4, 1);
            _rail.Advance(15f);
            var section = new RailSection(_rail);
            var bytes = SaveBytes.Write(new SaveMetadata(), section);

            var restored = new RailNetwork(new TransitNetwork());
            restored.AddNode(new float2(5f, 5f));
            SaveBytes.Read(bytes, new RailSection(restored));

            Assert.That(restored.Nodes.Count(), Is.EqualTo(5));
            Assert.That(restored.Tracks.Count(), Is.EqualTo(4));
            Assert.That(restored.SelectedTrack(_n2), Is.EqualTo(_rail.SelectedTrack(_n2)));
            Assert.That(restored.Trains.Count(), Is.EqualTo(2));
            Assert.That(restored.Crossings.Single().IsClosed, Is.EqualTo(_rail.Crossings.Single().IsClosed));
            Assert.That(restored.NextTrainId, Is.EqualTo(_rail.NextTrainId));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new RailSection(restored)), Is.EqualTo(bytes));
            Assert.That(section.Id, Is.EqualTo(RailSection.SectionId));
            Assert.That(RailSection.SectionId, Is.EqualTo("RAIL"));
            Assert.That(section.Version, Is.EqualTo(1));
            Assert.Throws<ArgumentNullException>(() => new RailSection(null));
        }

        [Test]
        public void TheSectionRejectsImpossibleData()
        {
            var section = new RailSection(_rail);
            var before = _rail.Tracks.Count();

            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => { Header(w); w.Write(-1); }), 1), "negatief aantal");
            Assert.Throws<SaveFormatException>(
                () => section.Read(SaveBytes.ReaderFor(w => { Header(w); w.Write(0); w.Write(1); w.Write(1); w.Write(1); w.Write(2); w.Write(100f); Tail(w); }), 1),
                "spoor tussen onbekende knopen");
            Assert.Throws<SaveFormatException>(
                () => section.Read(SaveBytes.ReaderFor(w => { Header(w); w.Write(0); w.Write(0); w.Write(1); w.Write(1); w.Write((byte)9); w.Write(-1); w.Write(100f); }), 1),
                "onbekende soort trein");
            Assert.Throws<SaveFormatException>(
                () => section.Read(SaveBytes.ReaderFor(w => { Header(w); w.Write(0); w.Write(1); w.Write(1); w.Write(1); w.Write(2); w.Write(5f); }), 1),
                "spoor te kort");
            Assert.That(_rail.Tracks.Count(), Is.EqualTo(before));
        }

        [Test]
        public void ARestoreChecksTheSnapshot()
        {
            var node = new KeyValuePair<int, float2>(1, float2.zero);
            var other = new KeyValuePair<int, float2>(2, new float2(1000f, 0f));
            var track = new RailTrack(1, 1, 2, 1000f);

            Assert.Throws<ArgumentNullException>(() => RailNetwork.Check(null));
            Assert.Throws<ArgumentException>(() => RailNetwork.Check(new RailSnapshot { Nodes = new[] { node, node }, NextNode = 5 }), "dubbele knoop");
            Assert.Throws<ArgumentException>(() => RailNetwork.Check(new RailSnapshot { Nodes = new[] { node }, NextNode = 1 }), "nummer te hoog");
            Assert.Throws<ArgumentException>(() => RailNetwork.Check(new RailSnapshot { Nodes = new[] { node, other }, Tracks = new[] { track, track }, NextNode = 3, NextTrack = 5 }), "dubbel spoor");
            Assert.Throws<ArgumentException>(
                () => RailNetwork.Check(new RailSnapshot { Nodes = new[] { node, other }, Tracks = new[] { track, new RailTrack(2, 2, 1, 1000f) }, NextNode = 3, NextTrack = 5 }),
                "twee sporen tussen dezelfde knopen");
            Assert.Throws<ArgumentException>(
                () => RailNetwork.Check(new RailSnapshot { Nodes = new[] { node, other }, Tracks = new[] { track }, Switches = new[] { new KeyValuePair<int, int>(1, 1) }, NextNode = 3, NextTrack = 5 }),
                "wissel zonder drie sporen");
            Assert.Throws<ArgumentException>(
                () => RailNetwork.Check(new RailSnapshot { Nodes = new[] { node, other }, Tracks = new[] { track }, Stations = new[] { new RailStation(9, TrainKind.Freight, -1, 100f) }, NextNode = 3, NextTrack = 5 }),
                "station op onbekende knoop");
            Assert.Throws<ArgumentException>(
                () => RailNetwork.Check(new RailSnapshot { Nodes = new[] { node, other }, Tracks = new[] { track }, Crossings = new[] { new LevelCrossing(1, 1, 2f, 1) }, NextNode = 3, NextTrack = 5, NextCrossing = 5 }),
                "overweg te dicht bij de knoop");
            Assert.Throws<ArgumentException>(
                () => RailNetwork.Check(new RailSnapshot { Nodes = new[] { node, other }, Tracks = new[] { track }, Trains = new[] { new TrainRun(1, TrainKind.Freight, 2, new[] { 1, 9 }, 0f) }, NextNode = 3, NextTrack = 5, NextTrain = 5 }),
                "trein over spoor dat niet bestaat");
            Assert.Throws<ArgumentException>(() => new RailTrack(1, 1, 1, 100f));
            Assert.Throws<ArgumentException>(() => new RailStation(1, TrainKind.Passenger, -1, 100f));
            Assert.Throws<ArgumentException>(() => new TrainRun(1, TrainKind.Freight, 2, new[] { 1 }, 0f));
            Assert.DoesNotThrow(() => RailNetwork.Check(new RailSnapshot { Nodes = new[] { node, other }, Tracks = new[] { track }, NextNode = 3, NextTrack = 2 }));
        }

        private static void Tail(SaveWriter writer)
        {
            for (var i = 0; i < 4; i++)
            {
                writer.Write(0);
            }
        }

        private static void Header(SaveWriter writer)
        {
            writer.Write(10);
            writer.Write(10);
            writer.Write(10);
            writer.Write(10);
        }
    }
}
