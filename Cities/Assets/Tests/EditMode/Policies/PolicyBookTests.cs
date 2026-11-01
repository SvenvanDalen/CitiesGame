using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;
using Cities.Simulation.Policies;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Policies
{
    public class PolicyBookTests
    {
        private static PolicyBook NewBook() => new PolicyBook(new Dictionary<string, PolicySpec>
        {
            ["policy.free-transit"] = new PolicySpec
            {
                Scope = PolicyScope.City,
                ActivationCost = 1000,
                MonthlyCost = 200,
                Effects = new[] { new PolicyEffect { Key = "transit.ridership", Multiplier = 1.5f }, new PolicyEffect { Key = "tax.income", Multiplier = 0.9f } },
            },
            ["policy.heavy-traffic-ban"] = new PolicySpec
            {
                Scope = PolicyScope.District,
                ActivationCost = 50,
                MonthlyCostPerCitizen = 0.1f,
                Effects = new[] { new PolicyEffect { Key = "tax.income", Multiplier = 0.8f } },
            },
            ["policy.plain"] = new PolicySpec { Scope = PolicyScope.City },
        });

        [Test]
        public void ActivatingReturnsTheOneOffCostAndTurnsThePolicyOn()
        {
            var book = NewBook();

            Assert.That(book.Activate("policy.free-transit"), Is.EqualTo(1000));
            Assert.That(book.IsActive("policy.free-transit"), Is.True);
            Assert.That(book.Deactivate("policy.free-transit"), Is.True);
            Assert.That(book.Deactivate("policy.free-transit"), Is.False);
            Assert.That(book.Count, Is.Zero);
        }

        [Test]
        public void ScopeAndDuplicatesAreChecked()
        {
            var book = NewBook();
            book.Activate("policy.free-transit");

            Assert.Throws<InvalidOperationException>(() => book.Activate("policy.free-transit"));
            Assert.Throws<ArgumentOutOfRangeException>(() => book.Activate("policy.free-transit", 2), "stadsbreed beleid hoort niet bij een district");
            Assert.Throws<ArgumentOutOfRangeException>(() => book.Activate("policy.heavy-traffic-ban"), "districtbeleid heeft een district nodig");
            Assert.Throws<ArgumentOutOfRangeException>(() => book.Activate("policy.heavy-traffic-ban", -1));
            Assert.Throws<ArgumentException>(() => book.Activate("policy.unknown"));
            Assert.Throws<ArgumentException>(() => book.Activate(null));
            Assert.That(book.IsKnown("policy.plain"), Is.True);
        }

        [Test]
        public void EffectsMultiplyAndDistrictPolicyOnlyAppliesThere()
        {
            var book = NewBook();
            book.Activate("policy.free-transit");
            book.Activate("policy.heavy-traffic-ban", 3);

            Assert.That(book.Multiplier("tax.income"), Is.EqualTo(0.9f).Within(1e-5f), "stad zonder district kent alleen stadsbreed beleid");
            Assert.That(book.Multiplier("tax.income", 3), Is.EqualTo(0.72f).Within(1e-5f));
            Assert.That(book.Multiplier("tax.income", 4), Is.EqualTo(0.9f).Within(1e-5f));
            Assert.That(book.Multiplier("transit.ridership", 3), Is.EqualTo(1.5f));
            Assert.That(book.Multiplier("nothing"), Is.EqualTo(1f));
        }

        [Test]
        public void PolicyWithoutEffectsChangesNothing()
        {
            var book = NewBook();
            book.Activate("policy.plain");

            Assert.That(book.Multiplier("tax.income"), Is.EqualTo(1f));
        }

        [Test]
        public void MonthlyCostUsesThePopulationOfTheArea()
        {
            var book = NewBook();
            book.Activate("policy.free-transit");
            book.Activate("policy.heavy-traffic-ban", 3);

            var cost = book.MonthlyCost(district => district == PolicyBook.CityWide ? 50_000 : 2000);

            Assert.That(cost, Is.EqualTo(200 + 200));
            Assert.Throws<ArgumentNullException>(() => book.MonthlyCost(null));
            Assert.Throws<ArgumentNullException>(() => new PolicyBook(null));
        }

        [Test]
        public void SpecValidationRejectsEmptyKeysAndWildMultipliers()
        {
            var errors = new List<string>();
            new PolicySpec { Effects = new[] { new PolicyEffect { Key = " ", Multiplier = 50f } } }.Validate(errors);

            Assert.That(errors.Count, Is.EqualTo(2));
            errors.Clear();
            new PolicySpec().Validate(errors);
            Assert.That(errors, Is.Empty);
        }

        [Test]
        public void ThePolicyBookRoundTripsThroughASave()
        {
            var book = NewBook();
            book.Activate("policy.heavy-traffic-ban", 3);
            book.Activate("policy.free-transit");
            var bytes = SaveBytes.Write(new SaveMetadata(), new PolicySection(book));
            var loaded = NewBook();
            loaded.Activate("policy.plain");

            SaveBytes.Read(bytes, new PolicySection(loaded));

            Assert.That(loaded.Active.ToArray(), Is.EqualTo(new[] { ("policy.free-transit", 0), ("policy.heavy-traffic-ban", 3) }));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new PolicySection(loaded)), Is.EqualTo(bytes));
        }

        [Test]
        public void ASaveWithUnknownPolicyOrWrongScopeIsRejectedAndChangesNothing()
        {
            var loaded = NewBook();
            loaded.Activate("policy.plain");

            Assert.Throws<SaveFormatException>(() => new PolicySection(loaded).Read(Reader("policy.gone", 0), 1));
            Assert.Throws<SaveFormatException>(() => new PolicySection(loaded).Read(Reader("policy.free-transit", 4), 1));
            Assert.Throws<SaveFormatException>(() => new PolicySection(loaded).Read(SaveBytes.ReaderFor(w => w.Write(-1)), 1));
            Assert.That(loaded.IsActive("policy.plain"), Is.True);
        }

        private static SaveReader Reader(string id, int district) => SaveBytes.ReaderFor(w =>
        {
            w.Write(1);
            w.Write(id);
            w.Write(district);
        });
    }
}
