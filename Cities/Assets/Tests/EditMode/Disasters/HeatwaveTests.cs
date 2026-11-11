using System;
using Cities.Core.Persistence;
using Cities.Simulation.Disasters;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Disasters
{
    public class HeatwaveTests
    {
        private static FireSpread Dry(int size, float fuel)
        {
            var fire = new FireSpread(size, size);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    fire.SetFuel(x, y, fuel);
                }
            }

            return fire;
        }

        private static int Run(ulong seed)
        {
            var fire = Dry(40, 1f);
            var wave = new Heatwave(1, 5, 3, seed);
            var total = 0;
            for (var i = 0; i < 3; i++)
            {
                total += wave.Advance(fire);
            }

            Assert.That(fire.BurningCount, Is.EqualTo(total));
            return total;
        }

        [Test]
        public void DemandRisesWithStrengthAndDryness()
        {
            var mild = new Heatwave(1, 1, 4, 5UL);
            var severe = new Heatwave(2, 5, 4, 5UL);
            var before = severe.WaterDemandFactor;

            severe.Advance(null);
            severe.Advance(null);

            Assert.That(severe.WaterDemandFactor, Is.GreaterThan(before));
            Assert.That(severe.WaterDemandFactor, Is.GreaterThan(mild.WaterDemandFactor));
            Assert.That(severe.EnergyDemandFactor, Is.GreaterThan(mild.EnergyDemandFactor));
            Assert.That(severe.WaterDemandFactor, Is.LessThanOrEqualTo(1.61f));
            Assert.That(Heatwave.Spec.Name, Is.EqualTo("Hitte en droogte"));
        }

        [Test]
        public void DroughtAddsFuelAndEndsAfterItsDuration()
        {
            var fire = Dry(4, 0.5f);
            var wave = new Heatwave(1, 3, 2, 5UL);

            wave.Advance(fire);

            Assert.That(fire.FuelAt(0, 0), Is.GreaterThan(0.5f));
            Assert.That(wave.IsOver, Is.False);
            wave.Advance(fire);
            Assert.That(wave.IsOver, Is.True);
        }

        [Test]
        public void VeryDryCellsCanIgniteAndTheOutcomeIsDeterministic()
        {
            Assert.That(Run(11UL), Is.GreaterThan(0));
            Assert.That(Run(11UL), Is.EqualTo(Run(11UL)));
        }

        [Test]
        public void BarrenCellsNeverBurn()
        {
            var fire = Dry(10, 0f);
            var wave = new Heatwave(1, 5, 3, 1UL);

            Assert.That(wave.Advance(fire), Is.EqualTo(0));
            Assert.That(fire.BurningCount, Is.EqualTo(0));
        }

        [Test]
        public void RejectsBadArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Heatwave(1, 0, 3, 1UL));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Heatwave(1, 6, 3, 1UL));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Heatwave(1, 3, 0, 1UL));
        }

        [Test]
        public void SectionRoundTripsTheRunningWave()
        {
            var field = new HeatwaveField();
            field.Start(4, 6, 77UL).Advance(null);
            var bytes = SaveBytes.Write(new SaveMetadata(), new HeatwaveSection(field));

            var loaded = new HeatwaveField();
            SaveBytes.Read(bytes, new HeatwaveSection(loaded));

            Assert.That(loaded.Current.Strength, Is.EqualTo(4));
            Assert.That(loaded.Current.AgeDays, Is.EqualTo(1));
            Assert.That(loaded.NextId, Is.EqualTo(2));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new HeatwaveSection(loaded)), Is.EqualTo(bytes));
            Assert.That(new HeatwaveSection(loaded).Id, Is.EqualTo("HEAT"));

            var empty = new HeatwaveField();
            empty.Start(1, 1, 1UL);
            empty.Clear();
            var emptyBytes = SaveBytes.Write(new SaveMetadata(), new HeatwaveSection(empty));
            var loadedEmpty = new HeatwaveField();
            SaveBytes.Read(emptyBytes, new HeatwaveSection(loadedEmpty));
            Assert.That(loadedEmpty.Current, Is.Null);
            Assert.Throws<ArgumentNullException>(() => new HeatwaveSection(null));
        }
    }
}
