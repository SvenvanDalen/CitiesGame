using System.Collections.Generic;
using Cities.Presentation.UI;
using Cities.Simulation.Economy;
using Cities.Simulation.Policies;
using NUnit.Framework;

namespace Cities.Tests.EditMode.UI
{
    public class PolicyTooltipTests
    {
        private static readonly PolicySpec Recycling = new PolicySpec
        {
            Scope = PolicyScope.City,
            ActivationCost = 5000,
            MonthlyCost = 3000,
            Effects = new[]
            {
                new PolicyEffect { Key = "tax.income", Multiplier = 1.1f },
                new PolicyEffect { Key = "waste.volume", Multiplier = 0.75f },
            },
        };

        private static long? Baseline(string key) => key == "tax.income" ? 10_000 : (long?)null;

        [Test]
        public void MonthlyCostBecomesADailyCost()
        {
            Assert.That(PolicyTooltip.DailyCost(Recycling, 0), Is.EqualTo(100));
            var perCitizen = new PolicySpec { MonthlyCostPerCitizen = 0.5f };
            Assert.That(PolicyTooltip.DailyCost(perCitizen, 600), Is.EqualTo(10));
        }

        [Test]
        public void DailyGainIsTheBaselineTimesTheChange()
        {
            Assert.That(PolicyTooltip.DailyGain(Recycling.Effects[0], Baseline), Is.EqualTo(1000));
            Assert.That(PolicyTooltip.DailyGain(Recycling.Effects[1], Baseline), Is.Null, "geen bekende dagwaarde");
            Assert.That(PolicyTooltip.DailyGain(Recycling.Effects[0], null), Is.Null);
        }

        [Test]
        public void NetDailyIsTheGainMinusTheDailyCost()
        {
            Assert.That(PolicyTooltip.NetDaily(Recycling, 0, Baseline), Is.EqualTo(900));
            Assert.That(PolicyTooltip.NetDaily(Recycling, 0, null), Is.EqualTo(-100));
            Assert.That(PolicyTooltip.NetDaily(new PolicySpec { MonthlyCost = 600 }, 0, null), Is.EqualTo(-20), "zonder effecten");
        }

        [Test]
        public void PercentagesShowTheDirectionOfTheChange()
        {
            Assert.That(PolicyTooltip.Percentage(1.25f), Is.EqualTo("+25%"));
            Assert.That(PolicyTooltip.Percentage(0.9f), Is.EqualTo("−10%"));
            Assert.That(PolicyTooltip.Percentage(1f), Is.EqualTo("geen verandering"));
        }

        [Test]
        public void TooltipListsCostsEffectsAndTheNetContribution()
        {
            var names = new Dictionary<string, string> { ["tax.income"] = "Belastinginkomsten" };

            var text = PolicyTooltip.Build("Recycling", Recycling, 0, names, Baseline);

            Assert.That(text, Does.StartWith("Recycling\n"));
            Assert.That(text, Does.Contain("Invoeren: € 5"));
            Assert.That(text, Does.Contain("per dag)"));
            Assert.That(text, Does.Contain("Belastinginkomsten: +10% (+€ 1"));
            Assert.That(text, Does.Contain("waste.volume: −25%"), "zonder naam de sleutel, zonder basis geen dagwaarde");
            Assert.That(text, Does.Contain("Netto: +€ 900 per dag"));
        }

        [Test]
        public void ModelBuildsTheTooltipAndIgnoresUnknownPolicies()
        {
            var specs = new Dictionary<string, PolicySpec> { ["recycling"] = Recycling };
            var model = new PolicyPanelModel(
                new PolicyBook(specs),
                specs,
                new Dictionary<string, string> { ["recycling"] = "Recycling" },
                new Ledger(0),
                () => 0,
                district => 0)
            {
                DailyBaseline = Baseline,
            };

            Assert.That(model.TooltipFor("recycling"), Does.Contain("Netto: +€ 900 per dag"));
            Assert.That(model.TooltipFor("onbekend"), Is.Empty);
        }
    }
}
