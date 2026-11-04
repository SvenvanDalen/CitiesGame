using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;
using Cities.Simulation.Disasters;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Disasters
{
    public class StormTests
    {
        private static List<StormPowerLine> Lines()
        {
            var lines = new List<StormPowerLine>();
            for (var i = 0; i < 100; i++)
            {
                lines.Add(new StormPowerLine(i, new float2(i * 10f, 0f)));
            }

            return lines;
        }

        private static FireSpread Forest()
        {
            var fire = new FireSpread(40, 40);
            for (var y = 0; y < 40; y++)
            {
                for (var x = 0; x < 40; x++)
                {
                    fire.SetFuel(x, y, 1f);
                }
            }

            return fire;
        }

        [Test]
        public void ConstructorRejectsBadValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Storm(1, float2.zero, 0f, 1, 1UL));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Storm(1, float2.zero, 10f, 0, 1UL));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Storm(1, float2.zero, 10f, 6, 1UL));
            var storm = new Storm(1, float2.zero, 10f, 3, 1UL);
            Assert.Throws<ArgumentNullException>(() => storm.Advance(null, null, 10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => storm.Advance(Lines(), null, 0f));
        }

        [Test]
        public void OnlyLinesInsideTheStormFailAndStayDownUntilRepaired()
        {
            var storm = new Storm(1, new float2(200f, 0f), 100f, 5, 3UL);
            var lines = Lines();

            for (var day = 0; day < 5; day++)
            {
                storm.Advance(lines, null, 10f);
            }

            Assert.That(storm.LinesDown, Is.Not.Empty);
            Assert.That(storm.LinesDown.All(id => storm.Contains(lines[id].Position)));
            Assert.That(storm.LinesDown.Distinct().Count(), Is.EqualTo(storm.LinesDown.Count), "een lijn valt maar een keer uit");
            var first = storm.LinesDown[0];
            Assert.That(storm.Repair(first), Is.True);
            Assert.That(storm.Repair(first), Is.False);
            Assert.That(storm.AgeDays, Is.EqualTo(5));
        }

        [Test]
        public void AStrongerStormKnocksOutMoreLinesAndStrikesMore()
        {
            var weak = new Storm(1, new float2(500f, 0f), 600f, 1, 9UL);
            var strong = new Storm(2, new float2(500f, 0f), 600f, 5, 9UL);

            var weakOutcome = weak.Advance(Lines(), null, 10f);
            var strongOutcome = strong.Advance(Lines(), null, 10f);

            Assert.That(strongOutcome.LinesDown.Count, Is.GreaterThan(weakOutcome.LinesDown.Count));
            Assert.That(strongOutcome.Strikes.Count, Is.EqualTo(Storm.StrikesPerStrength * 5));
            Assert.That(weakOutcome.Strikes.Count, Is.EqualTo(Storm.StrikesPerStrength));
            Assert.That(strong.WindStrength, Is.GreaterThan(weak.WindStrength));
        }

        [Test]
        public void LightningIgnitesFuelOnTheMapOnly()
        {
            var fire = Forest();
            var storm = new Storm(1, new float2(200f, 200f), 150f, 5, 5UL);

            var outcome = storm.Advance(new List<StormPowerLine>(), fire, 10f);

            Assert.That(outcome.Fires, Is.GreaterThan(0));
            Assert.That(fire.BurningCount, Is.EqualTo(outcome.Fires));
            foreach (var strike in outcome.Strikes)
            {
                Assert.That(fire.IsBurning(strike.x, strike.y), Is.True);
            }

            var offMap = new Storm(2, new float2(-5000f, -5000f), 10f, 5, 5UL).Advance(Lines(), Forest(), 10f);
            Assert.That(offMap.Fires, Is.Zero);
            var barren = new Storm(3, new float2(200f, 200f), 150f, 5, 5UL).Advance(Lines(), new FireSpread(40, 40), 10f);
            Assert.That(barren.Fires, Is.Zero);
        }

        [Test]
        public void ABurningCellIsNotCountedTwice()
        {
            var fire = Forest();
            var storm = new Storm(1, new float2(205f, 205f), 1f, 1, 5UL);
            var first = storm.Advance(new List<StormPowerLine>(), fire, 10f);
            var second = new Storm(2, new float2(205f, 205f), 1f, 1, 5UL).Advance(new List<StormPowerLine>(), fire, 10f);

            Assert.That(first.Fires, Is.EqualTo(1), "alle slagen vallen in dezelfde cel");
            Assert.That(second.Fires, Is.Zero);
        }

        [Test]
        public void TheSameSeedGivesTheSameStorm()
        {
            var a = new Storm(1, new float2(300f, 0f), 200f, 4, 11UL);
            var b = new Storm(1, new float2(300f, 0f), 200f, 4, 11UL);
            var c = new Storm(1, new float2(300f, 0f), 200f, 4, 12UL);
            for (var i = 0; i < 3; i++)
            {
                a.Advance(Lines(), null, 10f);
                b.Advance(Lines(), null, 10f);
                c.Advance(Lines(), null, 10f);
            }

            CollectionAssert.AreEqual(a.LinesDown, b.LinesDown);
            CollectionAssert.AreNotEqual(a.LinesDown, c.LinesDown);
            Assert.That(Storm.Spec.Name, Is.EqualTo("Storm"));
        }

        [Test]
        public void TheFieldReplacesTheCurrentStorm()
        {
            var field = new StormField();
            var first = field.Start(float2.zero, 10f, 1, 1UL);
            var second = field.Start(float2.zero, 10f, 2, 1UL);

            Assert.That(second.Id, Is.EqualTo(first.Id + 1));
            Assert.That(field.Current, Is.SameAs(second));
            field.Clear();
            Assert.That(field.Current, Is.Null);
        }

        [Test]
        public void TheSectionRoundTripsDeterministically()
        {
            var field = new StormField();
            var storm = field.Start(new float2(200f, 0f), 150f, 5, ulong.MaxValue - 4);
            storm.Advance(Lines(), null, 10f);
            storm.Advance(Lines(), null, 10f);
            var bytes = SaveBytes.Write(new SaveMetadata(), new StormSection(field));

            var loaded = new StormField();
            SaveBytes.Read(bytes, new StormSection(loaded));

            Assert.That(loaded.Current.AgeDays, Is.EqualTo(2));
            Assert.That(loaded.Current.Seed, Is.EqualTo(storm.Seed));
            CollectionAssert.AreEqual(storm.LinesDown, loaded.Current.LinesDown);
            Assert.That(SaveBytes.Write(new SaveMetadata(), new StormSection(loaded)), Is.EqualTo(bytes));
            Assert.That(new StormSection(loaded).Id, Is.EqualTo("STRM"));

            var empty = new StormField();
            empty.Start(float2.zero, 1f, 1, 1UL);
            empty.Clear();
            var emptyBytes = SaveBytes.Write(new SaveMetadata(), new StormSection(empty));
            var loadedEmpty = new StormField();
            loadedEmpty.Start(float2.zero, 1f, 1, 1UL);
            SaveBytes.Read(emptyBytes, new StormSection(loadedEmpty));
            Assert.That(loadedEmpty.Current, Is.Null);
            Assert.That(loadedEmpty.NextId, Is.EqualTo(2));
        }

        [Test]
        public void ACorruptSaveIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new StormSection(null));
            var section = new StormSection(new StormField());

            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Corrupt(w, 0, 1, 10f, 1, 0, 0)), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Corrupt(w, 5, 9, 10f, 1, 0, 0)), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Corrupt(w, 5, 1, 0f, 1, 0, 0)), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Corrupt(w, 5, 1, 10f, 9, 0, 0)), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Corrupt(w, 5, 1, 10f, 1, -1, 0)), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Corrupt(w, 5, 1, 10f, 1, 0, -1)), 1));
        }

        private static void Corrupt(SaveWriter w, int nextId, int id, float radius, int strength, int age, int count)
        {
            w.Write(nextId);
            if (nextId < 1)
            {
                return;
            }

            w.Write(true);
            w.Write(id);
            w.Write(0f);
            w.Write(0f);
            w.Write(radius);
            w.Write(strength);
            w.Write(1L);
            w.Write(age);
            w.Write(count);
        }
    }
}
