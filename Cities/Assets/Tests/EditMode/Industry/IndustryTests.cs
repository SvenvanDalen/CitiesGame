using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;
using Cities.Simulation.Economy;
using Cities.Simulation.Industry;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Industry
{
    public class IndustryTests
    {
        [Test]
        public void EveryKindHasItsOwnGoodAndBuildings()
        {
            var kinds = new[] { IndustryKind.Agriculture, IndustryKind.Forestry, IndustryKind.Ore, IndustryKind.Oil };
            var profiles = kinds.Select(IndustryProfile.For).ToArray();

            Assert.That(profiles.Select(p => p.Good).Distinct().Count(), Is.EqualTo(4));
            Assert.That(profiles.Select(p => p.ExtractionBuilding).Distinct().Count(), Is.EqualTo(4));
            Assert.That(profiles.Select(p => p.ProcessingBuilding).Distinct().Count(), Is.EqualTo(4));
            Assert.That(profiles.Select(p => p.Renewable), Is.EqualTo(new[] { true, true, false, false }));
            Assert.That(profiles.All(p => p.WorkersPerLevel > 0f && p.PollutionPerUnit > 0f && p.BaseOutput > 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => IndustryProfile.For((IndustryKind)9));
        }

        [Test]
        public void SiteValidatesItsArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExtractionSite(1, IndustryKind.Ore, 0, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExtractionSite(1, IndustryKind.Ore, 4, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExtractionSite(1, IndustryKind.Ore, 1, 1.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExtractionSite(1, IndustryKind.Ore, 1, float.NaN));
        }

        [Test]
        public void FarmFollowsTheSeasonsAndWearsTheSoil()
        {
            Assert.That(ExtractionSite.SeasonFactor(0), Is.EqualTo(0.7f));
            Assert.That(ExtractionSite.SeasonFactor(14), Is.EqualTo(1.2f));
            Assert.That(ExtractionSite.SeasonFactor(30), Is.EqualTo(1.4f));
            Assert.That(ExtractionSite.SeasonFactor(45), Is.EqualTo(0.2f));
            Assert.That(ExtractionSite.SeasonFactor(52 + 14), Is.EqualTo(1.2f), "het jaar herhaalt");
            Assert.That(ExtractionSite.SeasonFactor(-7), Is.EqualTo(0.2f), "negatieve weken tellen terug");

            var farm = new ExtractionSite(1, IndustryKind.Agriculture, 2, 1f);
            var summer = farm.Step(14);
            var fertilityAfterSummer = farm.Stock;
            var winter = farm.Step(45);

            Assert.That(summer.Output, Is.GreaterThan(winter.Output * 4f));
            Assert.That(fertilityAfterSummer, Is.LessThan(1f));
            Assert.That(farm.Stock, Is.GreaterThan(fertilityAfterSummer), "de winter herstelt de grond");
        }

        [Test]
        public void FarmFertilityNeverDropsBelowTheFloor()
        {
            var farm = new ExtractionSite(1, IndustryKind.Agriculture, 1, 0.5f);
            for (var i = 0; i < 500; i++)
            {
                farm.Step(14);
            }

            Assert.That(farm.Stock, Is.EqualTo(0.2f).Within(1e-4f));
            Assert.That(farm.Step(14).Output, Is.GreaterThan(0f));
        }

        [Test]
        public void ForestRegrowsAfterHarvestAndSurvivesClearCutting()
        {
            var forest = new ExtractionSite(1, IndustryKind.Forestry, 3, 1f);
            var first = forest.Step(0);
            Assert.That(first.Output, Is.EqualTo(36f).Within(1e-3f));

            var cut = new ExtractionSite(2, IndustryKind.Forestry, 3, 0.01f);
            for (var i = 0; i < 20; i++)
            {
                cut.Step(i);
            }

            Assert.That(cut.Stock, Is.GreaterThanOrEqualTo(0f));
            Assert.That(cut.Stock, Is.LessThanOrEqualTo(cut.Capacity));

            var bare = new ExtractionSite(3, IndustryKind.Forestry, 1, 0f);
            Assert.That(bare.Step(0).Output, Is.EqualTo(0f));
        }

        [Test]
        public void ForestStaysSustainableAtLowLevel()
        {
            var forest = new ExtractionSite(1, IndustryKind.Forestry, 1, 1f);
            var total = 0f;
            for (var i = 0; i < 200; i++)
            {
                total += forest.Step(i).Output;
            }

            Assert.That(forest.Stock, Is.GreaterThan(0f));
            Assert.That(total, Is.GreaterThan(1500f), "meer dan de beginvoorraad: er groeit hout bij");
        }

        [Test]
        public void OreRunsOutAndStaysEmpty()
        {
            var mine = new ExtractionSite(1, IndustryKind.Ore, 3, 0.01f);
            var total = 0f;
            for (var i = 0; i < 100 && !mine.Depleted; i++)
            {
                total += mine.Step(i).Output;
            }

            Assert.That(mine.Depleted, Is.True);
            Assert.That(total, Is.EqualTo(mine.Capacity).Within(1e-2f), "niet meer winnen dan de voorraad");
            Assert.That(mine.Step(0).Output, Is.EqualTo(0f));
            Assert.That(mine.Workers, Is.EqualTo(45f));
        }

        [Test]
        public void OilPressureDeclinesWithTheReserve()
        {
            var well = new ExtractionSite(1, IndustryKind.Oil, 1, 0.5f);
            var early = well.Step(0).Output;
            for (var i = 0; i < 200; i++)
            {
                well.Step(i);
            }

            var late = well.Step(0).Output;
            Assert.That(late, Is.LessThan(early));
            Assert.That(well.Step(0).Pollution, Is.GreaterThanOrEqualTo(0f));
        }

        [Test]
        public void NetworkSumsOutputPerGoodDeterministically()
        {
            var network = new IndustryNetwork();
            network.Add(new ExtractionSite(5, IndustryKind.Ore, 1, 1f));
            network.Add(new ExtractionSite(2, IndustryKind.Ore, 2, 0.5f));
            network.Add(new ExtractionSite(9, IndustryKind.Oil, 1, 1f));

            var week = network.Step(0);

            Assert.That(week.Of("erts"), Is.EqualTo((50f * 1.5f) + (100f * 1f)).Within(1e-3f));
            Assert.That(week.Of("olie"), Is.EqualTo(90f).Within(1e-3f));
            Assert.That(week.Of("hout"), Is.EqualTo(0f));
            Assert.That(week.Of(null), Is.EqualTo(0f));
            Assert.That(week.Extracted.Keys, Is.EqualTo(new[] { "erts", "olie" }));
            Assert.That(week.Workers, Is.EqualTo(15f + 30f + 10f));
            Assert.That(week.Pollution, Is.GreaterThan(0f));
            Assert.That(network.Sites.Select(s => s.Id), Is.EqualTo(new[] { 2, 5, 9 }));
            Assert.That(network.CountOf(IndustryKind.Ore), Is.EqualTo(2));
        }

        [Test]
        public void NetworkRejectsBadSitesAndRemoves()
        {
            var network = new IndustryNetwork();
            network.Add(new ExtractionSite(1, IndustryKind.Ore, 1, 1f));

            Assert.Throws<ArgumentNullException>(() => network.Add(null));
            Assert.Throws<InvalidOperationException>(() => network.Add(new ExtractionSite(1, IndustryKind.Oil, 1, 1f)));
            Assert.Throws<KeyNotFoundException>(() => network.Get(7));
            Assert.That(network.Get(1).Kind, Is.EqualTo(IndustryKind.Ore));
            Assert.That(network.Contains(1), Is.True);
            Assert.That(network.Remove(1), Is.True);
            Assert.That(network.Remove(1), Is.False);
            Assert.That(network.Count, Is.EqualTo(0));
        }

        [Test]
        public void DepletedMineStopsEmployingWorkers()
        {
            var network = new IndustryNetwork();
            network.Add(new ExtractionSite(1, IndustryKind.Ore, 3, 0.001f));
            for (var i = 0; i < 5; i++)
            {
                network.Step(i);
            }

            Assert.That(network.Step(6).Workers, Is.EqualTo(0f));
        }

        [Test]
        public void ExtractionFeedsTheGoodsChain()
        {
            var network = new IndustryNetwork();
            network.Add(new ExtractionSite(1, IndustryKind.Ore, 2, 1f));
            var chain = new GoodsChain("erts", 1f, 1000f, 1000f);

            var extracted = network.Step(0).Of("erts");
            var week = chain.Step(new ChainInput(extracted, 1000f, 50f));

            Assert.That(extracted, Is.EqualTo(150f).Within(1e-3f));
            Assert.That(week.Delivered, Is.EqualTo(50f).Within(1e-3f));
            Assert.That(chain.Availability, Is.EqualTo(1f));
        }

        [Test]
        public void SaveAndLoadKeepsTheStock()
        {
            var network = new IndustryNetwork();
            network.Add(new ExtractionSite(3, IndustryKind.Oil, 2, 0.7f));
            network.Add(new ExtractionSite(1, IndustryKind.Agriculture, 1, 0.4f));
            for (var i = 0; i < 10; i++)
            {
                network.Step(i);
            }

            var bytes = SaveBytes.Write(new SaveMetadata(), new IndustrySection(network));
            var loaded = new IndustryNetwork();
            loaded.Add(new ExtractionSite(99, IndustryKind.Ore, 1, 1f));
            SaveBytes.Read(bytes, new IndustrySection(loaded));

            Assert.That(loaded.Count, Is.EqualTo(2));
            Assert.That(loaded.Get(3).Stock, Is.EqualTo(network.Get(3).Stock).Within(1e-3f));
            Assert.That(loaded.Get(1).Level, Is.EqualTo(1));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new IndustrySection(loaded)), Is.EqualTo(bytes));
            Assert.That(loaded.Step(10).Of("olie"), Is.EqualTo(network.Step(10).Of("olie")).Within(1e-3f));
            Assert.That(new IndustrySection(network).Id, Is.EqualTo("INDU"));
            Assert.Throws<ArgumentNullException>(() => new IndustrySection(null));
        }

        [Test]
        public void CorruptSaveIsRejected()
        {
            var section = new IndustrySection(new IndustryNetwork());

            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => w.Write(-1)), 1), "negatief aantal");
            Assert.Throws<SaveFormatException>(() => section.Read(Site(9, 2, 1, 0.5f, 1f), 1), "onbekend type");
            Assert.Throws<SaveFormatException>(() => section.Read(Site(0, 2, 0, 0.5f, 1f), 1), "niveau");
            Assert.Throws<SaveFormatException>(() => section.Read(Site(0, 2, 1, 2f, 1f), 1), "rijkdom");
            Assert.Throws<SaveFormatException>(() => section.Read(Site(0, 2, 1, 0.5f, -1f), 1), "voorraad");
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w =>
            {
                w.Write(2);
                for (var i = 0; i < 2; i++)
                {
                    w.Write(1);
                    w.Write(2);
                    w.Write(1);
                    w.Write(0.5f);
                    w.Write(1f);
                }
            }), 1), "dubbel id");
        }

        private static SaveReader Site(int kind, int id, int level, float richness, float stock) => SaveBytes.ReaderFor(w =>
        {
            w.Write(1);
            w.Write(id);
            w.Write(kind);
            w.Write(level);
            w.Write(richness);
            w.Write(stock);
        });
    }
}
