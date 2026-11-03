using Cities.Gameplay.Persistence;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Persistence
{
    public class AutosaveTimerTests
    {
        [Test]
        public void IsOffByDefaultAndWhenZero()
        {
            var timer = new AutosaveTimer();

            Assert.That(timer.Enabled, Is.False);
            Assert.That(timer.Advance(10_000), Is.False);
            Assert.That(timer.SecondsUntilDue, Is.EqualTo(double.PositiveInfinity));
            timer.IntervalSeconds = 120;
            timer.IntervalSeconds = 0;
            Assert.That(timer.Enabled, Is.False);
        }

        [Test]
        public void FiresOnceThePlayTimeReachesTheInterval()
        {
            var timer = new AutosaveTimer { IntervalSeconds = 60 };

            Assert.That(timer.Advance(59), Is.False);
            Assert.That(timer.SecondsUntilDue, Is.EqualTo(1).Within(1e-9));
            Assert.That(timer.Advance(1), Is.True);
            Assert.That(timer.Advance(30), Is.False, "Daarna begint de telling opnieuw");
            Assert.That(timer.Advance(30), Is.True);
        }

        [Test]
        public void ShortIntervalsAreRaisedAndBadValuesTurnItOff()
        {
            var timer = new AutosaveTimer { IntervalSeconds = 5 };
            Assert.That(timer.IntervalSeconds, Is.EqualTo(AutosaveTimer.MinimumIntervalSeconds));

            timer.IntervalSeconds = double.NaN;
            Assert.That(timer.Enabled, Is.False);
            timer.IntervalSeconds = double.PositiveInfinity;
            Assert.That(timer.Enabled, Is.False);
            timer.IntervalSeconds = -3;
            Assert.That(timer.Enabled, Is.False);
        }

        [Test]
        public void IgnoresInvalidStepsAndResets()
        {
            var timer = new AutosaveTimer { IntervalSeconds = 60 };

            Assert.That(timer.Advance(-5), Is.False);
            Assert.That(timer.Advance(double.NaN), Is.False);
            Assert.That(timer.Advance(double.PositiveInfinity), Is.False);
            timer.Advance(50);
            timer.Reset();
            Assert.That(timer.SecondsUntilDue, Is.EqualTo(60));
            Assert.That(timer.Advance(20), Is.False);
        }
    }
}
