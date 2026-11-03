using Cities.Core.Ids;
using Cities.Simulation.Citizens;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Citizens
{
    public class HappinessModelTests
    {
        private static readonly HappinessFactors Ideal = new HappinessFactors(0f, 0f, 0f, 1f, 0f, 5f);
        private static readonly HappinessFactors Dreadful = new HappinessFactors(1f, 1f, 1f, 0f, -80f, 90f);

        private static Citizen Worker() => new Citizen { Age = 30, Work = new EntityId<BuildingKind>(3, 1), Happiness = 50 };

        [Test]
        public void IdealSurroundingsGiveFullHappinessAndDreadfulOnesGiveNone()
        {
            Assert.That(HappinessModel.Target(Worker(), Ideal), Is.EqualTo(100));
            var jobless = new Citizen { Age = 30 };
            Assert.That(HappinessModel.Target(jobless, Dreadful), Is.Zero);
        }

        [Test]
        public void EachFactorLowersTheTargetOnItsOwn()
        {
            var worker = Worker();
            var best = HappinessModel.Target(worker, Ideal);
            Assert.That(HappinessModel.Target(worker, new HappinessFactors(1f, 0f, 0f, 1f, 0f, 5f)), Is.LessThan(best), "vervuiling");
            Assert.That(HappinessModel.Target(worker, new HappinessFactors(0f, 1f, 0f, 1f, 0f, 5f)), Is.LessThan(best), "geluid");
            Assert.That(HappinessModel.Target(worker, new HappinessFactors(0f, 0f, 1f, 1f, 0f, 5f)), Is.LessThan(best), "veiligheid");
            Assert.That(HappinessModel.Target(worker, new HappinessFactors(0f, 0f, 0f, 0f, 0f, 5f)), Is.LessThan(best), "diensten");
            Assert.That(HappinessModel.Target(worker, new HappinessFactors(0f, 0f, 0f, 1f, -50f, 5f)), Is.LessThan(best), "belasting");
            Assert.That(HappinessModel.Target(worker, new HappinessFactors(0f, 0f, 0f, 1f, 0f, 60f)), Is.LessThan(best), "reistijd");
            Assert.That(HappinessModel.Target(new Citizen { Age = 30 }, Ideal), Is.LessThan(best), "werk");
        }

        [Test]
        public void ChildrenAndRetireesNeedNoJobAndLowTaxIsNotARewardOrPenalty()
        {
            Assert.That(HappinessModel.WorkSatisfaction(new Citizen { Age = 8 }), Is.EqualTo(1f));
            Assert.That(HappinessModel.WorkSatisfaction(new Citizen { Age = 70 }), Is.EqualTo(1f));
            Assert.That(HappinessModel.TaxSatisfaction(20f), Is.EqualTo(1f));
            Assert.That(HappinessModel.TaxSatisfaction(-25f), Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void CommuteSatisfactionFallsLinearlyAndIgnoresNonWorkers()
        {
            var worker = Worker();
            Assert.That(HappinessModel.CommuteSatisfaction(worker, 10f), Is.EqualTo(1f));
            Assert.That(HappinessModel.CommuteSatisfaction(worker, 35f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(HappinessModel.CommuteSatisfaction(worker, 200f), Is.Zero);
            Assert.That(HappinessModel.CommuteSatisfaction(new Citizen(), 200f), Is.EqualTo(1f));
        }

        [Test]
        public void HappinessMovesAtMostAFewPointsPerDayTowardsTheTarget()
        {
            var citizen = Worker();
            Assert.That(HappinessModel.Step(citizen, Ideal), Is.EqualTo(53));
            Assert.That(HappinessModel.Step(citizen, Dreadful), Is.EqualTo(47));
            citizen.Happiness = 99;
            Assert.That(HappinessModel.Step(citizen, Ideal), Is.EqualTo(100), "stopt bij het doel");
            citizen.Happiness = 100;
            Assert.That(HappinessModel.Step(citizen, Ideal), Is.EqualTo(100));
        }

        [Test]
        public void TheAverageIsZeroForAnEmptyCity()
        {
            Assert.That(HappinessModel.Average(System.Array.Empty<Citizen>()), Is.Zero);
            Assert.That(HappinessModel.Average(new[] { new Citizen { Happiness = 20 }, new Citizen { Happiness = 61 } }), Is.EqualTo(40.5f));
        }
    }
}
