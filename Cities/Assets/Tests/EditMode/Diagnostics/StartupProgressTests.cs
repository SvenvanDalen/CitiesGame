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
    }
}
