using System;
using System.Linq;
using Cities.Core.Persistence;
using Cities.Simulation.Economy;
using Cities.Simulation.Resources;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Resources
{
    public class ResourceExtractionTests
    {
        private static (int X, int Z)[] Block(int x, int z, int size) =>
            Enumerable.Range(0, size * size).Select(i => (x + i % size, z + i / size)).ToArray();

        private static ResourceExtraction World(ResourceKind kind, float capacity, int size = 2)
        {
            var deposits = new ResourceDeposits();
            foreach (var (x, z) in Block(0, 0, size))
            {
                deposits.Set(x, z, kind, capacity);
            }

            return new ResourceExtraction(deposits);
        }

        [Test]
        public void BuildingsOnLayersExtractTheirKind()
        {
            foreach (var kind in new[] { ResourceKind.Oil, ResourceKind.Ore, ResourceKind.Wood, ResourceKind.Crops })
            {
                var extraction = World(kind, 100f);
                extraction.Add(kind, Block(0, 0, 2), 3f, true);

                extraction.Step(1f);

                Assert.That(extraction.LastWeek(kind), Is.EqualTo(12f).Within(1e-4f), kind.ToString());
            }
        }

        [Test]
        public void OnlyCellsOfTheRightKindCount()
        {
            var extraction = World(ResourceKind.Ore, 100f);
            var e = extraction.Add(ResourceKind.Ore, Block(0, 0, 3), 2f, true);

            extraction.Step(1f);

            Assert.That(e.LastYield, Is.EqualTo(8f).Within(1e-4f), "vier cellen met erts van de negen");
            Assert.That(extraction.CanPlace(ResourceKind.Oil, Block(0, 0, 2)), Is.False);
            Assert.That(extraction.CanPlace(ResourceKind.Ore, null), Is.False);
            Assert.Throws<InvalidOperationException>(() => extraction.Add(ResourceKind.Oil, Block(0, 0, 2), 1f, true));
            Assert.Throws<InvalidOperationException>(() => extraction.Add(ResourceKind.Ore, Block(10, 10, 2), 1f, true));
        }

        [Test]
        public void FiniteDepositsRunOutWhenDepletionIsOn()
        {
            var extraction = World(ResourceKind.Oil, 5f, 1);
            extraction.Add(ResourceKind.Oil, Block(0, 0, 1), 3f, true);

            extraction.Step(1f);
            extraction.Step(1f);
            extraction.Step(1f);

            Assert.That(extraction.LastWeek(ResourceKind.Oil), Is.EqualTo(0f));
            Assert.That(extraction.Deposits.Get(0, 0).IsDepleted, Is.True);
        }

        [Test]
        public void DepletionIsOptional()
        {
            var extraction = World(ResourceKind.Oil, 5f, 1);
            extraction.Add(ResourceKind.Oil, Block(0, 0, 1), 3f, false);

            for (var i = 0; i < 10; i++)
            {
                extraction.Step(1f);
            }

            Assert.That(extraction.Deposits.Get(0, 0).Remaining, Is.EqualTo(5f));
            Assert.That(extraction.LastWeek(ResourceKind.Oil), Is.EqualTo(3f).Within(1e-4f));
        }

        [Test]
        public void WoodGrowsBackAndCropsFollowTheSeason()
        {
            var wood = World(ResourceKind.Wood, 100f, 1);
            wood.Add(ResourceKind.Wood, Block(0, 0, 1), 10f, true);
            wood.Step(1f);
            Assert.That(wood.Deposits.Get(0, 0).Remaining, Is.EqualTo(91f).Within(1e-3f), "10 gekapt, 1 teruggegroeid");
            for (var i = 0; i < 200; i++)
            {
                wood.Remove(1);
                wood.Step(1f);
            }

            Assert.That(wood.Deposits.Get(0, 0).Remaining, Is.EqualTo(100f).Within(1e-3f), "hersteld tot de capaciteit");

            var crops = World(ResourceKind.Crops, 8f, 1);
            crops.Add(ResourceKind.Crops, Block(0, 0, 1), 20f, true);
            crops.Step(0.5f);
            Assert.That(crops.LastWeek(ResourceKind.Crops), Is.EqualTo(4f).Within(1e-4f), "vruchtbaarheid 8 maal seizoen 0,5");
            crops.Step(0f);
            Assert.That(crops.LastWeek(ResourceKind.Crops), Is.EqualTo(0f), "winter");
            Assert.That(crops.Deposits.Get(0, 0).Remaining, Is.EqualTo(8f), "gewassen raken niet op");
        }

        [Test]
        public void ExtractionFeedsAGoodsChain()
        {
            var extraction = World(ResourceKind.Ore, 100f);
            extraction.Add(ResourceKind.Ore, Block(0, 0, 2), 5f, true);
            var chain = new GoodsChain(ResourceKinds.Good(ResourceKind.Ore), 2f, 100f, 50f);

            extraction.Step(1f);
            var week = chain.Step(new ChainInput(extraction.LastWeek(ResourceKind.Ore), 100f, 5f));

            Assert.That(week.Processed, Is.EqualTo(10f).Within(1e-4f));
            Assert.That(week.Delivered, Is.EqualTo(5f).Within(1e-4f));
        }

        [Test]
        public void ValidatesInput()
        {
            var extraction = World(ResourceKind.Ore, 10f, 1);
            Assert.Throws<ArgumentNullException>(() => new ResourceExtraction(null));
            Assert.Throws<ArgumentNullException>(() => extraction.Add(ResourceKind.Ore, null, 1f, true));
            Assert.Throws<ArgumentException>(() => extraction.Add(ResourceKind.Ore, Block(0, 0, 1), 0f, true));
            Assert.Throws<ArgumentException>(() => extraction.Add((ResourceKind)9, Block(0, 0, 1), 1f, true));
            Assert.Throws<ArgumentException>(() => extraction.Add(ResourceKind.Ore, new (int, int)[0], 1f, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => extraction.Step(1.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => extraction.Deposits.Set(1, 1, ResourceKind.Ore, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => extraction.Deposits.Set(1, 1, (ResourceKind)7, 1f));
            Assert.That(extraction.LastWeek((ResourceKind)7), Is.EqualTo(0f));
            Assert.That(ResourceKinds.IsFinite(ResourceKind.Oil) && !ResourceKinds.IsFinite(ResourceKind.Wood), Is.True);
            Assert.That(new[] { ResourceKind.Oil, ResourceKind.Wood, ResourceKind.Crops }.Select(ResourceKinds.Good), Is.EqualTo(new[] { "olie", "hout", "gewassen" }));
        }

        [Test]
        public void RemovingAndNegativeCoordinatesWork()
        {
            var deposits = new ResourceDeposits();
            deposits.Set(-3, 7, ResourceKind.Ore, 1f);
            var key = deposits.All.Single().Key;
            Assert.That((ResourceDeposits.KeyX(key), ResourceDeposits.KeyZ(key)), Is.EqualTo((-3, 7)));
            Assert.That(deposits.Remove(-3, 7), Is.True);
            Assert.That(deposits.Remove(-3, 7), Is.False);
            var extraction = World(ResourceKind.Ore, 1f, 1);
            var e = extraction.Add(ResourceKind.Ore, Block(0, 0, 1), 1f, true);
            Assert.That(extraction.Get(e.Id), Is.SameAs(e));
            Assert.That(extraction.Remove(e.Id), Is.True);
            Assert.That(extraction.Get(e.Id), Is.Null);
        }

        [Test]
        public void SaveRoundTripKeepsDepositsAndExtractors()
        {
            var extraction = World(ResourceKind.Oil, 10f);
            extraction.Add(ResourceKind.Oil, Block(0, 0, 2), 2f, true);
            extraction.Step(1f);
            var bytes = SaveBytes.Write(new SaveMetadata(), new ResourceSection(extraction));
            var loaded = new ResourceExtraction(new ResourceDeposits());
            loaded.Deposits.Set(50, 50, ResourceKind.Wood, 1f);

            SaveBytes.Read(bytes, new ResourceSection(loaded));

            Assert.That(loaded.Deposits.Count, Is.EqualTo(4));
            Assert.That(loaded.Deposits.Get(0, 0).Remaining, Is.EqualTo(8f).Within(1e-4f));
            Assert.That(loaded.Extractors.Single().Cells.Count, Is.EqualTo(4));
            Assert.That(loaded.NextId, Is.EqualTo(extraction.NextId));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new ResourceSection(loaded)), Is.EqualTo(bytes));
            Assert.That(new ResourceSection(loaded).Id, Is.EqualTo("RSRC"));
            Assert.Throws<ArgumentNullException>(() => new ResourceSection(null));
        }

        [Test]
        public void ACorruptSaveIsRejectedAndChangesNothing()
        {
            var extraction = World(ResourceKind.Ore, 10f, 1);
            var section = new ResourceSection(extraction);

            Assert.Throws<SaveFormatException>(() => section.Read(Save(w => w.Write(-1)), 1), "negatief aantal");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(w => { w.Write(1); Dep(w, 0, 0, 9, 1f, 1f); }), 1), "onbekende soort");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(w => { w.Write(1); Dep(w, 0, 0, 0, 1f, 2f); }), 1), "meer dan capaciteit");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(w => { w.Write(2); Dep(w, 0, 0, 0, 1f, 1f); Dep(w, 0, 0, 0, 1f, 1f); }), 1), "dubbele cel");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(w => { w.Write(0); w.Write(1); w.Write(-1); }), 1), "negatief aantal gebouwen");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(w => { w.Write(0); w.Write(1); w.Write(1); Ext(w, 5, 0, 1f, 1); }), 1), "ID boven volgnummer");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(w => { w.Write(0); w.Write(3); w.Write(2); Ext(w, 1, 0, 1f, 1); Ext(w, 1, 0, 1f, 1); }), 1), "dubbel ID");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(w => { w.Write(0); w.Write(3); w.Write(1); Ext(w, 1, 0, 0f, 1); }), 1), "snelheid nul");
            Assert.That(extraction.Deposits.Count, Is.EqualTo(1));
        }

        private static SaveReader Save(Action<SaveWriter> body) => SaveBytes.ReaderFor(body);

        private static void Dep(SaveWriter w, int x, int z, int kind, float capacity, float remaining)
        {
            w.Write(x);
            w.Write(z);
            w.Write(kind);
            w.Write(capacity);
            w.Write(remaining);
        }

        private static void Ext(SaveWriter w, int id, int kind, float rate, int cells)
        {
            w.Write(id);
            w.Write(kind);
            w.Write(rate);
            w.Write(true);
            w.Write(cells);
            for (var i = 0; i < cells; i++)
            {
                w.Write(0);
                w.Write(0);
            }
        }
    }
}
