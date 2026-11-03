using Cities.Presentation.UI;
using Cities.Simulation.Economy;
using Cities.Simulation.Services;
using NUnit.Framework;

namespace Cities.Tests.EditMode.UI
{
    public class FinanceTextTests
    {
        [Test]
        public void PercentRoundsToWholePercent()
        {
            Assert.That(FinanceText.Percent(0.09f), Is.EqualTo("9%"));
            Assert.That(FinanceText.Percent(1.5f), Is.EqualTo("150%"));
        }

        [Test]
        public void EveryZoneDensityAndServiceHasADutchName()
        {
            Assert.That(FinanceText.Zone(TaxZone.Residential), Is.EqualTo("Wonen"));
            Assert.That(FinanceText.Zone(TaxZone.Commercial), Is.EqualTo("Handel"));
            Assert.That(FinanceText.Zone(TaxZone.Industrial), Is.EqualTo("Industrie"));
            Assert.That(FinanceText.Zone(TaxZone.Office), Is.EqualTo("Kantoor"));
            Assert.That(FinanceText.Density(TaxDensity.Low), Does.Contain("lage"));
            Assert.That(FinanceText.Density(TaxDensity.High), Does.Contain("hoge"));
            Assert.That(FinanceText.Service(ServiceKind.Fire), Is.EqualTo("Brandweer"));
            Assert.That(FinanceText.Service(ServiceKind.Police), Is.EqualTo("Politie"));
            Assert.That(FinanceText.Service(ServiceKind.Health), Is.EqualTo("Gezondheidszorg"));
            Assert.That(FinanceText.Service(ServiceKind.Death), Is.EqualTo("Begraafplaatsen"));
        }

        [Test]
        public void ForecastAndCostTexts()
        {
            Assert.That(FinanceText.WeeksOfMoney(null), Is.EqualTo(FinanceText.NotLosingMoney));
            Assert.That(FinanceText.WeeksOfMoney(12), Is.EqualTo("Geld voor 12 weken"));
            Assert.That(FinanceText.PerMonth(1500), Is.EqualTo("€ 1.500 per maand"));
            Assert.That(FinanceText.Locked(2500), Is.EqualTo("Vanaf 2.500 inwoners"));
        }

        [TestCase(PolicyToggleResult.Activated, "ingevoerd")]
        [TestCase(PolicyToggleResult.Deactivated, "ingetrokken")]
        [TestCase(PolicyToggleResult.Locked, "nog niet beschikbaar")]
        [TestCase(PolicyToggleResult.CannotAfford, "Niet genoeg geld")]
        [TestCase(PolicyToggleResult.Unknown, "Onbekend")]
        public void ResultMessagesNameThePolicy(PolicyToggleResult result, string expected)
        {
            var text = FinanceText.Result(result, "Recycling");

            Assert.That(text, Does.Contain(expected));
            if (result != PolicyToggleResult.Unknown)
            {
                Assert.That(text, Does.Contain("Recycling"));
            }
        }
    }
}
