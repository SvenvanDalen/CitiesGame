using System;
using Cities.Core.Persistence;
using Cities.Simulation.Network;
using Cities.Simulation.Transit;
using Cities.Simulation.Vehicles;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Transit
{
    public class TransitPriorityTests
    {
        private static readonly int[] Crossing = { 1, 2 };

        private static Approach Vehicle(int id, RightOfWay rule, bool circulating = false) =>
            new Approach(id, rule, Crossing, 180f, true, 0f, circulating);

        [Test]
        public void ABusLaneReplacesTheOuterLaneOfEachDirection()
        {
            var lanes = LaneProfiles.Road(2, bikeLanes: false, sidewalks: false, busLanes: true);

            var bus = lanes.FindAll(TransitPriority.IsBusLane);
            Assert.That(bus.Count, Is.EqualTo(2));
            Assert.That(bus.TrueForAll(l => Math.Abs(l.Offset) > LaneProfiles.CarLaneWidth), Is.True, "buitenste strook");
            Assert.That(lanes.FindAll(l => l.Types == LaneTypes.Vehicles).Count, Is.EqualTo(2));
            Assert.That(TransitPriority.MayUse(bus[0], LaneTypes.Bus), Is.True);
            Assert.That(TransitPriority.MayUse(bus[0], LaneTypes.Car), Is.False);
            Assert.That(TransitPriority.MayUse(lanes[0], LaneTypes.Car), Is.True);
        }

        [Test]
        public void ABusLaneNeedsRoomNextToTheCarLanes()
        {
            Assert.Throws<ArgumentException>(() => LaneProfiles.Road(1, false, false, busLanes: true));
            Assert.Throws<ArgumentException>(() => LaneProfiles.Road(0, false, false, oneWay: true, busLanes: true));

            var oneWay = LaneProfiles.Road(2, false, false, oneWay: true, busLanes: true);
            Assert.That(oneWay.FindAll(TransitPriority.IsBusLane).Count, Is.EqualTo(1));
            Assert.That(LaneProfiles.Road(1, false, false, oneWay: true, busLanes: true).TrueForAll(TransitPriority.IsBusLane), Is.True, "busstraat");
            Assert.That(LaneProfiles.Road(2, false, false).FindAll(TransitPriority.IsBusLane), Is.Empty);
        }

        [Test]
        public void ABusWithPriorityDoesNotYieldAtAStopSign()
        {
            var bus = Vehicle(1, RightOfWay.Yield).WithTransitPriority();
            var car = Vehicle(2, RightOfWay.Priority);

            Assert.That(IntersectionRules.MayProceed(Vehicle(1, RightOfWay.Yield), new[] { car }), Is.False);
            Assert.That(IntersectionRules.MayProceed(bus, new[] { car }), Is.True);
            Assert.That(bus.TransitPriority, Is.True);
            Assert.That(bus.WithTransitPriority(false).TransitPriority, Is.False);
        }

        [Test]
        public void TrafficYieldsToABusWithPriorityButNotTheOtherWayAroundInARoundabout()
        {
            var bus = Vehicle(1, RightOfWay.Yield).WithTransitPriority();

            Assert.That(IntersectionRules.MayProceed(Vehicle(2, RightOfWay.Yield), new[] { bus }), Is.False);
            Assert.That(IntersectionRules.MayProceed(bus, new[] { Vehicle(3, RightOfWay.Roundabout, circulating: true) }), Is.False, "rotonde gaat voor");
        }

        [Test]
        public void TwoBusesWithPriorityFallBackToTheNormalRule()
        {
            var a = Vehicle(1, RightOfWay.Stop).WithTransitPriority();
            var b = Vehicle(2, RightOfWay.Stop).WithTransitPriority();

            Assert.That(IntersectionRules.MayProceed(a, new[] { b }), Is.False);
        }

        [Test]
        public void PriorityOnlyAppliesToEnabledLinesAtEnabledIntersections()
        {
            var priority = new TransitPriority();
            priority.SetLine(7, true);
            priority.SetIntersection(3, true);
            var approach = Vehicle(1, RightOfWay.Yield);

            Assert.That(priority.Mark(approach, 7, 3).TransitPriority, Is.True);
            Assert.That(priority.Mark(approach, 8, 3).TransitPriority, Is.False, "andere lijn");
            Assert.That(priority.Mark(approach, 7, 4).TransitPriority, Is.False, "ander kruispunt");
            Assert.That(priority.HasPriority(7), Is.True);
            Assert.That(priority.IsPriorityIntersection(3), Is.True);

            priority.SetLine(7, false);
            priority.SetIntersection(3, false);
            Assert.That(priority.Applies(7, 3), Is.False);
            Assert.That(priority.Lines, Is.Empty);
        }

        [Test]
        public void ARequestExtendsGreenThatIsAboutToEnd()
        {
            var plan = new SignalPlan(2, 3f);
            var priority = Enabled(plan, out var line, out var junction);
            plan.Tick(plan.GreenOf(0) - 2f);

            Assert.That(priority.RequestSignal(plan, line, junction, 0), Is.True);
            plan.Tick(5f);
            Assert.That(plan.IsOpen(0), Is.True, "groen is verlengd");
            Assert.That(plan.CurrentGreen, Is.EqualTo(SignalPlan.DefaultGreen + SignalPlan.TransitExtension));
            Assert.That(priority.SignalGrants, Is.EqualTo(1));
        }

        [Test]
        public void ARequestDoesNotExtendGreenThatHasPlentyLeft()
        {
            var plan = new SignalPlan(2, 3f);
            var priority = Enabled(plan, out var line, out var junction);
            plan.Tick(1f);

            Assert.That(priority.RequestSignal(plan, line, junction, 0), Is.False);
            Assert.That(priority.SignalDenied, Is.EqualTo(1));
        }

        [Test]
        public void ARequestCutsTheGreenOfTheCurrentPhaseForTheNextGroup()
        {
            var plan = new SignalPlan(2, 3f);
            var priority = Enabled(plan, out var line, out var junction);
            plan.Tick(2f);

            Assert.That(priority.RequestSignal(plan, line, junction, 1), Is.True);
            Assert.That(plan.CurrentGreen, Is.EqualTo(SignalPlan.MinGreen));
            plan.Tick(SignalPlan.MinGreen - 2f);
            Assert.That(plan.Clearing, Is.True);
            plan.Tick(3f);
            Assert.That(plan.IsOpen(1), Is.True);
        }

        [Test]
        public void TheGreenIsNeverCutBelowTheMinimumOrWhenTheGroupIsNotNext()
        {
            var plan = new SignalPlan(3, 3f);
            plan.Tick(15f);

            Assert.That(plan.TryTransitPriority(2), Is.False, "niet de volgende groep");
            Assert.That(plan.TryTransitPriority(1), Is.True);
            Assert.That(plan.CurrentGreen, Is.EqualTo(15f), "niet korter dan al verstreken");
        }

        [Test]
        public void ARequestDuringClearanceOrWhileOffIsRefused()
        {
            var plan = new SignalPlan(2, 3f);
            plan.Tick(21f);
            Assert.That(plan.Clearing, Is.True);
            Assert.That(plan.TryTransitPriority(1), Is.False);
            Assert.That(plan.TryTransitPriority(0), Is.False);

            plan.Enabled = false;
            Assert.That(plan.TryTransitPriority(0), Is.False);
        }

        [Test]
        public void OnlyOneInterventionPerRoundKeepsOtherDirectionsFair()
        {
            var plan = new SignalPlan(2, 3f);
            plan.Tick(2f);
            Assert.That(plan.TryTransitPriority(1), Is.True);
            Assert.That(plan.TryTransitPriority(1), Is.False, "direct opnieuw");

            plan.Tick(SignalPlan.MinGreen + 3f);
            Assert.That(plan.Phase, Is.EqualTo(1));
            Assert.That(plan.TryTransitPriority(0), Is.False, "nog geen volledige ronde");

            plan.Tick(plan.GreenOf(1) + 3f);
            Assert.That(plan.Phase, Is.EqualTo(0));
            plan.Tick(plan.GreenOf(0) - 4f);
            Assert.That(plan.TryTransitPriority(0), Is.True, "nieuwe ronde");
        }

        [Test]
        public void TimingStaysConsistentAfterACut()
        {
            var plan = new SignalPlan(2, 3f);
            plan.Tick(2f);
            plan.TryTransitPriority(1);

            Assert.That(plan.SecondsUntilOpen(1), Is.EqualTo(SignalPlan.MinGreen + 3f - 2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => plan.TryTransitPriority(5));
        }

        [Test]
        public void AnInactiveLineOrNullPlanIsHandled()
        {
            var priority = new TransitPriority();
            var plan = new SignalPlan(1);

            Assert.That(priority.RequestSignal(plan, 1, 1, 0), Is.False);
            Assert.Throws<ArgumentNullException>(() => priority.RequestSignal(null, 1, 1, 0));
        }

        [Test]
        public void TheSectionRoundTrips()
        {
            var priority = new TransitPriority();
            priority.SetLine(9, true);
            priority.SetLine(2, true);
            priority.SetIntersection(40, true);
            var section = new TransitPrioritySection(priority);
            var bytes = SaveBytes.Write(new SaveMetadata(), section);

            var restored = new TransitPriority();
            restored.SetLine(77, true);
            SaveBytes.Read(bytes, new TransitPrioritySection(restored));

            Assert.That(restored.Lines, Is.EqualTo(new[] { 2, 9 }));
            Assert.That(restored.Intersections, Is.EqualTo(new[] { 40 }));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new TransitPrioritySection(restored)), Is.EqualTo(bytes));
            Assert.That(section.Id, Is.EqualTo("TPRI"));
            Assert.That(section.Version, Is.EqualTo(1));
            Assert.Throws<ArgumentNullException>(() => new TransitPrioritySection(null));
        }

        [Test]
        public void TheSectionRejectsImpossibleData()
        {
            var priority = new TransitPriority();
            priority.SetLine(1, true);
            var section = new TransitPrioritySection(priority);

            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => w.Write(-1)), 1), "negatief aantal");
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => { w.Write(2); w.Write(5); w.Write(5); w.Write(0); }), 1), "dubbel");
            Assert.That(priority.Lines, Is.EqualTo(new[] { 1 }));
        }

        private static TransitPriority Enabled(SignalPlan plan, out int line, out int junction)
        {
            Assert.That(plan.Groups, Is.GreaterThan(0));
            line = 1;
            junction = 1;
            var priority = new TransitPriority();
            priority.SetLine(line, true);
            priority.SetIntersection(junction, true);
            return priority;
        }
    }
}
