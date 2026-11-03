using System;
using Cities.Core.Ids;
using Cities.Simulation.Citizens;
using Cities.World.Buildings;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Citizens
{
    public class HousingTests
    {
        private static readonly EntityId<BuildingKind> Job = new EntityId<BuildingKind>(5, 1);

        private static Citizen Worker(byte education) => new Citizen { Age = 35, Education = education, Work = Job };

        [Test]
        public void HouseholdIncomeAddsUpTheWagesOfTheMembers()
        {
            var members = new[] { Worker(0), Worker(2), new Citizen { Age = 8 } };

            Assert.That(Housing.HouseholdIncome(members), Is.EqualTo(EducationEconomy.Wage(0) + EducationEconomy.Wage(2)));
        }

        [Test]
        public void IncomeClassesFollowTheThresholds()
        {
            Assert.That(Housing.ClassOf(0), Is.EqualTo(IncomeClass.Low));
            Assert.That(Housing.ClassOf(Housing.MiddleIncomeFrom - 1), Is.EqualTo(IncomeClass.Low));
            Assert.That(Housing.ClassOf(Housing.MiddleIncomeFrom), Is.EqualTo(IncomeClass.Middle));
            Assert.That(Housing.ClassOf(Housing.HighIncomeFrom), Is.EqualTo(IncomeClass.High));
        }

        [Test]
        public void LandValueDecidesTheTierAndRentRisesWithIt()
        {
            Assert.That(Housing.TierOf(0.1f), Is.EqualTo(HousingTier.Affordable));
            Assert.That(Housing.TierOf(Housing.StandardFromLandValue), Is.EqualTo(HousingTier.Standard));
            Assert.That(Housing.TierOf(Housing.PremiumFromLandValue), Is.EqualTo(HousingTier.Premium));
            Assert.That(Housing.RentFor(0.9f), Is.GreaterThan(Housing.RentFor(0.2f)));
        }

        [Test]
        public void RentMustFitWithinTheRentShareOfTheIncome()
        {
            Assert.That(Housing.CanAfford(300, 90f), Is.True);
            Assert.That(Housing.CanAfford(300, 91f), Is.False);
            Assert.That(Housing.CanAfford(0, 1f), Is.False);
        }

        [Test]
        public void ALowIncomeHouseholdOnlyFitsCheapHomes()
        {
            Assert.That(Housing.Fits(300, 0.1f), Is.True);
            Assert.That(Housing.Fits(300, 0.9f), Is.False);
        }

        [Test]
        public void AHighIncomeHouseholdAlsoFitsCheaperHomes()
        {
            Assert.That(Housing.Fits(2000, 0.9f), Is.True);
            Assert.That(Housing.Fits(2000, 0.1f), Is.True);
        }

        [Test]
        public void ARichHouseholdOnATooExpensiveRentStillCannotAfford()
        {
            Assert.That(Housing.Fits(Housing.HighIncomeFrom, 1f), Is.True);
            Assert.That(Housing.CanAfford(100, Housing.RentFor(1f)), Is.False);
        }

        [Test]
        public void ClassesAreCountedPerIncomeClass()
        {
            var counts = new int[Housing.ClassCount];

            Housing.CountClasses(new[] { 100, 200, 700, 2000 }, counts);

            Assert.That(counts, Is.EqualTo(new[] { 2, 1, 1 }));
        }

        [Test]
        public void CountingNeedsRoomForEveryClass()
        {
            Assert.Throws<ArgumentException>(() => Housing.CountClasses(new[] { 1 }, new int[2]));
        }

        [Test]
        public void GrowthFollowsTheDemandOfTheMatchingClass()
        {
            var demand = new[] { 6, 3, 1 };

            Assert.That(Housing.GrowthWeight(0.1f, demand), Is.EqualTo(0.6f).Within(1e-6f));
            Assert.That(Housing.GrowthWeight(0.5f, demand), Is.EqualTo(0.3f).Within(1e-6f));
            Assert.That(Housing.GrowthWeight(0.9f, demand), Is.EqualTo(0.1f).Within(1e-6f));
        }

        [Test]
        public void WithoutDemandNothingGrows()
        {
            Assert.That(Housing.GrowthWeight(0.9f, new[] { 0, 0, 0 }), Is.EqualTo(0f));
        }

        [Test]
        public void InvalidDemandIsRejected()
        {
            Assert.Throws<ArgumentException>(() => Housing.GrowthWeight(0.5f, new[] { 1, 2 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => Housing.GrowthWeight(0.5f, new[] { 1, -2, 3 }));
        }
    }
}
