using System.Collections.Generic;
using Cities.Presentation.UI;
using Cities.Simulation.Economy;
using Cities.Simulation.Policies;
using Cities.Simulation.Services;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine.UIElements;

namespace Cities.Tests.EditMode.UI
{
    [Category("UnityRuntime")]
    public class FinancePanelTests
    {
        [Test]
        public void BudgetPanelShowsEveryRowAndRedrawsAfterAChange()
        {
            var ledger = new Ledger(1000);
            var model = new BudgetPanelModel(
                new TaxPolicy(),
                new Budget(),
                new Loans(ledger, 100_000),
                ledger,
                () => new List<ServiceStation> { new ServiceStation(ServiceKind.Fire, float2.zero, 50f, 1, 3000) },
                () => 5000,
                () => 0);
            using (var panel = new BudgetPanel(model))
            {
                Assert.That(panel.Q("tax-Residential-Low").Q<Label>("value").text, Is.EqualTo("9%"));
                Assert.That(panel.Q("service-Fire").Q<Label>("value").text, Is.EqualTo("100%"));
                Assert.That(panel.Q("loan"), Is.Not.Null);

                model.StepTax(TaxZone.Residential, TaxDensity.Low, 1);
                model.StepFunding(ServiceKind.Fire, -1);

                Assert.That(panel.Q("tax-Residential-Low").Q<Label>("value").text, Is.EqualTo("10%"));
                Assert.That(panel.Q("service-Fire").Q<Label>("value").text, Is.EqualTo("90%"));
            }
        }

        [Test]
        public void PolicyPanelListsPoliciesAndFollowsTheModel()
        {
            var specs = new Dictionary<string, PolicySpec> { ["recycling"] = new PolicySpec { Scope = PolicyScope.City, ActivationCost = 500 } };
            var ledger = new Ledger(1000);
            var model = new PolicyPanelModel(new PolicyBook(specs), specs, new Dictionary<string, string> { ["recycling"] = "Recycling" }, ledger, () => 0, d => 100);
            using (var panel = new PolicyPanel(model))
            {
                var button = panel.Q("recycling").Q<CityButton>("toggle");
                Assert.That(button.text, Is.EqualTo("Invoeren"));
                Assert.That(button.enabledSelf, Is.True);

                model.Toggle("recycling");

                Assert.That(panel.Q("recycling").Q<CityButton>("toggle").text, Is.EqualTo("Intrekken"));
            }
        }
    }
}
