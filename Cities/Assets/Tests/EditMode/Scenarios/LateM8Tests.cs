using System;
using System.Collections.Generic;
using Cities.Gameplay.Replay;
using Cities.Gameplay.Scenarios;
using Cities.Gameplay.UI;
using Cities.Simulation.Services;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Scenarios
{
    public class LateM8Tests
    {
        [Test]
        public void HelicopterOnlyHelpsOnLongDistancesAndInGoodWeather()
        {
            var heli = new ServiceHelicopter(ServiceKind.Health, float2.zero);

            Assert.That(heli.ArrivalSeconds(new float2(1000f, 0f)), Is.NaN);
            Assert.That(heli.ArrivalSeconds(new float2(6000f, 0f)), Is.EqualTo(145f).Within(0.01f));
            heli.Grounded = true;
            Assert.That(heli.ArrivalSeconds(new float2(6000f, 0f)), Is.NaN);
            Assert.Throws<ArgumentException>(() => new ServiceHelicopter(ServiceKind.Death, float2.zero));
        }

        [Test]
        public void ScenarioIsWonWhenAllGoalsAreMetAndLostOnBrokenLimitOrDeadline()
        {
            var scenario = new Scenario(
                "Eerste stad",
                new[]
                {
                    new ScenarioGoal("10.000 inwoners", "population", 10_000),
                    new ScenarioGoal("Geen schuld", "debt", 0, atLeast: false, mustHold: true),
                },
                365);
            var metrics = new Dictionary<string, double> { { "population", 500 }, { "debt", 0 } };

            Assert.That(scenario.Evaluate(10, metrics), Is.EqualTo(GoalState.Open));
            Assert.That(scenario.Evaluate(400, metrics), Is.EqualTo(GoalState.Failed));
            metrics["population"] = 12_000;
            Assert.That(scenario.Evaluate(100, metrics), Is.EqualTo(GoalState.Reached));
            metrics["debt"] = 5;
            Assert.That(scenario.Evaluate(100, metrics), Is.EqualTo(GoalState.Failed));
            Assert.Throws<ArgumentNullException>(() => scenario.Evaluate(1, null));
            Assert.Throws<ArgumentException>(() => new Scenario("x", Array.Empty<ScenarioGoal>(), 10));
            Assert.Throws<ArgumentException>(() => new ScenarioGoal("", "m", 1));
        }

        [Test]
        public void HintAppearsOnceAndCanBeAskedAgain()
        {
            var hints = new ToolHints();
            hints.Register("road", "Sleep om een weg te leggen.");

            Assert.That(hints.OnToolChosen("road"), Is.EqualTo("Sleep om een weg te leggen."));
            Assert.That(hints.OnToolChosen("road"), Is.Null);
            Assert.That(hints.Explain("road"), Is.Not.Null);
            hints.ResetSeen();
            hints.Enabled = false;
            Assert.That(hints.OnToolChosen("road"), Is.Null);
            Assert.That(hints.OnToolChosen(null), Is.Null);
            Assert.That(hints.Explain("onbekend"), Is.Null);
            Assert.Throws<ArgumentException>(() => hints.Register("", "x"));
        }

        [Test]
        public void TimelapseKeepsAnEvenSampleAndStaysBounded()
        {
            var lapse = new GrowthTimelapse();

            Assert.Throws<InvalidOperationException>(() => lapse.At(0f));
            Assert.That(lapse.Record(3, 1, 1), Is.False);
            for (var day = 0; day < 7 * 1000; day += 7)
            {
                lapse.Record(day, day, day / 10);
            }

            Assert.That(lapse.Frames.Count, Is.LessThanOrEqualTo(GrowthTimelapse.MaxFrames));
            Assert.That(lapse.IntervalDays, Is.GreaterThan(7));
            Assert.That(lapse.At(0f).Day, Is.EqualTo(0));
            Assert.That(lapse.At(1f).Day, Is.GreaterThan(lapse.At(0.5f).Day));
            Assert.That(lapse.Record(0, 1, 1), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => lapse.Record(-1, 0, 0));
        }
    }
}
