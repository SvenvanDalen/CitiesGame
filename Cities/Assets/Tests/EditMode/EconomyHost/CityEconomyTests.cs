using Cities.Gameplay.Economy;
using Cities.Gameplay.NewCity;
using Cities.Gameplay.Population;
using NUnit.Framework;

namespace Cities.Tests.EditMode.EconomyHost
{
    public class CityEconomyTests
    {
        [Test]
        public void StartCapitalFollowsDifficulty()
        {
            Assert.That(new CityEconomy(Difficulty.Easy).Balance, Is.GreaterThan(new CityEconomy(Difficulty.Normal).Balance));
            Assert.That(new CityEconomy(Difficulty.Hard).Balance, Is.LessThan(new CityEconomy(Difficulty.Normal).Balance));
        }

        [Test]
        public void BuildingCostsMoneyAndFailsWhenBroke()
        {
            var economy = new CityEconomy(Difficulty.Normal);
            var before = economy.Balance;

            Assert.That(economy.TryBuild(0, 10_000), Is.True);
            Assert.That(economy.Balance, Is.EqualTo(before - 10_000));
            Assert.That(economy.CanAfford(economy.Balance + 1), Is.False);
            Assert.That(economy.TryBuild(0, economy.Balance + 1), Is.False);
        }

        [Test]
        public void PopulationPaysTaxesEachWeek()
        {
            var economy = new CityEconomy(Difficulty.Normal);
            var city = new CityPopulation();
            city.AddBuilding(200, 100, 5000);
            for (var day = 0; day < 20; day++)
            {
                city.Daily();
            }

            var before = economy.Balance;
            economy.EndWeek(1, city);

            Assert.That(city.Population, Is.GreaterThan(0));
            Assert.That(economy.Balance, Is.GreaterThan(before));
            Assert.That(economy.WeeklyTaxIncome(city), Is.GreaterThan(0));
        }

        [Test]
        public void UpkeepIsPaidEachWeek()
        {
            var economy = new CityEconomy(Difficulty.Normal);
            economy.AddUpkeep(30_000);
            var before = economy.Balance;

            economy.EndWeek(1, new CityPopulation());

            Assert.That(economy.Balance, Is.LessThan(before));
        }
    }
}
