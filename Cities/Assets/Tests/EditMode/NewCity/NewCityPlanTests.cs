using System;
using System.Collections.Generic;
using Cities.Gameplay.NewCity;
using NUnit.Framework;

namespace Cities.Tests.EditMode.NewCity
{
    public class NewCityPlanTests
    {
        private static List<string> Errors(NewCityPlan plan)
        {
            var errors = new List<string>();
            plan.Validate(errors);
            return errors;
        }

        [Test]
        public void DifficultyDecidesTheStartingMoney()
        {
            Assert.That(new NewCityPlan("A", 1, Difficulty.Easy, 2000).StartMoney, Is.EqualTo(2_000_000));
            Assert.That(new NewCityPlan("A", 1, Difficulty.Normal, 2000).StartMoney, Is.EqualTo(1_000_000));
            Assert.That(new NewCityPlan("A", 1, Difficulty.Hard, 2000).StartMoney, Is.EqualTo(500_000));
        }

        [Test]
        public void TheSeedGivesTheMapAndTheYearTheClock()
        {
            var plan = new NewCityPlan("Utrecht", 4242, Difficulty.Normal, 1975);

            Assert.That(plan.Terrain().Seed, Is.EqualTo(4242));
            Assert.That(plan.Clock().StartYear, Is.EqualTo(1975));
            Assert.That(plan.Clock().StartMonth, Is.EqualTo(1));
            Assert.That(plan.Clock().StartDay, Is.EqualTo(1));
        }

        [Test]
        public void AValidPlanHasNoErrors() =>
            Assert.That(Errors(new NewCityPlan("Utrecht", 1, Difficulty.Normal, 2000)), Is.Empty);

        [Test]
        public void NameYearAndDifficultyAreChecked()
        {
            Assert.That(Errors(new NewCityPlan("  ", 1, Difficulty.Normal, 2000)), Has.Some.Contains("naam"));
            Assert.That(Errors(new NewCityPlan(null, 1, Difficulty.Normal, 2000)), Has.Some.Contains("naam"));
            Assert.That(Errors(new NewCityPlan(new string('x', 41), 1, Difficulty.Normal, 2000)), Has.Some.Contains("hoogstens"));
            Assert.That(Errors(new NewCityPlan("A", 1, Difficulty.Normal, 1800)), Has.Some.Contains("startjaar"));
            Assert.That(Errors(new NewCityPlan("A", 1, Difficulty.Normal, 2200)), Has.Some.Contains("startjaar"));
            Assert.That(Errors(new NewCityPlan("A", 1, (Difficulty)9, 2000)), Has.Some.Contains("moeilijkheid"));
            Assert.Throws<ArgumentNullException>(() => new NewCityPlan("A", 1, Difficulty.Normal, 2000).Validate(null));
        }
    }
}
