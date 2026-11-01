using System;
using Cities.World.Terrain;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Terrain
{
    public class EarthworksTests
    {
        [Test]
        public void MovingSoilCostsMoneyPerCubicMeter()
        {
            var quote = Earthworks.Quote(100f, 100f, null);

            Assert.That(quote.Cost, Is.EqualTo(200f * Earthworks.PricePerCubicMeter));
            Assert.That(quote.Feasible, Is.True, "afgraven en ophogen heffen elkaar op: geen depot nodig");
        }

        [Test]
        public void ARaiseNeedsASourceWithEnoughSoil()
        {
            Assert.That(Earthworks.Quote(0f, 50f, null).Block, Is.EqualTo(EarthworksBlock.NoDepot));
            Assert.That(Earthworks.Quote(0f, 50f, new SoilDepot(100f, 20f)).Block, Is.EqualTo(EarthworksBlock.NotEnoughSoil));
            Assert.That(Earthworks.Quote(0f, 50f, new SoilDepot(100f, 50f)).Feasible, Is.True);
        }

        [Test]
        public void ACutNeedsAPitWithEnoughRoom()
        {
            Assert.That(Earthworks.Quote(50f, 0f, null).Block, Is.EqualTo(EarthworksBlock.NoDepot));
            Assert.That(Earthworks.Quote(50f, 0f, new SoilDepot(100f, 80f)).Block, Is.EqualTo(EarthworksBlock.DepotFull));
            Assert.That(Earthworks.Quote(50f, 0f, new SoilDepot(100f, 50f)).Feasible, Is.True);
        }

        [Test]
        public void CommitMovesTheNetSoilAndIgnoresBlockedQuotes()
        {
            var depot = new SoilDepot(100f, 50f);

            Earthworks.Commit(Earthworks.Quote(0f, 30f, depot), depot);
            Assert.That(depot.Stock, Is.EqualTo(20f));
            Earthworks.Commit(Earthworks.Quote(60f, 0f, depot), depot);
            Assert.That(depot.Stock, Is.EqualTo(80f));
            Earthworks.Commit(Earthworks.Quote(0f, 500f, depot), depot);
            Assert.That(depot.Stock, Is.EqualTo(80f), "te weinig grond: niets geboekt");
            Earthworks.Commit(Earthworks.Quote(0f, 5f, null), null);
        }

        [Test]
        public void ThePreviewTextShowsTheCostOrTheReason()
        {
            Assert.That(Earthworks.Describe(Earthworks.Quote(10f, 10f, null)), Does.Contain("kosten 240"));
            Assert.That(Earthworks.Describe(Earthworks.Quote(0f, 10f, null)), Does.Contain("Geen bron"));
            Assert.That(Earthworks.Describe(Earthworks.Quote(0f, 10f, new SoilDepot(100f, 1f))), Does.Contain("Te weinig grond"));
            Assert.That(Earthworks.Describe(Earthworks.Quote(10f, 0f, new SoilDepot(100f, 100f))), Does.Contain("zit vol"));
        }

        [Test]
        public void BadInputIsHandled()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SoilDepot(0f, 0f));
            Assert.That(new SoilDepot(10f, 50f).Stock, Is.EqualTo(10f), "startvoorraad begrensd");
            Assert.That(Earthworks.Quote(-5f, -5f, null).Cost, Is.Zero);
        }
    }
}
