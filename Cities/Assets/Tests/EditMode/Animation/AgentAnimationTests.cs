using System;
using Cities.Simulation.Animation;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Animation
{
    public class AgentAnimationTests
    {
        [Test]
        public void WheelAngleFollowsDistanceOverRadius()
        {
            var circumference = 2f * math.PI * 0.3f;
            Assert.That(WheelSpin.Advance(0f, circumference * 0.25f, 0.3f), Is.EqualTo(math.PI * 0.5f).Within(1e-4f));
            Assert.That(WheelSpin.Advance(0f, 0f, 0.3f), Is.Zero, "stilstaand draait niet");
        }

        [Test]
        public void WheelAngleWrapsForwardAndBackward()
        {
            var forward = WheelSpin.Advance(6f, 1f, 0.5f);
            Assert.That(forward, Is.InRange(0f, WheelSpin.FullTurn));
            var backward = WheelSpin.Advance(0.1f, -1f, 0.5f);
            Assert.That(backward, Is.InRange(0f, WheelSpin.FullTurn));
            Assert.That(WheelSpin.Wrap(WheelSpin.FullTurn), Is.Zero);
            Assert.That(WheelSpin.Wrap(-0.5f), Is.EqualTo(WheelSpin.FullTurn - 0.5f).Within(1e-5f));
        }

        [Test]
        public void RevolutionsMatchSpeed()
        {
            Assert.That(WheelSpin.RevolutionsPerSecond(2f * math.PI * 0.3f, 0.3f), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void WheelRejectsNonPositiveRadius()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => WheelSpin.Advance(0f, 1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => WheelSpin.RevolutionsPerSecond(1f, -1f));
        }

        [Test]
        public void WalkPhaseAdvancesWithDistanceAndWraps()
        {
            Assert.That(WalkCycle.Advance(0f, WalkCycle.StrideMeters * 0.25f), Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(WalkCycle.Advance(0.9f, WalkCycle.StrideMeters * 0.2f), Is.EqualTo(0.1f).Within(1e-5f));
            Assert.That(WalkCycle.Advance(0.4f, 0f), Is.EqualTo(0.4f), "stilstaan houdt de fase vast");
            Assert.That(WalkCycle.Advance(0f, 0.7f, 0.5f), Is.EqualTo(1f).Within(1e-5f).Or.Zero.Within(1e-5f), "een kind heeft kortere stappen");
            Assert.Throws<ArgumentOutOfRangeException>(() => WalkCycle.Advance(0f, 1f, 0f));
        }

        [Test]
        public void LegsAndArmsSwingOppositely()
        {
            Assert.That(WalkCycle.LegSwing(0f), Is.Zero.Within(1e-6f));
            Assert.That(WalkCycle.LegSwing(0.25f), Is.EqualTo(WalkCycle.MaxSwing).Within(1e-5f));
            Assert.That(WalkCycle.LegSwing(0.75f), Is.EqualTo(-WalkCycle.MaxSwing).Within(1e-5f));
            Assert.That(WalkCycle.ArmSwing(0.25f), Is.LessThan(0f));
            Assert.That(math.abs(WalkCycle.ArmSwing(0.25f)), Is.LessThan(WalkCycle.MaxSwing));
            Assert.That(WalkCycle.LegSwing(0.3f), Is.EqualTo(-WalkCycle.LegSwing(0.8f)).Within(1e-5f), "het andere been loopt een halve cyclus achter");
        }

        [Test]
        public void BodyBobIsLowestWithBothFeetDown()
        {
            Assert.That(WalkCycle.BodyBob(0f), Is.Zero.Within(1e-6f));
            Assert.That(WalkCycle.BodyBob(0.25f), Is.LessThan(0f));
            Assert.That(WalkCycle.BodyBob(0.25f), Is.GreaterThanOrEqualTo(-0.02f - 1e-6f));
        }

        [Test]
        public void LoopBlendsBetweenNeighbouringFramesAndWraps()
        {
            var middle = ClipSampler.Loop(0.375f, 8);
            Assert.That(middle.From, Is.EqualTo(3));
            Assert.That(middle.To, Is.EqualTo(4));
            Assert.That(middle.Weight, Is.EqualTo(0f).Within(1e-5f));
            var half = ClipSampler.Loop(0.4375f, 8);
            Assert.That(half.Weight, Is.EqualTo(0.5f).Within(1e-4f));
            var last = ClipSampler.Loop(0.95f, 8);
            Assert.That(last.From, Is.EqualTo(7));
            Assert.That(last.To, Is.EqualTo(0), "na het laatste frame volgt het eerste");
            Assert.That(ClipSampler.Loop(1.5f, 8).From, Is.EqualTo(4), "fase buiten 0..1 wordt teruggebracht");
            Assert.That(ClipSampler.Loop(-0.25f, 8).From, Is.EqualTo(6));
        }

        [Test]
        public void OnceStopsOnTheLastFrame()
        {
            var end = ClipSampler.Once(1.5f, 5);
            Assert.That(end, Is.EqualTo(new FrameBlend(4, 4, 0f)));
            var start = ClipSampler.Once(-1f, 5);
            Assert.That(start.From, Is.Zero);
            var mid = ClipSampler.Once(0.5f, 5);
            Assert.That(mid.From, Is.EqualTo(2));
            Assert.That(mid.To, Is.EqualTo(3));
            Assert.That(ClipSampler.Once(0.99f, 1), Is.EqualTo(new FrameBlend(0, 0, 0f)));
        }

        [Test]
        public void SamplerRejectsEmptyClips()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ClipSampler.Loop(0f, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ClipSampler.Once(0f, 0));
        }

        [Test]
        public void FrameBlendEqualityUsesAllFields()
        {
            var a = new FrameBlend(1, 2, 0.5f);
            Assert.That(a.Equals(new FrameBlend(1, 2, 0.5f)), Is.True);
            Assert.That(a.Equals((object)new FrameBlend(1, 3, 0.5f)), Is.False);
            Assert.That(a.Equals("x"), Is.False);
            Assert.That(a.GetHashCode(), Is.EqualTo(new FrameBlend(1, 2, 0.5f).GetHashCode()));
        }

        [Test]
        public void LayoutIndexesRowByRowAndCountsMemory()
        {
            var layout = new BakedClipLayout(100, 16);
            Assert.That(layout.TexelIndex(0, 0), Is.Zero);
            Assert.That(layout.TexelIndex(5, 2), Is.EqualTo(205));
            Assert.That(layout.Bytes, Is.EqualTo(100L * 16 * 8));
            Assert.Throws<ArgumentOutOfRangeException>(() => layout.TexelIndex(100, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => layout.TexelIndex(0, 16));
            Assert.Throws<ArgumentOutOfRangeException>(() => layout.TexelIndex(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => layout.TexelIndex(0, -1));
        }

        [Test]
        public void LayoutRejectsSizesBeyondTheTextureLimit()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BakedClipLayout(0, 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BakedClipLayout(BakedClipLayout.MaxTextureSize + 1, 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BakedClipLayout(10, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BakedClipLayout(10, BakedClipLayout.MaxTextureSize + 1));
            Assert.That(BakedClipLayout.FramesFor(1.2f, 20f), Is.EqualTo(24));
            Assert.That(BakedClipLayout.FramesFor(0f, 20f), Is.EqualTo(1));
        }

        [Test]
        public void PhaseOffsetIsStableAndSpreadsOver01()
        {
            Assert.That(AnimationPhase.Offset(42), Is.EqualTo(AnimationPhase.Offset(42)));
            var sum = 0f;
            for (uint id = 0; id < 1000; id++)
            {
                var offset = AnimationPhase.Offset(id);
                Assert.That(offset, Is.InRange(0f, 1f).And.LessThan(1f));
                sum += offset;
            }

            Assert.That(sum / 1000f, Is.EqualTo(0.5f).Within(0.05f));
            Assert.That(AnimationPhase.Offset(1), Is.Not.EqualTo(AnimationPhase.Offset(2)));
        }

        [Test]
        public void LimbRotatesAroundItsHinge()
        {
            var pivot = new float2(1f, 0f);
            var hanging = new float3(0.2f, 0f, 0f);
            Assert.That(LimbPose.RotateAroundX(hanging, pivot, 0f), Is.EqualTo(hanging));
            var turned = LimbPose.RotateAroundX(hanging, pivot, math.PI * 0.5f);
            Assert.That(turned.x, Is.EqualTo(0.2f), "de X-as blijft staan");
            Assert.That(turned.y, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(turned.z, Is.EqualTo(-1f).Within(1e-5f));
            Assert.That(LimbPose.RotateAroundX(new float3(0f, 1f, 0f), pivot, 1.3f).y, Is.EqualTo(1f).Within(1e-5f), "het scharnier zelf beweegt niet");
        }
    }
}
