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
    public class ProcessingIndustryTests
    {
        // Staal: 2 ton erts + 1 ton kolen geeft 2 ton staal per cyclus, 10 cycli per dag.
        private static RecipeBook Book()
        {
            var book = new RecipeBook();
            book.Add(new Recipe("staal", new[] { new RecipeInput("erts", 2f), new RecipeInput("kolen", 1f) }, 2f, 10f, 2f, 3f));
            book.Add(new Recipe("meel", new[] { new RecipeInput("graan", 1f) }, 1f, 5f, 2f, 2f));
            return book;
        }

        [Test]
        public void ARecipeValidatesItsParametersAndReportsCapacities()
        {
            var recipe = Book().Get("staal");

            Assert.That(recipe.RawPerCycle, Is.EqualTo(3f));
            Assert.That(recipe.OutputCapacity, Is.EqualTo(6f));
            Assert.That(recipe.InputCapacity("erts"), Is.EqualTo(4f));
            Assert.That(recipe.InputCapacity("goud"), Is.Zero);
            Assert.Throws<ArgumentException>(() => new Recipe("", new[] { new RecipeInput("a", 1) }, 1, 1, 1, 1));
            Assert.Throws<ArgumentNullException>(() => new Recipe("x", null, 1, 1, 1, 1));
            Assert.Throws<ArgumentException>(() => new Recipe("x", new RecipeInput[0], 1, 1, 1, 1));
            Assert.Throws<ArgumentException>(() => new Recipe("x", new[] { new RecipeInput("a", 1), new RecipeInput("a", 2) }, 1, 1, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Recipe("x", new[] { new RecipeInput("a", 1) }, 0, 1, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Recipe("x", new[] { new RecipeInput("a", 1) }, 1, 1, 0.5f, 1));
            Assert.Throws<ArgumentException>(() => new RecipeInput(" ", 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RecipeInput("a", 0));
        }

        [Test]
        public void TheRecipeBookRejectsDuplicatesAndUnknownProducts()
        {
            var book = Book();

            Assert.Throws<InvalidOperationException>(() => book.Add(book.Get("meel")));
            Assert.Throws<ArgumentNullException>(() => book.Add(null));
            Assert.Throws<KeyNotFoundException>(() => book.Get("goud"));
            Assert.That(book.TryGet(null, out _), Is.False);
            Assert.That(book.Recipes.Select(r => r.Product).ToArray(), Is.EqualTo(new[] { "meel", "staal" }));
        }

        [Test]
        public void APlantMakesProductFromRawMaterialAtItsEfficiency()
        {
            var industry = new ProcessingIndustry(Book());
            var plant = industry.Add("staal", 0.5f);
            industry.SupplyRaw("erts", 4f);
            industry.SupplyRaw("kolen", 2f);

            var made = industry.Step(0.5f);

            // 10 cycli x 0,5 bezetting x 0,5 dag = 2,5 cycli; voorraad 2 cycli (erts 4/2) dus 2 cycli, 4 ton staal.
            Assert.That(made["staal"], Is.EqualTo(4f).Within(1e-4f));
            Assert.That(plant.InputStock("erts"), Is.Zero.Within(1e-4f));
            Assert.That(plant.Status, Is.EqualTo(PlantStatus.StarvedForInput));
            Assert.That(industry.Stock("staal"), Is.EqualTo(4f).Within(1e-4f));
            Assert.That(plant.RawCapacityPerDay, Is.EqualTo(3f * 10f * 0.5f));
        }

        [Test]
        public void ALimitedInputBlocksProductionAndAFullOutputStopsIt()
        {
            var industry = new ProcessingIndustry(Book());
            var plant = industry.Add("meel");
            industry.SupplyRaw("graan", 100f);

            industry.Step(10f);
            Assert.That(plant.OutputStock, Is.EqualTo(2f).Within(1e-4f), "uitvoeropslag begrenst");
            Assert.That(plant.Status, Is.EqualTo(PlantStatus.StarvedForInput));

            industry.Step(1f);
            Assert.That(plant.Status, Is.EqualTo(PlantStatus.OutputFull));
            Assert.That(plant.LastProduced, Is.Zero);
        }

        [Test]
        public void ZeroEfficiencyOrZeroTimeIsIdle()
        {
            var industry = new ProcessingIndustry(Book());
            var plant = industry.Add("meel", 0f);
            industry.SupplyRaw("graan", 2f);

            Assert.That(industry.Step(1f)["meel"], Is.Zero);
            Assert.That(plant.Status, Is.EqualTo(PlantStatus.Idle));
            plant.SetEfficiency(float.NaN);
            Assert.That(plant.Efficiency, Is.Zero);
            plant.SetEfficiency(7f);
            Assert.That(plant.Efficiency, Is.EqualTo(1f));
            Assert.That(industry.Step(0f)["meel"], Is.Zero);
        }

        [Test]
        public void RawSupplyIsSharedByFreeRoomAndTheOverflowIsReturned()
        {
            var industry = new ProcessingIndustry(Book());
            var a = industry.Add("meel");
            var b = industry.Add("meel");
            industry.Add("staal");

            Assert.That(industry.SupplyRaw("graan", 2f), Is.Zero);
            Assert.That(a.InputStock("graan"), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(b.InputStock("graan"), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(industry.SupplyRaw("graan", 10f), Is.EqualTo(8f).Within(1e-4f));
            Assert.That(industry.SupplyRaw("goud", 5f), Is.EqualTo(5f));
            Assert.That(industry.SupplyRaw("graan", -1f), Is.Zero);
            Assert.That(a.Accept("goud", 1f), Is.Zero);
            Assert.That(a.Accept("graan", 0f), Is.Zero);
        }

        [Test]
        public void ShopsGetTheirFullRequestWhenThereIsEnoughStock()
        {
            var industry = new ProcessingIndustry(Book());
            var plant = industry.Add("meel");
            industry.SupplyRaw("graan", 2f);
            industry.Step(1f);

            var deliveries = industry.DeliverToShops("meel", new[] { new ShopRequest(7, 0.5f), new ShopRequest(3, 1f) });

            Assert.That(deliveries.Select(d => d.ShopId).ToArray(), Is.EqualTo(new[] { 7, 3 }));
            Assert.That(deliveries.All(d => d.Shortage == 0f), Is.True);
            Assert.That(plant.OutputStock, Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void ScarcityIsSharedProportionallyAmongShops()
        {
            var industry = new ProcessingIndustry(Book());
            industry.Add("meel");
            industry.Add("meel");
            industry.SupplyRaw("graan", 4f);
            industry.Step(1f);

            var deliveries = industry.DeliverToShops("meel", new[] { new ShopRequest(1, 3f), new ShopRequest(2, 1f), new ShopRequest(3, 0f) });

            // Precies genoeg voorraad: alles wordt geleverd; daarna is de voorraad op.
            Assert.That(deliveries.Sum(d => d.Delivered), Is.EqualTo(4f).Within(1e-4f));
            var more = industry.DeliverToShops("meel", new[] { new ShopRequest(1, 4f), new ShopRequest(2, 4f) });
            Assert.That(more.Sum(d => d.Delivered), Is.Zero);
            Assert.That(more[0].Shortage, Is.EqualTo(4f));
        }

        [Test]
        public void HalfStockGivesEveryShopHalfItsDemand()
        {
            var industry = new ProcessingIndustry(Book());
            industry.Add("meel");
            industry.SupplyRaw("graan", 2f);
            industry.Step(1f);

            var deliveries = industry.DeliverToShops("meel", new[] { new ShopRequest(1, 3f), new ShopRequest(2, 1f) });

            Assert.That(deliveries[0].Delivered, Is.EqualTo(1.5f).Within(1e-4f));
            Assert.That(deliveries[1].Delivered, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(industry.Stock("meel"), Is.Zero.Within(1e-4f));
            Assert.Throws<ArgumentNullException>(() => industry.DeliverToShops("meel", null));
        }

        [Test]
        public void PlantsCanBeAddedRemovedAndLookedUp()
        {
            var industry = new ProcessingIndustry(Book());
            var first = industry.Add("meel");

            Assert.That(industry.Get(first.Id), Is.SameAs(first));
            Assert.That(industry.Remove(first.Id), Is.True);
            Assert.That(industry.Remove(first.Id), Is.False);
            Assert.Throws<KeyNotFoundException>(() => industry.Get(first.Id));
            Assert.Throws<KeyNotFoundException>(() => industry.Add("goud"));
            Assert.That(industry.Add("meel").Id, Is.EqualTo(2));
            Assert.That(industry.Count, Is.EqualTo(1));
            Assert.Throws<ArgumentNullException>(() => new ProcessingIndustry(null));
        }

        [Test]
        public void TheIndustryFeedsTheGoodsChainOfE1108()
        {
            var industry = new ProcessingIndustry(Book());
            industry.Add("meel");
            industry.Add("meel", 0.5f);
            var chain = new GoodsChain("meel", 1f, 100f, 100f);

            var input = industry.ToChainInput("meel", 20f, 10f);
            var week = chain.Step(input);

            // Capaciteit: (1 x 5 x 1 + 1 x 5 x 0,5) x 7 = 52,5 ton per week.
            Assert.That(input.IndustryCapacity, Is.EqualTo(52.5f).Within(1e-3f));
            Assert.That(week.Processed, Is.EqualTo(20f).Within(1e-3f));
            Assert.That(week.Delivered, Is.EqualTo(10f).Within(1e-3f));
        }

        [Test]
        public void SaveRoundTripKeepsPlantsStocksAndIds()
        {
            var industry = new ProcessingIndustry(Book());
            industry.Add("meel");
            industry.Remove(industry.Add("meel").Id);
            var steel = industry.Add("staal", 0.75f);
            industry.SupplyRaw("erts", 3f);
            industry.SupplyRaw("kolen", 1f);
            industry.Step(0.1f);
            var bytes = SaveBytes.Write(new SaveMetadata(), new ProcessingSection(industry));
            var loaded = new ProcessingIndustry(Book());
            loaded.Add("meel");

            SaveBytes.Read(bytes, new ProcessingSection(loaded));

            Assert.That(loaded.Plants.Select(p => p.Id).ToArray(), Is.EqualTo(new[] { 1, 3 }));
            Assert.That(loaded.NextId, Is.EqualTo(industry.NextId));
            Assert.That(loaded.Get(3).Efficiency, Is.EqualTo(0.75f));
            Assert.That(loaded.Get(3).InputStock("erts"), Is.EqualTo(steel.InputStock("erts")));
            Assert.That(loaded.Get(3).Status, Is.EqualTo(steel.Status));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new ProcessingSection(loaded)), Is.EqualTo(bytes));
            Assert.That(new ProcessingSection(industry).Id, Is.EqualTo("PROC"));
            Assert.Throws<ArgumentNullException>(() => new ProcessingSection(null));
        }

        [Test]
        public void ACorruptSaveIsRejectedAndChangesNothing()
        {
            var industry = new ProcessingIndustry(Book());
            industry.Add("meel");
            var section = new ProcessingSection(industry);

            Assert.Throws<SaveFormatException>(() => section.Read(Save(2, w => w.Write(-1)), 1), "negatief aantal");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(1, w => { w.Write(1); Plant(w, 1, "meel", 1f, 0f, 0); }), 1), "volgnummer te laag");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(9, w => { w.Write(2); Plant(w, 2, "meel", 1f, 0f, 0); Plant(w, 1, "meel", 1f, 0f, 0); }), 1), "volgorde");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(9, w => { w.Write(1); Plant(w, 1, "goud", 1f, 0f, 0); }), 1), "onbekend recept");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(9, w => { w.Write(1); Plant(w, 1, "meel", 2f, 0f, 0); }), 1), "bezetting");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(9, w => { w.Write(1); Plant(w, 1, "meel", 1f, -1f, 0); }), 1), "negatieve voorraad");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(9, w => { w.Write(1); Plant(w, 1, "meel", 1f, 0f, 9); }), 1), "status");
            Assert.That(industry.Count, Is.EqualTo(1));
        }

        private static SaveReader Save(int nextId, Action<SaveWriter> body) => SaveBytes.ReaderFor(w =>
        {
            w.Write(nextId);
            body(w);
        });

        private static void Plant(SaveWriter w, int id, string product, float efficiency, float output, byte status)
        {
            w.Write(id);
            w.Write(product);
            w.Write(efficiency);
            w.Write(output);
            w.Write(status);
            w.Write(0f);
        }
    }
}
