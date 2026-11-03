using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;
using Cities.Simulation.Economy;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Economy
{
    public class OtherIncomeTests
    {
        [Test]
        public void TouristsSpendAndPayTicketsAndTheUpkeepIsBooked()
        {
            var tourism = new Tourism();
            tourism.Add("Kasteel", 10, 5, 100, true);
            tourism.Add("Pretpark", 20, 10, 50, false);
            var ledger = new Ledger(0);

            var week = tourism.Settle(0, ledger, 1000);

            Assert.That(week.Tourists, Is.EqualTo(210));
            Assert.That(week.Spending, Is.EqualTo(210 * Tourism.SpendPerTourist));
            Assert.That(week.Tickets, Is.EqualTo(70 * 5 + 140 * 10));
            Assert.That(week.Upkeep, Is.EqualTo(150));
            Assert.That(ledger.Income(0, LedgerCategory.Other), Is.EqualTo(week.Spending + week.Tickets));
            Assert.That(ledger.Expenses(0, LedgerCategory.Upkeep), Is.EqualTo(150));
        }

        [Test]
        public void TooFewBedsShrinkEveryAttractionEqually()
        {
            var tourism = new Tourism();
            tourism.Add("A", 10, 1, 0, false);
            tourism.Add("B", 30, 1, 0, false);

            // 280 gewenst, 20 bedden x 2 verblijven = 40 mogelijk.
            var week = tourism.Settle(0, new Ledger(0), 20);

            Assert.That(week.Tourists, Is.EqualTo(40));
            Assert.That(week.Tickets, Is.EqualTo(10 + 30));
            Assert.That(tourism.Settle(0, new Ledger(0), 0).Tourists, Is.Zero);
            Assert.That(tourism.Settle(0, new Ledger(0), -5).Tourists, Is.Zero);
        }

        [Test]
        public void AUniqueAttractionCanStandOnlyOnce()
        {
            var tourism = new Tourism();
            var id = tourism.Add("Eiffeltoren", 5, 0, 0, true);

            Assert.Throws<InvalidOperationException>(() => tourism.Add("Eiffeltoren", 5, 0, 0, true));
            tourism.Add("Hotel", 1, 0, 0, false);
            tourism.Add("Hotel", 1, 0, 0, false);
            Assert.That(tourism.Remove(id), Is.True);
            Assert.That(tourism.Remove(id), Is.False);
            Assert.DoesNotThrow(() => tourism.Add("Eiffeltoren", 5, 0, 0, true));
        }

        [Test]
        public void InvalidAttractionsAndMissingLedgerAreRefused()
        {
            var tourism = new Tourism();

            Assert.Throws<ArgumentException>(() => tourism.Add(" ", 1, 0, 0, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => tourism.Add("X", -1, 0, 0, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => tourism.Add("X", 1, -1, 0, false));
            Assert.Throws<ArgumentNullException>(() => tourism.Settle(0, null, 1));
            Assert.That(tourism.Attractions, Is.Empty);
        }

        [Test]
        public void SurplusIsExportedAndShortageIsImported()
        {
            var trade = new Trade();
            trade.SetPrices("staal", 12, 8);
            trade.SetPrices("graan", 5, 3);
            var ledger = new Ledger(100);

            var week = trade.Settle(0, ledger, new Dictionary<string, float> { ["staal"] = 10f, ["graan"] = -4f });

            Assert.That(week.Exports, Is.EqualTo(80));
            Assert.That(week.Imports, Is.EqualTo(20));
            Assert.That(week.Balance, Is.EqualTo(60));
            Assert.That(ledger.Balance, Is.EqualTo(160));
            Assert.That(ledger.Income(0, LedgerCategory.Trade), Is.EqualTo(80));
            Assert.That(ledger.Expenses(0, LedgerCategory.Trade), Is.EqualTo(20));
            Assert.That(trade.ImportPrice("staal"), Is.EqualTo(12));
            Assert.That(trade.ExportPrice("staal"), Is.EqualTo(8));
            Assert.That(trade.Goods.ToArray(), Is.EqualTo(new[] { "graan", "staal" }));
        }

        [Test]
        public void ImportsAreMandatoryEvenWhenTheBalanceGoesNegative()
        {
            var trade = new Trade();
            trade.SetPrices("olie", 100, 50);
            var ledger = new Ledger(0);

            trade.Settle(0, ledger, new Dictionary<string, float> { ["olie"] = -2f });

            Assert.That(ledger.Balance, Is.EqualTo(-200));
        }

        [Test]
        public void BadPricesAndUnknownGoodsAreRefused()
        {
            var trade = new Trade();

            Assert.Throws<ArgumentException>(() => trade.SetPrices("", 2, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => trade.SetPrices("a", 1, 2), "import goedkoper dan export");
            Assert.Throws<ArgumentOutOfRangeException>(() => trade.SetPrices("a", 1, -1));
            Assert.Throws<ArgumentException>(() => trade.ImportPrice("a"));
            Assert.Throws<ArgumentException>(() => trade.Settle(0, new Ledger(0), new Dictionary<string, float> { ["a"] = 1f }));
            Assert.Throws<ArgumentNullException>(() => trade.Settle(0, null, new Dictionary<string, float>()));
            Assert.Throws<ArgumentNullException>(() => trade.Settle(0, new Ledger(0), null));
        }

        [Test]
        public void SaveRoundTripKeepsAttractionsPricesAndIds()
        {
            var tourism = new Tourism();
            tourism.Add("Kasteel", 10, 5, 100, true);
            tourism.Remove(tourism.Add("Tijdelijk", 1, 1, 1, false));
            var trade = new Trade();
            trade.SetPrices("staal", 12, 8);
            var bytes = SaveBytes.Write(new SaveMetadata(), new OtherIncomeSection(tourism, trade));
            var loadedTourism = new Tourism();
            loadedTourism.Add("Weg", 1, 1, 1, false);
            var loadedTrade = new Trade();

            SaveBytes.Read(bytes, new OtherIncomeSection(loadedTourism, loadedTrade));

            Assert.That(loadedTourism.Attractions.Select(a => a.Name).ToArray(), Is.EqualTo(new[] { "Kasteel" }));
            Assert.That(loadedTourism.NextId, Is.EqualTo(tourism.NextId));
            Assert.That(loadedTrade.ImportPrice("staal"), Is.EqualTo(12));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new OtherIncomeSection(loadedTourism, loadedTrade)), Is.EqualTo(bytes));
            Assert.That(new OtherIncomeSection(tourism, trade).Id, Is.EqualTo("INCM"));
            Assert.Throws<ArgumentNullException>(() => new OtherIncomeSection(null, trade));
            Assert.Throws<ArgumentNullException>(() => new OtherIncomeSection(tourism, null));
        }

        [Test]
        public void ACorruptSaveIsRejectedAndChangesNothing()
        {
            var tourism = new Tourism();
            tourism.Add("Blijft", 1, 1, 1, false);
            var section = new OtherIncomeSection(tourism, new Trade());

            Assert.Throws<SaveFormatException>(() => section.Read(Save(2, w => w.Write(-1)), 1), "negatief aantal");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(1, w => { w.Write(1); Attraction(w, 1, "A", false); w.Write(0); }), 1), "volgnummer te laag");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(5, w => { w.Write(2); Attraction(w, 1, "A", false); Attraction(w, 1, "B", false); w.Write(0); }), 1), "dubbel ID");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(5, w => { w.Write(2); Attraction(w, 1, "A", true); Attraction(w, 2, "A", true); w.Write(0); }), 1), "dubbel uniek");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(5, w => { w.Write(1); Attraction(w, 1, "", false); w.Write(0); }), 1), "naamloos");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(5, w => { w.Write(0); w.Write(1); w.Write("x"); w.Write(1L); w.Write(2L); }), 1), "import goedkoper dan export");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(5, w => { w.Write(0); w.Write(2); Price(w, "x"); Price(w, "x"); }), 1), "dubbel goed");
            Assert.That(tourism.Attractions.Single().Name, Is.EqualTo("Blijft"));
        }

        private static SaveReader Save(int nextId, Action<SaveWriter> body) => SaveBytes.ReaderFor(w =>
        {
            w.Write(nextId);
            body(w);
        });

        private static void Attraction(SaveWriter w, int id, string name, bool unique)
        {
            w.Write(id);
            w.Write(name);
            w.Write(1);
            w.Write(1L);
            w.Write(1L);
            w.Write(unique);
        }

        private static void Price(SaveWriter w, string good)
        {
            w.Write(good);
            w.Write(2L);
            w.Write(1L);
        }
    }
}
