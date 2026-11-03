using System;
using Cities.Simulation.Animation;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Animation
{
    public class AgentAnimationTrackerTests
    {
        private static readonly float2 East = new float2(1f, 0f);

        [Test]
        public void NewVehicleStartsWithoutSpinAndRollsWithDistance()
        {
            var tracker = new AgentAnimationTracker(AgentAnimationKind.Wheel, 0.3f);
            tracker.Begin();
            Assert.That(tracker.Update(1, float2.zero, East), Is.Zero);
            tracker.Begin();
            var angle = tracker.Update(1, new float2(0.3f, 0f), East);
            Assert.That(angle, Is.EqualTo(1f).Within(1e-5f), "0,3 m bij straal 0,3 m is één radiaal");
        }

        [Test]
        public void ReversingSpinsTheWheelBack()
        {
            var tracker = new AgentAnimationTracker(AgentAnimationKind.Wheel, 0.5f);
            tracker.Begin();
            tracker.Update(1, new float2(10f, 0f), East);
            tracker.Begin();
            var angle = tracker.Update(1, new float2(9f, 0f), East);
            Assert.That(angle, Is.EqualTo(WheelSpin.Wrap(-2f)).Within(1e-4f));
        }

        [Test]
        public void StandingStillKeepsTheValue()
        {
            var tracker = new AgentAnimationTracker(AgentAnimationKind.Wheel, 0.3f);
            tracker.Begin();
            tracker.Update(1, float2.zero, East);
            tracker.Begin();
            var moved = tracker.Update(1, new float2(1f, 0f), East);
            tracker.Begin();
            Assert.That(tracker.Update(1, new float2(1f, 0f), East), Is.EqualTo(moved));
            tracker.Begin();
            Assert.That(tracker.Update(1, new float2(1.5f, 0f), float2.zero), Is.Not.EqualTo(moved), "zonder richting telt het als vooruit");
        }

        [Test]
        public void NewWalkerGetsItsOwnPhaseAndWalksForwardOnly()
        {
            var tracker = new AgentAnimationTracker(AgentAnimationKind.Walk, 1f);
            tracker.Begin();
            var start = tracker.Update(7, float2.zero, East);
            Assert.That(start, Is.EqualTo(AnimationPhase.Offset(7)));
            tracker.Begin();
            var forward = tracker.Update(7, new float2(WalkCycle.StrideMeters * 0.1f, 0f), East);
            Assert.That(forward, Is.EqualTo(start + 0.1f).Within(1e-4f).Or.EqualTo(start + 0.1f - 1f).Within(1e-4f));
            tracker.Begin();
            var back = tracker.Update(7, float2.zero, East);
            Assert.That(back, Is.EqualTo(forward + 0.1f).Within(1e-4f).Or.EqualTo(forward + 0.1f - 1f).Within(1e-4f), "achteruit lopen telt ook als stappen");
        }

        [Test]
        public void AgentsNotSeenAnymoreAreForgotten()
        {
            var tracker = new AgentAnimationTracker(AgentAnimationKind.Walk, 1f);
            tracker.Begin();
            tracker.Update(1, float2.zero, East);
            tracker.Update(2, float2.zero, East);
            Assert.That(tracker.End(), Is.Zero);
            Assert.That(tracker.Count, Is.EqualTo(2));
            tracker.Begin();
            tracker.Update(1, float2.zero, East);
            Assert.That(tracker.End(), Is.EqualTo(1));
            Assert.That(tracker.Count, Is.EqualTo(1));
        }

        [Test]
        public void RejectsNonPositiveSize()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AgentAnimationTracker(AgentAnimationKind.Wheel, 0f));
        }
    }
}
