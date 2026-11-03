using System;
using System.Linq;
using Cities.Core.Persistence;
using Cities.Gameplay.Tools;
using Cities.Tests.EditMode.Persistence;
using Cities.World.Districts;
using Cities.World.Grid;
using Cities.World.Picking;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Districts
{
    public class DistrictTests
    {
        /// <summary>32 cellen van 8 m: 8×8 blokken van 32 m.</summary>
        private static readonly WorldDimensions Dimensions = new WorldDimensions(1, 1, 32, 8f);

        [Test]
        public void NewDistrictsGetTheLowestFreeNumberAndAPaletteColour()
        {
            var book = new DistrictBook();

            var a = book.Create("Centrum");
            var b = book.Create("  Haven ");
            book.Remove(a);
            var c = book.Create("Industrie", 0x123456);

            Assert.That(new[] { a, b, c }, Is.EqualTo(new[] { 1, 2, 1 }));
            Assert.That(book.NameOf(2), Is.EqualTo("Haven"));
            Assert.That(book.TryGet(1, out var info) && info.Rgb == 0x123456, Is.True);
            Assert.That(book.NameOf(DistrictBook.WholeCity), Is.EqualTo("Hele stad"));
            Assert.That(book.Districts.Select(d => d.Id), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(book.Revision, Is.GreaterThan(0));
        }

        [Test]
        public void NamesAndColoursAreValidated()
        {
            var book = new DistrictBook();
            var id = book.Create("Centrum");

            Assert.Throws<ArgumentException>(() => book.Create(" "));
            Assert.Throws<ArgumentException>(() => book.Create(new string('x', DistrictBook.MaxNameLength + 1)));
            Assert.Throws<ArgumentException>(() => book.Create("CENTRUM"));
            Assert.Throws<ArgumentException>(() => book.Create("Oost", 0x1000000));
            Assert.Throws<ArgumentException>(() => book.Rename(id + 1, "Nergens"));
            Assert.Throws<ArgumentException>(() => book.NameOf(9));
            Assert.DoesNotThrow(() => book.Rename(id, "centrum"), "de eigen naam in andere hoofdletters mag");
        }

        [Test]
        public void RenameAndRecolourChangeOnlyThatDistrict()
        {
            var book = new DistrictBook();
            var a = book.Create("A", 0x111111);
            var b = book.Create("B", 0x222222);

            book.Rename(a, "Noord");
            book.Recolor(b, 0xABCDEF);

            Assert.That(book.Districts.Select(d => (d.Name, d.Rgb)), Is.EqualTo(new[] { ("Noord", 0x111111), ("B", 0xABCDEF) }));
        }

        [Test]
        public void CannotCreateMoreThanTheMaximum()
        {
            var book = new DistrictBook();
            for (var i = 0; i < DistrictBook.MaxDistricts; i++)
            {
                book.Create("D" + i);
            }

            Assert.Throws<InvalidOperationException>(() => book.Create("Te veel"));
            Assert.That(book.Count, Is.EqualTo(255));
        }

        [Test]
        public void PaintingFillsABlockCircleAndReplacesWhatWasThere()
        {
            var map = new DistrictMap(Dimensions);
            var a = map.Book.Create("A");
            var b = map.Book.Create("B");

            var painted = map.Paint(new float2(128f, 128f), 40f, a);
            var again = map.Paint(new float2(128f, 128f), 40f, a);
            var over = map.Paint(new float2(128f, 128f), 20f, b);

            Assert.That(painted, Is.GreaterThan(0));
            Assert.That(again, Is.EqualTo(0));
            Assert.That(over, Is.GreaterThan(0).And.LessThan(painted));
            Assert.That(map.AtPosition(128f, 128f), Is.EqualTo(b));
            Assert.That(map.AtPosition(128f - 38f, 128f), Is.EqualTo(a));
            Assert.That(map.AtPosition(10f, 10f), Is.EqualTo(0));
            Assert.That(map.BlockCount(a) + map.BlockCount(b), Is.EqualTo(painted));
            Assert.That(map.BlockCount(0), Is.EqualTo((map.BlocksX * map.BlocksZ) - painted));
            Assert.That(map.Area(a), Is.EqualTo(map.BlockCount(a) * 32f * 32f));
        }

        [Test]
        public void PaintingStaysInsideTheMapAndRejectsUnknownDistricts()
        {
            var map = new DistrictMap(Dimensions);
            var a = map.Book.Create("A");

            Assert.That(map.Paint(new float2(-500f, -500f), 30f, a), Is.EqualTo(0));
            Assert.That(map.Paint(new float2(0f, 0f), 100f, a), Is.GreaterThan(0));
            Assert.That(map.Paint(new float2(128f, 128f), 0f, a), Is.EqualTo(0));
            Assert.That(map.AtPosition(-1f, 5f), Is.EqualTo(0));
            Assert.That(map.AtPosition(5000f, 5f), Is.EqualTo(0));
            Assert.Throws<ArgumentException>(() => map.Paint(new float2(10f, 10f), 40f, 9));
            Assert.That(map.BlockCount(300), Is.EqualTo(0));
        }

        [Test]
        public void ErasingMakesBlocksWholeCityAgain()
        {
            var map = new DistrictMap(Dimensions);
            var a = map.Book.Create("A");
            map.Paint(new float2(128f, 128f), 200f, a);
            var before = map.BlockCount(a);

            var erased = map.Paint(new float2(128f, 128f), 40f, DistrictBook.WholeCity);

            Assert.That(erased, Is.GreaterThan(0));
            Assert.That(map.BlockCount(a), Is.EqualTo(before - erased));
            Assert.That(map.AtPosition(128f, 128f), Is.EqualTo(0));
        }

        [Test]
        public void RemovingADistrictClearsItsBlocksAndKeepsTheOthers()
        {
            var map = new DistrictMap(Dimensions);
            var a = map.Book.Create("A");
            var b = map.Book.Create("B");
            map.Paint(new float2(60f, 60f), 50f, a);
            map.Paint(new float2(200f, 200f), 50f, b);
            var bBlocks = map.BlockCount(b);
            var revision = map.Revision;

            Assert.That(map.RemoveDistrict(a), Is.True);
            Assert.That(map.RemoveDistrict(a), Is.False);

            Assert.That(map.BlockCount(a), Is.EqualTo(0));
            Assert.That(map.BlockCount(b), Is.EqualTo(bBlocks));
            Assert.That(map.AtPosition(60f, 60f), Is.EqualTo(0));
            Assert.That(map.AtPosition(200f, 200f), Is.EqualTo(b));
            Assert.That(map.Revision, Is.GreaterThan(revision));
            Assert.That(map.BlockCount(0) + bBlocks, Is.EqualTo(map.BlocksX * map.BlocksZ));
        }

        [Test]
        public void SaveRoundTripKeepsDistrictsAndBlocks()
        {
            var map = new DistrictMap(Dimensions);
            map.Book.Create("Centrum", 0x112233);
            var b = map.Book.Create("Haven");
            map.Paint(new float2(100f, 100f), 60f, b);
            var bytes = SaveBytes.Write(new SaveMetadata(), new DistrictSection(map));
            var loaded = new DistrictMap(Dimensions);
            loaded.Book.Create("Oud");

            SaveBytes.Read(bytes, new DistrictSection(loaded));

            Assert.That(loaded.Book.Districts.Select(d => (d.Id, d.Name, d.Rgb)), Is.EqualTo(map.Book.Districts.Select(d => (d.Id, d.Name, d.Rgb))));
            Assert.That(loaded.Blocks.ToArray(), Is.EqualTo(map.Blocks.ToArray()));
            Assert.That(loaded.BlockCount(b), Is.EqualTo(map.BlockCount(b)));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new DistrictSection(loaded)), Is.EqualTo(bytes));
        }

        [Test]
        public void ACorruptSaveIsRejectedAndChangesNothing()
        {
            var loaded = new DistrictMap(Dimensions);
            var keep = loaded.Book.Create("Blijft");
            var section = new DistrictSection(loaded);
            var blocks = loaded.Blocks.Length;

            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => w.Write(-1)), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Write(w, blocks, 0, (1, "A"), (1, "B"))), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Write(w, blocks, 0, (1, "A"), (2, "a"))), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Write(w, blocks, 0, (0, "A"))), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Write(w, blocks - 1, 0, (1, "A"))), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Write(w, blocks, 7, (1, "A"))), 1));
            Assert.That(loaded.Book.Contains(keep), Is.True);
            Assert.That(loaded.Book.Count, Is.EqualTo(1));
        }

        [Test]
        public void ToolPaintsAndErasesAndExplainsWhyNot()
        {
            var map = new DistrictMap(Dimensions);
            var a = map.Book.Create("A");
            var tool = new DistrictTool(map) { Radius = 40f };
            var ground = Ground(128f, 128f);

            Assert.That(tool.UpdatePreview(ground).InvalidReason, Is.EqualTo("Kies eerst een district"));
            tool.ActiveDistrict = a;
            Assert.That(tool.UpdatePreview(ground).InvalidReason, Is.Null);
            Assert.That(tool.UpdatePreview(Ground(-5f, 10f)).InvalidReason, Is.EqualTo("Buiten de kaart"));
            Assert.That(tool.UpdatePreview(new ToolInput(default)).IsVisible, Is.False);

            tool.Apply(tool.UpdatePreview(ground));
            Assert.That(map.AtPosition(128f, 128f), Is.EqualTo(a));
            Assert.That(tool.Name, Is.EqualTo("District schilderen"));

            tool.Erase = true;
            Assert.That(tool.UpdatePreview(ground).InvalidReason, Is.Null, "wissen heeft geen district nodig");
            tool.Apply(tool.UpdatePreview(ground));
            Assert.That(map.AtPosition(128f, 128f), Is.EqualTo(0));
            Assert.That(tool.Name, Is.EqualTo("District wissen"));
            Assert.That(tool.RepeatsWhileHeld, Is.True);
            Assert.That(tool.CancelStep(), Is.False);
        }

        [Test]
        public void ToolRadiusIsClamped()
        {
            var tool = new DistrictTool(new DistrictMap(Dimensions));

            tool.Radius = 1f;
            Assert.That(tool.Radius, Is.EqualTo(DistrictTool.MinRadius));
            tool.Radius = 9999f;
            Assert.That(tool.Radius, Is.EqualTo(DistrictTool.MaxRadius));
        }

        private static ToolInput Ground(float x, float z) =>
            new ToolInput(new PickResult(PickHit.None, new PickHit(PickKind.Terrain, 0, new float3(x, 0f, z), 100f)), 0, 0.5f);

        private static void Write(SaveWriter w, int blockCount, byte fill, params (int Id, string Name)[] districts)
        {
            w.Write(districts.Length);
            foreach (var (id, name) in districts)
            {
                w.Write(id);
                w.Write(name);
                w.Write(0x445566);
            }

            var blocks = new byte[blockCount];
            Array.Fill(blocks, fill);
            w.WriteArray<byte>(blocks);
        }
    }
}
