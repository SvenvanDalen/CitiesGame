using System;
using Cities.Core.Persistence;
using Cities.Simulation.Economy;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Economy
{
    public class ResourceTradeTests
    {
        private static ResourceTrade Market()
        {
            var trade = new ResourceTrade();
            trade.SetOffer(TradeResource.Goods, 12, 8, 100);
            trade.SetOffer(TradeResource.Power, 3, 2, 1000);
            trade.SetOffer(TradeResource.Water, 2, 1, 500);
            trade.SetOffer(TradeResource.Waste, 0, 5, 50);
            return trade;
        }

        [Test]
        public void BuyingBooksTheUnitPriceAsAnExpense()
        {
            var ledger = new Ledger(1000);

            var deal = Market().Buy(0, ledger, TradeResource.Goods, 10);

            Assert.That(deal.Units, Is.EqualTo(10));
            Assert.That(deal.Amount, Is.EqualTo(120));
            Assert.That(ledger.Balance, Is.EqualTo(880));
            Assert.That(ledger.Expenses(0, LedgerCategory.Trade), Is.EqualTo(120));
        }

        [Test]
        public void SellingBooksTheUnitPriceAsIncome()
        {
            var ledger = new Ledger(0);

            var deal = Market().Sell(0, ledger, TradeResource.Power, 400);

            Assert.That(deal.Amount, Is.EqualTo(800));
            Assert.That(ledger.Income(0, LedgerCategory.Trade), Is.EqualTo(800));
            Assert.That(ledger.Balance, Is.EqualTo(800));
        }

        [Test]
        public void WaterCanBeBoughtAndSold()
        {
            var trade = Market();
            var ledger = new Ledger(100);

            trade.Buy(0, ledger, TradeResource.Water, 20);
            trade.Sell(0, ledger, TradeResource.Water, 10);

            Assert.That(ledger.Balance, Is.EqualTo(100 - 40 + 10));
        }

        [Test]
        public void WasteIsDisposedAgainstAFeeAndCannotBeBought()
        {
            var trade = Market();
            var ledger = new Ledger(100);

            var deal = trade.Sell(0, ledger, TradeResource.Waste, 10);

            Assert.That(deal.Amount, Is.EqualTo(50));
            Assert.That(ledger.Balance, Is.EqualTo(50));
            Assert.That(ledger.Expenses(0, LedgerCategory.Trade), Is.EqualTo(50));
            Assert.That(trade.Buy(0, ledger, TradeResource.Waste, 5).Units, Is.Zero);
            Assert.That(ResourceTrade.CanBuy(TradeResource.Waste), Is.False);
            Assert.That(trade.BuyRoom(0, TradeResource.Waste), Is.Zero);
        }

        [Test]
        public void WasteDisposalIsLimitedByTheBalance()
        {
            var ledger = new Ledger(120);

            var deal = Market().Sell(0, ledger, TradeResource.Waste, 40);

            Assert.That(deal.Units, Is.EqualTo(24));
            Assert.That(ledger.Balance, Is.EqualTo(0));
        }

        [Test]
        public void TheWeeklyCapacityLimitsTradeAndResetsNextWeek()
        {
            var trade = Market();
            var ledger = new Ledger(100000);

            Assert.That(trade.Buy(0, ledger, TradeResource.Goods, 70).Units, Is.EqualTo(70));
            Assert.That(trade.Buy(0, ledger, TradeResource.Goods, 70).Units, Is.EqualTo(30));
            Assert.That(trade.Buy(0, ledger, TradeResource.Goods, 1).Units, Is.Zero);
            Assert.That(trade.BoughtThisWeek(TradeResource.Goods), Is.EqualTo(100));
            Assert.That(trade.Sell(0, ledger, TradeResource.Goods, 500).Units, Is.EqualTo(100));
            Assert.That(trade.SoldThisWeek(TradeResource.Goods), Is.EqualTo(100));
            Assert.That(trade.SellRoom(0, TradeResource.Goods), Is.Zero);

            Assert.That(trade.BuyRoom(1, TradeResource.Goods), Is.EqualTo(100));
            Assert.That(trade.SellRoom(1, TradeResource.Goods), Is.EqualTo(100));
            Assert.That(trade.CurrentWeek, Is.EqualTo(1));
        }

        [Test]
        public void WithoutAConnectionNothingIsTraded()
        {
            var trade = new ResourceTrade();
            var ledger = new Ledger(100);

            Assert.That(trade.Buy(0, ledger, TradeResource.Power, 5).Units, Is.Zero);
            Assert.That(trade.Sell(0, ledger, TradeResource.Power, 5).Units, Is.Zero);
            Assert.That(ledger.Balance, Is.EqualTo(100));
        }

        [Test]
        public void BuyingIsLimitedByTheBalance()
        {
            var ledger = new Ledger(50);

            var deal = Market().Buy(0, ledger, TradeResource.Goods, 10);

            Assert.That(deal.Units, Is.EqualTo(4));
            Assert.That(ledger.Balance, Is.EqualTo(2));
            Assert.That(Market().Buy(0, new Ledger(-5), TradeResource.Goods, 1).Units, Is.Zero);
        }

        [Test]
        public void SandboxMoneyIsNeverTheLimit()
        {
            var ledger = new Ledger(0) { UnlimitedMoney = true };

            Assert.That(Market().Buy(0, ledger, TradeResource.Goods, 10).Units, Is.EqualTo(10));
        }

        [Test]
        public void SettleSellsASurplusAndBuysAShortage()
        {
            var trade = Market();
            var ledger = new Ledger(1000);

            Assert.That(trade.Settle(0, ledger, TradeResource.Power, 100).Amount, Is.EqualTo(200));
            Assert.That(trade.Settle(0, ledger, TradeResource.Power, -100).Amount, Is.EqualTo(300));
            Assert.That(trade.Settle(0, ledger, TradeResource.Water, 0).Units, Is.Zero);
            Assert.That(ledger.Balance, Is.EqualTo(900));
        }

        [Test]
        public void FreeResourcesAreTradedWithoutAPrice()
        {
            var trade = new ResourceTrade();
            trade.SetOffer(TradeResource.Water, 0, 0, 10);

            Assert.That(trade.Buy(0, new Ledger(0), TradeResource.Water, 4).Units, Is.EqualTo(4));
        }

        [Test]
        public void InvalidOffersAndArgumentsAreRejected()
        {
            var trade = Market();
            var ledger = new Ledger(10);

            Assert.Throws<ArgumentOutOfRangeException>(() => trade.SetOffer(TradeResource.Goods, 5, 8, 10), "kopen goedkoper dan verkopen");
            Assert.Throws<ArgumentOutOfRangeException>(() => trade.SetOffer(TradeResource.Goods, 5, -1, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => trade.SetOffer(TradeResource.Goods, 5, 1, -10));
            Assert.Throws<ArgumentOutOfRangeException>(() => trade.SetOffer(TradeResource.Waste, 3, 1, 10), "afval inkopen");
            Assert.Throws<ArgumentOutOfRangeException>(() => trade.SetOffer((TradeResource)9, 3, 1, 10));
            Assert.Throws<ArgumentNullException>(() => trade.Buy(0, null, TradeResource.Goods, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => trade.Sell(0, ledger, TradeResource.Goods, -1));
            Assert.That(trade.Offer(TradeResource.Goods).BuyPrice, Is.EqualTo(12));
        }

        [Test]
        public void SaveRoundTripKeepsOffersAndWeekUsage()
        {
            var trade = Market();
            trade.Buy(3, new Ledger(1000), TradeResource.Goods, 10);
            trade.Sell(3, new Ledger(1000), TradeResource.Waste, 4);
            var bytes = SaveBytes.Write(new SaveMetadata(), new ResourceTradeSection(trade));
            var loaded = new ResourceTrade();

            SaveBytes.Read(bytes, new ResourceTradeSection(loaded));

            Assert.That(loaded.CurrentWeek, Is.EqualTo(3));
            Assert.That(loaded.Offer(TradeResource.Waste).SellPrice, Is.EqualTo(5));
            Assert.That(loaded.BoughtThisWeek(TradeResource.Goods), Is.EqualTo(10));
            Assert.That(loaded.SoldThisWeek(TradeResource.Waste), Is.EqualTo(4));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new ResourceTradeSection(loaded)), Is.EqualTo(bytes));
            Assert.That(new ResourceTradeSection(trade).Id, Is.EqualTo("XTRD"));
            Assert.Throws<ArgumentNullException>(() => new ResourceTradeSection(null));
        }

        [Test]
        public void ACorruptSaveIsRejectedAndChangesNothing()
        {
            var trade = Market();
            var section = new ResourceTradeSection(trade);

            Assert.Throws<SaveFormatException>(() => section.Read(Save(0, 3, w => { }), 1), "verkeerd aantal");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(0, 4, w => Row(w, 1, 5, 10, 0, 0, 4)), 1), "kopen goedkoper dan verkopen");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(0, 4, w => Row(w, 5, 1, 10, 11, 0, 4)), 1), "meer gebruikt dan capaciteit");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(0, 4, w => Row(w, 5, 1, 10, 0, -1, 4)), 1), "negatief gebruik");
            Assert.That(trade.Offer(TradeResource.Goods).BuyPrice, Is.EqualTo(12));
        }

        private static void Row(SaveWriter w, long buy, long sell, long capacity, long bought, long sold, int rows)
        {
            for (var i = 0; i < rows; i++)
            {
                w.Write(buy);
                w.Write(sell);
                w.Write(capacity);
                w.Write(bought);
                w.Write(sold);
            }
        }

        private static SaveReader Save(int week, int count, Action<SaveWriter> body) => SaveBytes.ReaderFor(w =>
        {
            w.Write(week);
            w.Write(count);
            body(w);
        });
    }
}
