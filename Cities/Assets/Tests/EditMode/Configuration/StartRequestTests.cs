using System;
using Cities.Core.Configuration;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Configuration
{
    public class StartRequestTests
    {
        [SetUp]
        public void SetUp() => StartRequest.Reset();

        [TearDown]
        public void TearDown() => StartRequest.Reset();

        [Test]
        public void TakeReturnsTheRequestOnlyOnce()
        {
            StartRequest.Submit(new StartRequest { Seed = 42UL, LoadSlot = "Stad" });

            var first = StartRequest.Take();
            var second = StartRequest.Take();

            Assert.That(first.Seed, Is.EqualTo(42UL));
            Assert.That(first.LoadSlot, Is.EqualTo("Stad"));
            Assert.That(second, Is.Null);
            Assert.That(StartRequest.Pending, Is.Null);
        }

        [Test]
        public void DefaultsToNormalAndSkipsTheMenu()
        {
            var request = new StartRequest();
            Assert.That(request.Difficulty, Is.EqualTo(1));
            Assert.That(request.SkipMenu, Is.True);
            Assert.That(request.Seed, Is.Null);
        }

        [Test]
        public void SubmitRejectsNull() => Assert.Throws<ArgumentNullException>(() => StartRequest.Submit(null));

        [Test]
        public void RememberedDifficultyIsClamped()
        {
            StartRequest.RememberDifficulty(7);
            Assert.That(StartRequest.ChosenDifficulty, Is.EqualTo(2));
            StartRequest.RememberDifficulty(-3);
            Assert.That(StartRequest.ChosenDifficulty, Is.EqualTo(0));
        }
    }
}
