using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;
using Cities.Simulation.Economy;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Economy
{
    public class TradePreferencesTests
    {
        private static TradePreferences Market(ulong seed = 7)
        {
            var preferences = new TradePreferences(seed);
            preferences.Define("staal", 20, 10, ConnectionKind.Rail);
            preferences.Define("graan", 12, 6, ConnectionKind.Road);
            return preferences;
        }

        private static FixedTradeConnections Roads(params int[] open)
        {
            var connections = new FixedTradeConnections();
            foreach (var id in open)
            {
                connections.Set(id, ConnectionKind.Road, true);
            }

            connections.Set(100, ConnectionKind.Rail, true);
            return connections;
        }

        [Test]
        public void PricesAndDemandFluctuateWithinBounds()
        {
            var preferences = Market();
            var connections = Roads(1);
            var prices = new HashSet<float>();
            for (var week = 0; week < 200; week++)
            {
                preferences.Step(week, connections);
                prices.Add(preferences.PriceFactor("staal"));
                Assert.That(preferences.PriceFactor("staal"), Is.InRange(TradePreferences.MinPrice, TradePreferences.MaxPrice));
                Assert.That(preferences.DemandFactor("graan"), Is.InRange(TradePreferences.MinDemand, TradePreferences.MaxDemand));
            }

            Assert.That(prices.Count, Is.GreaterThan(50));
        }

        [Test]
        public void SameSeedGivesSameMarketAndOtherSeedDiffers()
        {
            var a = Market(1);
            var b = Market(1);
            var c = Market(2);
            var connections = Roads(1);
            for (var week = 0; week < 20; week++)
            {
                a.Step(week, connections);
                b.Step(week, connections);
                c.Step(week, connections);
            }

            Assert.That(b.PriceFactor("staal"), Is.EqualTo(a.PriceFactor("staal")));
            Assert.That(b.DemandFactor("graan"), Is.EqualTo(a.DemandFactor("graan")));
            Assert.That(c.PriceFactor("staal"), Is.Not.EqualTo(a.PriceFactor("staal")));
        }

        [Test]
        public void ClosingAConnectionIsASetbackThatFadesAway()
        {
            var preferences = Market();
            var connections = Roads(1, 2);
            preferences.Step(0, connections);
            var before = preferences.ExternalDemand("graan");
            Assert.That(preferences.Setback(ConnectionKind.Road), Is.EqualTo(0f));

            connections.Set(2, ConnectionKind.Road, false);
            preferences.Step(1, connections);

            Assert.That(preferences.Setback(ConnectionKind.Road), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(preferences.Setback(ConnectionKind.Rail), Is.EqualTo(0f));
            Assert.That(preferences.ExternalDemand("graan"), Is.LessThan(before));
            Assert.That(preferences.ImportPrice("graan"), Is.GreaterThan((long)Math.Round(12 * preferences.PriceFactor("graan"))));
            Assert.That(preferences.ExportPrice("graan"), Is.LessThan((long)Math.Round(6 * preferences.PriceFactor("graan")) + 1));

            for (var week = 2; week < 60; week++)
            {
                preferences.Step(week, connections);
            }

            Assert.That(preferences.Setback(ConnectionKind.Road), Is.EqualTo(0f));
        }

        [Test]
        public void ARemovedConnectionCountsAndAReopenedOneDoesNot()
        {
            var preferences = Market();
            var connections = Roads(1);
            preferences.Step(0, connections);

            connections.Remove(1);
            preferences.Step(1, connections);
            Assert.That(preferences.Setback(ConnectionKind.Road), Is.EqualTo(1f).Within(1e-4f));

            connections.Set(1, ConnectionKind.Road, true);
            preferences.Step(2, connections);
            Assert.That(preferences.Setback(ConnectionKind.Road), Is.LessThan(1f));
        }

        [Test]
        public void TradeableLimitsOnlySurplus()
        {
            var preferences = Market();
            var connections = Roads(1, 2);
            preferences.Step(0, connections);
            connections.Set(1, ConnectionKind.Road, false);
            connections.Set(2, ConnectionKind.Road, false);
            preferences.Step(1, connections);

            Assert.That(preferences.Tradeable("graan", 100f), Is.LessThan(100f));
            Assert.That(preferences.Tradeable("graan", -40f), Is.EqualTo(-40f));
            Assert.That(preferences.Tradeable("graan", 0f), Is.EqualTo(0f));
        }

        [Test]
        public void ApplyWritesPricesIntoTrade()
        {
            var preferences = Market();
            preferences.Step(0, Roads(1));
            var trade = new Trade();

            preferences.Apply(trade);

            Assert.That(trade.ExportPrice("staal"), Is.EqualTo(preferences.ExportPrice("staal")));
            Assert.That(trade.ImportPrice("graan"), Is.EqualTo(preferences.ImportPrice("graan")));
            Assert.That(trade.ImportPrice("staal"), Is.GreaterThanOrEqualTo(trade.ExportPrice("staal")));
            Assert.That(preferences.Goods.ToArray(), Is.EqualTo(new[] { "graan", "staal" }));
        }

        [Test]
        public void InvalidInputIsRejected()
        {
            var preferences = Market();
            Assert.Throws<ArgumentException>(() => preferences.Define(" ", 2, 1, ConnectionKind.Road));
            Assert.Throws<ArgumentOutOfRangeException>(() => preferences.Define("x", 1, 2, ConnectionKind.Road));
            Assert.Throws<ArgumentOutOfRangeException>(() => preferences.Define("x", 1, -1, ConnectionKind.Road));
            Assert.Throws<ArgumentException>(() => preferences.PriceFactor("onbekend"));
            Assert.Throws<ArgumentException>(() => preferences.PriceFactor(null));
            Assert.Throws<ArgumentNullException>(() => preferences.Step(0, null));
            Assert.Throws<ArgumentNullException>(() => preferences.Apply(null));
            Assert.Throws<ArgumentNullException>(() => new TradePreferencesSection(null));
        }

        [Test]
        public void SaveRoundTripKeepsMarketAndSetback()
        {
            var preferences = Market(99);
            var connections = Roads(1, 2);
            for (var week = 0; week < 5; week++)
            {
                preferences.Step(week, connections);
            }

            connections.Set(2, ConnectionKind.Road, false);
            preferences.Step(5, connections);
            var bytes = SaveBytes.Write(new SaveMetadata(), new TradePreferencesSection(preferences));
            var loaded = new TradePreferences(0);

            SaveBytes.Read(bytes, new TradePreferencesSection(loaded));

            Assert.That(loaded.Seed, Is.EqualTo(99UL));
            Assert.That(loaded.PriceFactor("staal"), Is.EqualTo(preferences.PriceFactor("staal")));
            Assert.That(loaded.Setback(ConnectionKind.Road), Is.EqualTo(preferences.Setback(ConnectionKind.Road)));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new TradePreferencesSection(loaded)), Is.EqualTo(bytes));
            Assert.That(new TradePreferencesSection(loaded).Id, Is.EqualTo("TRDP"));

            preferences.Step(6, connections);
            loaded.Step(6, connections);
            Assert.That(loaded.DemandFactor("graan"), Is.EqualTo(preferences.DemandFactor("graan")));
        }

        [Test]
        public void ACorruptSaveIsRejectedAndChangesNothing()
        {
            var preferences = Market();
            var section = new TradePreferencesSection(preferences);

            Assert.Throws<SaveFormatException>(() => section.Read(Save(w => w.Write(-1)), 1), "negatief aantal");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(w => { w.Write(1); Good(w, "", 1f, 1f); }), 1), "naamloos");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(w => { w.Write(1); Good(w, "x", 9f, 1f); }), 1), "prijsfactor");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(w => { w.Write(1); Good(w, "x", 1f, 0f); }), 1), "vraagfactor");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(w => { w.Write(2); Good(w, "x", 1f, 1f); Good(w, "x", 1f, 1f); }), 1), "dubbel goed");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(w => { w.Write(0); w.Write(1); w.Write(5); w.Write(9); }), 1), "soort");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(w => { w.Write(0); w.Write(2); w.Write(5); w.Write(0); w.Write(5); w.Write(0); }), 1), "dubbele verbinding");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(w => { w.Write(0); w.Write(0); w.Write(2f); }), 1), "tegenvaller");
            Assert.That(preferences.Goods.ToArray(), Is.EqualTo(new[] { "graan", "staal" }));
        }

        private static SaveReader Save(Action<SaveWriter> body) => SaveBytes.ReaderFor(w =>
        {
            w.Write(5L);
            body(w);
        });

        private static void Good(SaveWriter w, string name, float price, float demand)
        {
            w.Write(name);
            w.Write(2L);
            w.Write(1L);
            w.Write(0);
            w.Write(price);
            w.Write(demand);
        }
    }
}
