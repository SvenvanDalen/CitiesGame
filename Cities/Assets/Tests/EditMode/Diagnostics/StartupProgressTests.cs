using System;
using Cities.Core.Diagnostics;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Diagnostics
{
    public class StartupProgressTests
    {
        [Test]
        public void FractionRisesWithEveryCompletedStep()
        {
            var progress = new StartupProgress(new[] { "A", "B", "C", "D" });
            Assert.That(progress.Fraction, Is.EqualTo(0f));
            Assert.That(progress.Current, Is.EqualTo("A"));

            progress.Complete("A");
            progress.Complete("C");

            Assert.That(progress.Fraction, Is.EqualTo(0.5f));
            Assert.That(progress.Current, Is.EqualTo("B"));
            Assert.That(progress.IsComplete, Is.False);
        }

        [Test]
        public void CompletingTwiceCountsOnce()
        {
            var progress = new StartupProgress(new[] { "A", "B" });
            progress.Complete("A");
            progress.Complete("A");
            Assert.That(progress.Fraction, Is.EqualTo(0.5f));
        }

        [Test]
        public void AllStepsDoneIsComplete()
        {
            var progress = StartupProgress.ForMainScene();
            foreach (var step in progress.Steps)
            {
                progress.Complete(step);
            }

            Assert.That(progress.IsComplete, Is.True);
            Assert.That(progress.Current, Is.Null);
        }

        [Test]
        public void UnknownStepAndEmptyListAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new StartupProgress(new[] { "A" }).Complete("X"));
            Assert.Throws<ArgumentException>(() => new StartupProgress(new string[0]));
        }

        [Test]
        public void ReportWithoutSharedProgressDoesNothing()
        {
            StartupProgress.Shared = null;
            Assert.DoesNotThrow(() => StartupProgress.Report("A"));
        }

        [Test]
        public void ReportCompletesOnTheSharedProgress()
        {
            var progress = new StartupProgress(new[] { "A", "B" });
            StartupProgress.Shared = progress;
            try
            {
                StartupProgress.Report("A");
                Assert.That(progress.Fraction, Is.EqualTo(0.5f));
            }
            finally
            {
                StartupProgress.Shared = null;
            }
        }

        [Test]
        public void StepsAreTimedAndSummed()
        {
            var time = 0.0;
            var progress = new StartupProgress(new[] { "A", "B" }, () => time);
            time = 1.5;
            progress.Complete("A");
            time = 4.0;
            progress.Complete("B");

            Assert.That(progress.Timings[0].Seconds, Is.EqualTo(1.5).Within(1e-9));
            Assert.That(progress.Timings[1].Seconds, Is.EqualTo(2.5).Within(1e-9));
            Assert.That(progress.TotalSeconds, Is.EqualTo(4.0).Within(1e-9));
            Assert.That(progress.OverBudget, Is.False);
            Assert.That(progress.Summary(), Does.Contain("4,0 s").Or.Contain("4.0 s"));
        }

        [Test]
        public void SlowStartIsFlaggedOverBudget()
        {
            var time = 0.0;
            var progress = new StartupProgress(new[] { "A" }, () => time);
            time = StartupProgress.BudgetSeconds + 1;
            progress.Complete("A");

            Assert.That(progress.OverBudget, Is.True);
            Assert.That(progress.Summary(), Does.Contain("budget"));
        }

        [Test]
        public void CompletingTwiceDoesNotTimeTwice()
        {
            var progress = new StartupProgress(new[] { "A" }, () => 0.0);
            progress.Complete("A");
            progress.Complete("A");
            Assert.That(progress.Timings.Count, Is.EqualTo(1));
        }
    }
}
