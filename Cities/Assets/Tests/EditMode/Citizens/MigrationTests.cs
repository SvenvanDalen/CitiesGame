using System;
using Cities.Simulation.Citizens;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Citizens
{
    public class MigrationTests
    {
        private static readonly CityAppeal Great = new CityAppeal(1f, 1f, 1f, 1f);
        private static readonly CityAppeal Awful = new CityAppeal(0f, 0f, 0f, 0f);

        [Test]
        public void AnAttractiveCityDrawsPeopleUpToTheVacantHomes()
        {
            Assert.That(Migration.DailyNet(Great, 10000, 1000), Is.EqualTo(20));
            Assert.That(Migration.DailyNet(Great, 10000, 5), Is.EqualTo(5), "zonder genoeg woningen geen volle instroom");
            Assert.That(Migration.DailyNet(Great, 10000, 0), Is.Zero);
        }

        [Test]
        public void AnUnattractiveCityLosesPeopleButNeverMoreThanItHas()
        {
            Assert.That(Migration.DailyNet(Awful, 10000, 1000), Is.EqualTo(-20));
            Assert.That(Migration.DailyNet(Awful, 0, 0), Is.Zero);
        }

        [Test]
        public void AnEmptyAttractiveCityGetsItsFirstSettler()
        {
            Assert.That(Migration.DailyNet(Great, 0, 10), Is.EqualTo(1));
        }

        [Test]
        public void ANeutralCityHasNoMigration()
        {
            var neutral = new CityAppeal(0.5f, 0.5f, 0.5f, 0.5f);

            Assert.That(Migration.Attractiveness(neutral), Is.EqualTo(Migration.Neutral).Within(1e-5f));
            Assert.That(Migration.DailyNet(neutral, 10000, 100), Is.Zero);
        }

        [Test]
        public void HighTaxesAndMissingJobsPushPeopleAway()
        {
            var good = new CityAppeal(0.6f, 0.5f, 0.5f, 1f);
            var taxed = new CityAppeal(0.6f, 0.5f, 0.5f, 0.2f);
            var jobless = new CityAppeal(0f, 0.5f, 0.5f, 1f);

            Assert.That(Migration.Attractiveness(taxed), Is.LessThan(Migration.Attractiveness(good)));
            Assert.That(Migration.Attractiveness(jobless), Is.LessThan(Migration.Attractiveness(good)));
        }

        [Test]
        public void ValuesOutsideTheRangeAreClamped()
        {
            Assert.That(Migration.Attractiveness(new CityAppeal(5f, 5f, 5f, 5f)), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(Migration.Attractiveness(new CityAppeal(-1f, -1f, -1f, -1f)), Is.Zero);
        }

        [Test]
        public void NegativeCountsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Migration.DailyNet(Great, -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Migration.DailyNet(Great, 0, -1));
        }
    }
}
