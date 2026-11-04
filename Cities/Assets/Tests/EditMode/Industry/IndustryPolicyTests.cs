using System;
using System.Collections.Generic;
using Cities.Simulation.Industry;
using Cities.Simulation.Policies;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Industry
{
    public class IndustryPolicyTests
    {
        private static PolicyBook Book() => new PolicyBook(new Dictionary<string, PolicySpec>
        {
            ["policy.ban-oil"] = new PolicySpec
            {
                Scope = PolicyScope.District,
                Effects = new[] { new PolicyEffect { Key = IndustryPolicy.EffectKey(IndustryKind.Oil), Multiplier = 0f } },
            },
            ["policy.farm-subsidy"] = new PolicySpec
            {
                Scope = PolicyScope.District,
                Effects = new[] { new PolicyEffect { Key = IndustryPolicy.EffectKey(IndustryKind.Agriculture), Multiplier = 1.5f } },
            },
            ["policy.green-industry"] = new PolicySpec
            {
                Scope = PolicyScope.City,
                Effects = new[] { new PolicyEffect { Key = IndustryPolicy.EffectKey(IndustryKind.Ore), Multiplier = 0.5f } },
            },
        });

        [Test]
        public void EffectKeyUsesLowercaseKindName()
        {
            Assert.That(IndustryPolicy.EffectKey(IndustryKind.Forestry), Is.EqualTo("industry.forestry"));
        }

        [Test]
        public void WithoutPolicyEverythingIsEnabledAtFactorOne()
        {
            Assert.That(IndustryPolicy.Factor(null, IndustryKind.Oil, 1), Is.EqualTo(1f));
            Assert.That(IndustryPolicy.Enabled(Book(), IndustryKind.Oil, 1), Is.True);
        }

        [Test]
        public void DistrictPolicyBansOnlyInThatDistrict()
        {
            var book = Book();
            book.Activate("policy.ban-oil", 2);

            Assert.That(IndustryPolicy.Enabled(book, IndustryKind.Oil, 2), Is.False);
            Assert.That(IndustryPolicy.Enabled(book, IndustryKind.Oil, 3), Is.True);
            Assert.That(IndustryPolicy.Enabled(book, IndustryKind.Ore, 2), Is.True);
        }

        [Test]
        public void CityPolicyAppliesEverywhere()
        {
            var book = Book();
            book.Activate("policy.green-industry");

            Assert.That(IndustryPolicy.Factor(book, IndustryKind.Ore, 5), Is.EqualTo(0.5f));
        }

        [Test]
        public void StepScalesOutputAndSkipsBannedSites()
        {
            var network = new IndustryNetwork();
            network.Add(new ExtractionSite(1, IndustryKind.Oil, 1, 0.5f));
            network.Add(new ExtractionSite(2, IndustryKind.Ore, 1, 0.5f));
            var plain = new IndustryNetwork();
            plain.Add(new ExtractionSite(2, IndustryKind.Ore, 1, 0.5f));
            var book = Book();
            book.Activate("policy.ban-oil", 1);
            book.Activate("policy.green-industry");
            var factor = IndustryPolicy.FactorOf(book, site => 1);

            var week = network.Step(0, factor);
            var baseline = plain.Step(0);

            Assert.That(week.Of("olie"), Is.Zero);
            Assert.That(network.Get(1).Stock, Is.EqualTo(network.Get(1).Capacity));
            Assert.That(week.Of("erts"), Is.EqualTo(baseline.Of("erts") * 0.5f).Within(1e-4f));
            Assert.That(week.Pollution, Is.EqualTo(baseline.Pollution * 0.5f).Within(1e-4f));
            Assert.That(week.Workers, Is.EqualTo(baseline.Workers));
        }

        [Test]
        public void FactorOfNeedsDistrictLookup()
        {
            Assert.Throws<ArgumentNullException>(() => IndustryPolicy.FactorOf(Book(), null));
        }
    }
}
