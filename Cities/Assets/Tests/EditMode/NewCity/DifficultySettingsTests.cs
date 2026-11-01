using System;
using Cities.Gameplay.NewCity;
using NUnit.Framework;

namespace Cities.Tests.EditMode.NewCity
{
    public class DifficultySettingsTests
    {
        [Test]
        public void HarderDifficultyMeansLessMoneyAndHigherCosts()
        {
            var easy = DifficultySettings.For(Difficulty.Easy);
            var normal = DifficultySettings.For(Difficulty.Normal);
            var hard = DifficultySettings.For(Difficulty.Hard);

            Assert.That(easy.StartMoney, Is.GreaterThan(normal.StartMoney));
            Assert.That(normal.StartMoney, Is.GreaterThan(hard.StartMoney));
            Assert.That(easy.CostFactor, Is.LessThan(normal.CostFactor));
            Assert.That(normal.CostFactor, Is.LessThan(hard.CostFactor));
            Assert.That(normal.UnlimitedMoney, Is.False);
            Assert.That(normal.AllUnlocked, Is.False);
        }

        [Test]
        public void TheNewCityPlanUsesTheSameStartMoney()
        {
            foreach (var difficulty in new[] { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard })
            {
                Assert.That(new NewCityPlan("x", 1, difficulty, 2000).StartMoney, Is.EqualTo(DifficultySettings.For(difficulty).StartMoney));
            }
        }

        [Test]
        public void TheCostFactorScalesAndRoundsUp()
        {
            Assert.That(DifficultySettings.For(Difficulty.Hard).Cost(1000), Is.EqualTo(1250));
            Assert.That(DifficultySettings.For(Difficulty.Easy).Cost(1000), Is.EqualTo(750));
            Assert.That(DifficultySettings.For(Difficulty.Easy).Cost(3), Is.EqualTo(3), "2,25 rondt naar boven");
            Assert.That(DifficultySettings.For(Difficulty.Normal).Cost(0), Is.Zero);
        }

        [Test]
        public void UnlimitedMoneyMakesEverythingFreeAndAffordable()
        {
            var sandbox = DifficultySettings.For(Difficulty.Hard, unlimitedMoney: true, allUnlocked: true);

            Assert.That(sandbox.Cost(1_000_000), Is.Zero);
            Assert.That(sandbox.CanAfford(0, 1_000_000), Is.True);
            Assert.That(sandbox.AllUnlocked, Is.True);
            Assert.That(DifficultySettings.For(Difficulty.Normal).CanAfford(10, 11), Is.False);
            Assert.That(DifficultySettings.For(Difficulty.Normal).CanAfford(11, 11), Is.True);
        }

        [Test]
        public void BadInputIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => DifficultySettings.For((Difficulty)9));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DifficultySettings(-1, 1f, false, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DifficultySettings(0, 0f, false, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => DifficultySettings.For(Difficulty.Normal).Cost(-1));
        }
    }
}
