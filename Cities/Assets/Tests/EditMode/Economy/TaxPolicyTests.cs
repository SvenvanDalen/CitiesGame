using System;
using Cities.Simulation.Demand;
using Cities.Simulation.Economy;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Economy
{
    public class TaxPolicyTests
    {
        [Test]
        public void EveryRateStartsAtTheDefault()
        {
            var tax = new TaxPolicy();

            Assert.That(tax.Rate(TaxZone.Office, TaxDensity.High), Is.EqualTo(TaxPolicy.DefaultRate));
            Assert.That(tax.Deviation(TaxZone.Residential), Is.Zero);
            Assert.That(tax.MigrationFactor(), Is.EqualTo(1f));
            Assert.That(tax.HappinessShift(), Is.Zero);
        }

        [Test]
        public void RatesAreSetPerZoneAndDensity()
        {
            var tax = new TaxPolicy();

            tax.SetRate(TaxZone.Commercial, TaxDensity.High, 0.2f);

            Assert.That(tax.Rate(TaxZone.Commercial, TaxDensity.High), Is.EqualTo(0.2f));
            Assert.That(tax.Rate(TaxZone.Commercial, TaxDensity.Low), Is.EqualTo(TaxPolicy.DefaultRate));
            Assert.That(tax.Rate(TaxZone.Industrial, TaxDensity.High), Is.EqualTo(TaxPolicy.DefaultRate));
        }

        [Test]
        public void RevenueFollowsTheRateAndIsBookedAsTaxIncome()
        {
            var tax = new TaxPolicy();
            tax.SetRate(TaxZone.Residential, TaxDensity.Low, 0.1f);
            var ledger = new Ledger(0);

            var amount = tax.Collect(ledger, 3, TaxZone.Residential, TaxDensity.Low, 12_345);

            Assert.That(amount, Is.EqualTo(1_235L), "1234,5 afgerond");
            Assert.That(ledger.Income(3, LedgerCategory.Taxes), Is.EqualTo(1_235L));
            Assert.That(ledger.Balance, Is.EqualTo(1_235L));
        }

        [Test]
        public void HighResidentialTaxLowersHousingDemandMigrationAndHappiness()
        {
            var tax = new TaxPolicy();
            tax.SetRate(TaxZone.Residential, TaxDensity.Low, 0.19f);
            tax.SetRate(TaxZone.Residential, TaxDensity.High, 0.19f);

            var result = tax.Apply(new Demand(0.5f, 0.5f, 0.5f));

            Assert.That(result.Residential, Is.EqualTo(0.3f).Within(1e-5f));
            Assert.That(result.Work, Is.EqualTo(0.5f));
            Assert.That(result.Commerce, Is.EqualTo(0.5f));
            Assert.That(tax.MigrationFactor(), Is.EqualTo(0.7f).Within(1e-5f));
            Assert.That(tax.HappinessShift(), Is.EqualTo(-10f).Within(1e-4f));
        }

        [Test]
        public void BusinessTaxLowersWorkDemandAndCommerceTaxLowersShopDemand()
        {
            var tax = new TaxPolicy();
            foreach (var density in new[] { TaxDensity.Low, TaxDensity.High })
            {
                tax.SetRate(TaxZone.Commercial, density, 0.19f);
            }

            var result = tax.Apply(new Demand(0f, 0f, 0f));

            Assert.That(result.Commerce, Is.EqualTo(-0.2f).Within(1e-5f));
            Assert.That(result.Work, Is.EqualTo(-0.2f / 3f).Within(1e-5f), "een van de drie bedrijfszones");
            Assert.That(result.Residential, Is.Zero);
        }

        [Test]
        public void LowTaxHelpsAndTheResultStaysWithinBounds()
        {
            var tax = new TaxPolicy();
            foreach (var density in new[] { TaxDensity.Low, TaxDensity.High })
            {
                tax.SetRate(TaxZone.Residential, density, 0f);
            }

            Assert.That(tax.Apply(new Demand(0.95f, 0f, 0f)).Residential, Is.EqualTo(1f), "begrensd op 1");
            Assert.That(tax.MigrationFactor(), Is.GreaterThan(1f));
            Assert.That(tax.HappinessShift(), Is.GreaterThan(0f));

            tax.SetRate(TaxZone.Residential, TaxDensity.Low, TaxPolicy.MaxRate);
            tax.SetRate(TaxZone.Residential, TaxDensity.High, TaxPolicy.MaxRate);
            Assert.That(tax.Apply(new Demand(-0.9f, 0f, 0f)).Residential, Is.EqualTo(-1f), "begrensd op -1");
            Assert.That(tax.MigrationFactor(), Is.GreaterThan(0f));
        }

        [Test]
        public void MigrationFactorAtTheMaximumTaxStaysPositive()
        {
            var tax = new TaxPolicy();
            foreach (var density in new[] { TaxDensity.Low, TaxDensity.High })
            {
                tax.SetRate(TaxZone.Residential, density, TaxPolicy.MaxRate);
            }

            Assert.That(tax.MigrationFactor(), Is.EqualTo(0.37f).Within(1e-5f));
        }

        [Test]
        public void BadInputIsRejected()
        {
            var tax = new TaxPolicy();

            Assert.Throws<ArgumentOutOfRangeException>(() => tax.SetRate(TaxZone.Office, TaxDensity.Low, -0.01f));
            Assert.Throws<ArgumentOutOfRangeException>(() => tax.SetRate(TaxZone.Office, TaxDensity.Low, TaxPolicy.MaxRate + 0.01f));
            Assert.Throws<ArgumentOutOfRangeException>(() => tax.SetRate(TaxZone.Office, TaxDensity.Low, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => tax.Rate((TaxZone)9, TaxDensity.Low));
            Assert.Throws<ArgumentOutOfRangeException>(() => tax.Rate(TaxZone.Office, (TaxDensity)5));
            Assert.Throws<ArgumentOutOfRangeException>(() => tax.Revenue(TaxZone.Office, TaxDensity.Low, -1));
            Assert.Throws<ArgumentNullException>(() => tax.Collect(null, 0, TaxZone.Office, TaxDensity.Low, 1));
        }
    }
}
