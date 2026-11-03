using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;
using Cities.Simulation.Economy;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Economy
{
    public class GoodsChainTests
    {
        private static GoodsChain Steel() => new GoodsChain("staal", 2f, 100f, 50f);

        [Test]
        public void RawMaterialFlowsThroughIndustryToTheShops()
        {
            var chain = Steel();

            var week = chain.Step(new ChainInput(extracted: 40f, industryCapacity: 100f, shopDemand: 10f));

            Assert.That(week.Processed, Is.EqualTo(20f).Within(1e-4f), "2 grondstof per eenheid");
            Assert.That(week.Delivered, Is.EqualTo(10f).Within(1e-4f));
            Assert.That(week.Shortage, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(chain.ProductStock, Is.EqualTo(10f).Within(1e-4f));
            Assert.That(chain.RawStock, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(chain.Availability, Is.EqualTo(1f));
            Assert.That(chain.HasEmptyShelves, Is.False);
        }

        [Test]
        public void AShortageOfRawMaterialEmptiesTheShelvesAndCutsTheShopsDemand()
        {
            var chain = Steel();

            var week = chain.Step(new ChainInput(4f, 100f, 10f));

            Assert.That(week.Processed, Is.EqualTo(2f).Within(1e-4f));
            Assert.That(week.Delivered, Is.EqualTo(2f).Within(1e-4f));
            Assert.That(week.Shortage, Is.EqualTo(8f).Within(1e-4f));
            Assert.That(chain.Availability, Is.EqualTo(0.2f).Within(1e-4f));
            Assert.That(chain.HasEmptyShelves, Is.True);
            Assert.That(chain.EffectiveDemand(50f), Is.EqualTo(10f).Within(1e-4f));
        }

        [Test]
        public void ShortageMeansLessRevenueForACompany()
        {
            var full = new Company(10, 1f, 10f, 5f, 1f, 0f);
            var starved = new Company(10, 1f, 10f, 5f, 1f, 0f);
            var chain = Steel();
            chain.Step(new ChainInput(10f, 100f, 10f));

            var normal = full.Step(100f);
            var starvedWeek = starved.Step(chain.EffectiveDemand(100f));

            Assert.That(chain.Availability, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(starvedWeek.Revenue, Is.LessThan(normal.Revenue));
        }

        [Test]
        public void IndustryCapacityLimitsProcessingAndStockCarriesOver()
        {
            var chain = Steel();

            var first = chain.Step(new ChainInput(60f, 20f, 0f));
            var second = chain.Step(new ChainInput(0f, 20f, 5f));

            Assert.That(first.Processed, Is.EqualTo(10f).Within(1e-4f));
            Assert.That(chain.Availability, Is.EqualTo(1f), "alle vraag geleverd");
            Assert.That(second.Processed, Is.EqualTo(10f).Within(1e-4f));
            Assert.That(second.Delivered, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(chain.ProductStock, Is.EqualTo(15f).Within(1e-4f));
            Assert.That(chain.RawStock, Is.EqualTo(20f).Within(1e-4f));
        }

        [Test]
        public void StorageIsCappedAndTheSurplusIsReported()
        {
            var chain = new GoodsChain("graan", 1f, 10f, 5f);

            var week = chain.Step(new ChainInput(30f, 100f, 0f));

            Assert.That(week.Overflow, Is.EqualTo(20f).Within(1e-4f));
            Assert.That(week.Processed, Is.EqualTo(5f).Within(1e-4f), "productopslag is vol");
            Assert.That(chain.RawStock, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(chain.ProductStock, Is.EqualTo(5f).Within(1e-4f));
        }

        [Test]
        public void InvalidValuesAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new GoodsChain(" ", 1f, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GoodsChain("a", 0f, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GoodsChain("a", 1f, -1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChainInput(-1f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChainInput(0f, float.NaN, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChainInput(0f, 0f, -1f));
        }

        [Test]
        public void TheNetworkHoldsChainsByNameAndAveragesAvailability()
        {
            var network = new GoodsNetwork();
            Assert.That(network.AverageAvailability(), Is.EqualTo(1f));
            var steel = network.Add(Steel());
            network.Add(new GoodsChain("brood", 1f, 10f, 10f));
            steel.Step(new ChainInput(0f, 10f, 10f));

            Assert.That(network.Get("staal"), Is.SameAs(steel));
            Assert.That(network.Contains("brood"), Is.True);
            Assert.That(network.Contains(null), Is.False);
            Assert.That(network.Chains.Select(c => c.Name).ToArray(), Is.EqualTo(new[] { "brood", "staal" }));
            Assert.That(network.AverageAvailability(), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.Throws<InvalidOperationException>(() => network.Add(Steel()));
            Assert.Throws<ArgumentNullException>(() => network.Add(null));
            Assert.Throws<KeyNotFoundException>(() => network.Get("goud"));
            Assert.Throws<KeyNotFoundException>(() => network.Get(null));
        }

        [Test]
        public void SaveRoundTripKeepsStocksAndAvailability()
        {
            var network = new GoodsNetwork();
            network.Add(Steel()).Step(new ChainInput(30f, 100f, 40f));
            var bytes = SaveBytes.Write(new SaveMetadata(), new GoodsSection(network));
            var loaded = new GoodsNetwork();
            var chain = loaded.Add(Steel());

            SaveBytes.Read(bytes, new GoodsSection(loaded));

            Assert.That(chain.ProductStock, Is.EqualTo(network.Get("staal").ProductStock));
            Assert.That(chain.Availability, Is.EqualTo(network.Get("staal").Availability));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new GoodsSection(loaded)), Is.EqualTo(bytes));
            Assert.That(new GoodsSection(network).Id, Is.EqualTo("GOOD"));
            Assert.Throws<ArgumentNullException>(() => new GoodsSection(null));
        }

        [Test]
        public void ACorruptSaveIsRejectedAndChangesNothing()
        {
            var network = new GoodsNetwork();
            var chain = network.Add(Steel());
            chain.Step(new ChainInput(10f, 10f, 1f));
            var before = chain.ProductStock;
            var section = new GoodsSection(network);

            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => w.Write(-1)), 1), "negatief aantal");
            Assert.Throws<SaveFormatException>(() => section.Read(State(1, "onbekend", 0f, 0f, 1f), 1), "onbekend goed");
            Assert.Throws<SaveFormatException>(() => section.Read(State(1, "staal", -1f, 0f, 1f), 1), "negatieve voorraad");
            Assert.Throws<SaveFormatException>(() => section.Read(State(1, "staal", 0f, 0f, 2f), 1), "beschikbaarheid boven 1");
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w =>
            {
                w.Write(2);
                for (var i = 0; i < 2; i++)
                {
                    w.Write("staal");
                    w.Write(0f);
                    w.Write(0f);
                    w.Write(1f);
                }
            }), 1), "dubbel");
            Assert.That(chain.ProductStock, Is.EqualTo(before));
        }

        private static SaveReader State(int count, string name, float raw, float product, float availability) => SaveBytes.ReaderFor(w =>
        {
            w.Write(count);
            w.Write(name);
            w.Write(raw);
            w.Write(product);
            w.Write(availability);
        });
    }
}
