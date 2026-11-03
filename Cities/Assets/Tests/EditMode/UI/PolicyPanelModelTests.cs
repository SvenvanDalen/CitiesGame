using System.Collections.Generic;
using System.Linq;
using Cities.Presentation.UI;
using Cities.Simulation.Economy;
using Cities.Simulation.Policies;
using NUnit.Framework;

namespace Cities.Tests.EditMode.UI
{
    public class PolicyPanelModelTests
    {
        private PolicyBook _book;
        private Ledger _ledger;
        private PolicyPanelModel _model;
        private int _citizens;
        private int _changes;

        [SetUp]
        public void SetUp()
        {
            var specs = new Dictionary<string, PolicySpec>
            {
                ["recycling"] = new PolicySpec { Scope = PolicyScope.City, ActivationCost = 5000, MonthlyCost = 300 },
                ["free-transit"] = new PolicySpec { Scope = PolicyScope.City, ActivationCost = 20_000, MonthlyCostPerCitizen = 0.5f, UnlockPopulation = 1000 },
                ["quiet"] = new PolicySpec { Scope = PolicyScope.District, ActivationCost = 1000, MonthlyCost = 100 },
            };
            var names = new Dictionary<string, string> { ["recycling"] = "Recycling", ["free-transit"] = "Gratis OV" };
            _book = new PolicyBook(specs);
            _ledger = new Ledger(10_000);
            _citizens = 500;
            _model = new PolicyPanelModel(_book, specs, names, _ledger, () => 2, district => _citizens);
            _changes = 0;
            _model.Changed += () => _changes++;
        }

        [Test]
        public void ListsCityPoliciesSortedByNameWithTheirState()
        {
            var entries = _model.Entries();

            Assert.That(entries.Select(e => e.Name), Is.EqualTo(new[] { "Gratis OV", "Recycling" }));
            var transit = entries[0];
            Assert.That(transit.Locked, Is.True);
            Assert.That(transit.CanAfford, Is.False);
            Assert.That(transit.CanToggle, Is.False);
            Assert.That(transit.MonthlyCost, Is.EqualTo(250));
            Assert.That(transit.UnlockPopulation, Is.EqualTo(1000));
            var recycling = entries[1];
            Assert.That(recycling.Locked, Is.False);
            Assert.That(recycling.CanToggle, Is.True);
            Assert.That(recycling.ActivationCost, Is.EqualTo(5000));
        }

        [Test]
        public void DistrictListShowsOnlyDistrictPoliciesAndFallsBackToTheId()
        {
            var entries = _model.Entries(1);

            Assert.That(entries.Count, Is.EqualTo(1));
            Assert.That(entries[0].Id, Is.EqualTo("quiet"));
            Assert.That(entries[0].Name, Is.EqualTo("quiet"));
            Assert.That(_model.NameOf(null), Is.Null);
        }

        [Test]
        public void ActivatingChargesTheCostAndTogglingAgainWithdraws()
        {
            Assert.That(_model.Toggle("recycling"), Is.EqualTo(PolicyToggleResult.Activated));
            Assert.That(_ledger.Balance, Is.EqualTo(5000));
            Assert.That(_book.IsActive("recycling"), Is.True);
            Assert.That(_model.ActiveCount, Is.EqualTo(1));
            Assert.That(_model.MonthlyCost, Is.EqualTo(300));
            Assert.That(_model.Entries().Single(e => e.Id == "recycling").Active, Is.True);

            Assert.That(_model.Toggle("recycling"), Is.EqualTo(PolicyToggleResult.Deactivated));
            Assert.That(_ledger.Balance, Is.EqualTo(5000), "intrekken geeft niets terug");
            Assert.That(_book.IsActive("recycling"), Is.False);
            Assert.That(_changes, Is.EqualTo(2));
        }

        [Test]
        public void RefusesLockedAndUnaffordablePolicies()
        {
            Assert.That(_model.Toggle("free-transit"), Is.EqualTo(PolicyToggleResult.Locked));

            _citizens = 2000;
            Assert.That(_model.Toggle("free-transit"), Is.EqualTo(PolicyToggleResult.CannotAfford));

            Assert.That(_ledger.Balance, Is.EqualTo(10_000));
            Assert.That(_book.Count, Is.Zero);
            Assert.That(_changes, Is.Zero);
        }

        [Test]
        public void UnknownPolicyOrWrongDistrictIsRejected()
        {
            Assert.That(_model.Toggle("nope"), Is.EqualTo(PolicyToggleResult.Unknown));
            Assert.That(_model.Toggle(null), Is.EqualTo(PolicyToggleResult.Unknown));
            Assert.That(_model.Toggle("recycling", 1), Is.EqualTo(PolicyToggleResult.Unknown), "stadsbeleid per district");
            Assert.That(_model.Toggle("quiet"), Is.EqualTo(PolicyToggleResult.Unknown), "districtsbeleid stadsbreed");
            Assert.That(_model.Toggle("quiet", -1), Is.EqualTo(PolicyToggleResult.Unknown));
        }

        [Test]
        public void DistrictPolicyIsKeptPerDistrict()
        {
            Assert.That(_model.Toggle("quiet", 2), Is.EqualTo(PolicyToggleResult.Activated));

            Assert.That(_model.Entries(2)[0].Active, Is.True);
            Assert.That(_model.Entries(1)[0].Active, Is.False);
        }

        [Test]
        public void ConstructorRejectsMissingParts()
        {
            var specs = new Dictionary<string, PolicySpec>();
            var names = new Dictionary<string, string>();
            Assert.Throws<System.ArgumentNullException>(() => new PolicyPanelModel(null, specs, names, _ledger, () => 0, d => 0));
            Assert.Throws<System.ArgumentNullException>(() => new PolicyPanelModel(_book, null, names, _ledger, () => 0, d => 0));
            Assert.Throws<System.ArgumentNullException>(() => new PolicyPanelModel(_book, specs, null, _ledger, () => 0, d => 0));
            Assert.Throws<System.ArgumentNullException>(() => new PolicyPanelModel(_book, specs, names, null, () => 0, d => 0));
            Assert.Throws<System.ArgumentNullException>(() => new PolicyPanelModel(_book, specs, names, _ledger, null, d => 0));
            Assert.Throws<System.ArgumentNullException>(() => new PolicyPanelModel(_book, specs, names, _ledger, () => 0, null));
        }
    }
}
