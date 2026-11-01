using System;
using Cities.Core.Randomness;
using Cities.Simulation.Disasters;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Disasters
{
    public class FireSpreadTests
    {
        private static FireSpread Field(int width, int height, float fuel)
        {
            var fire = new FireSpread(width, height);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    fire.SetFuel(x, y, fuel);
                }
            }

            return fire;
        }

        private static void Days(FireSpread fire, int days, float2 wind, ulong seed = 1)
        {
            var random = new DeterministicRandom(seed);
            for (var i = 0; i < days; i++)
            {
                fire.Step(ref random, wind);
            }
        }

        // Aantal cellen vanaf de startcel (20) dat in de gegeven richting is aangestoken, brandend of uitgebrand.
        private static int Reach(FireSpread fire, int direction)
        {
            var reach = 0;
            for (var x = 20; x >= 0 && x < fire.Width; x += direction)
            {
                if (fire.FuelAt(x, 0) == 0f || fire.IsBurning(x, 0))
                {
                    reach = Math.Abs(x - 20);
                }
            }

            return reach;
        }

        [Test]
        public void ACellBurnsForThreeDaysAndLeavesAsh()
        {
            var fire = Field(1, 1, 1f);
            fire.Ignite(0, 0);

            Days(fire, FireSpread.BurnDays - 1, float2.zero);
            Assert.That(fire.IsBurning(0, 0), Is.True);
            var random = new DeterministicRandom(1);
            fire.Step(ref random, float2.zero);

            Assert.That(fire.IsBurning(0, 0), Is.False);
            Assert.That(fire.FuelAt(0, 0), Is.Zero);
            Assert.That(fire.BurnedCount, Is.EqualTo(1));
            Assert.That(fire.BurningCount, Is.Zero);
        }

        [Test]
        public void AnEmptyCellCannotBeIgnitedAndIgnitingTwiceCountsOnce()
        {
            var fire = Field(2, 1, 0f);
            fire.SetFuel(1, 0, 1f);

            fire.Ignite(0, 0);
            fire.Ignite(1, 0);
            fire.Ignite(1, 0);

            Assert.That(fire.BurningCount, Is.EqualTo(1));
        }

        [Test]
        public void DenseCellsCatchFireMoreOftenThanSparseOnes()
        {
            var dense = Field(30, 1, 1f);
            var sparse = Field(30, 1, 0.1f);
            dense.Ignite(0, 0);
            sparse.Ignite(0, 0);

            Days(dense, 6, float2.zero);
            Days(sparse, 6, float2.zero);

            Assert.That(dense.BurningCount + dense.BurnedCount, Is.GreaterThan(sparse.BurningCount + sparse.BurnedCount));
        }

        [Test]
        public void TheFireTravelsWithTheWindAndStallsAgainstIt()
        {
            var east = Field(41, 1, 1f);
            var west = Field(41, 1, 1f);
            east.Ignite(20, 0);
            west.Ignite(20, 0);

            Days(east, 8, new float2(2f, 0f));
            Days(west, 8, new float2(-2f, 0f));

            Assert.That(Reach(east, 1), Is.GreaterThan(Reach(east, -1)), "met de wind mee verder dan tegen de wind in");
            Assert.That(Reach(west, -1), Is.GreaterThan(Reach(west, 1)));
        }

        [Test]
        public void ExtinguishingStopsTheFireButKeepsTheFuel()
        {
            var fire = Field(1, 1, 1f);
            fire.Ignite(0, 0);

            fire.Extinguish(0, 0);
            fire.Extinguish(0, 0);

            Assert.That(fire.BurningCount, Is.Zero);
            Assert.That(fire.FuelAt(0, 0), Is.EqualTo(1f));
        }

        [Test]
        public void TheSameSeedGivesTheSameFire()
        {
            var a = Field(20, 20, 0.8f);
            var b = Field(20, 20, 0.8f);
            a.Ignite(10, 10);
            b.Ignite(10, 10);

            Days(a, 10, new float2(0.5f, 0f), 7);
            Days(b, 10, new float2(0.5f, 0f), 7);

            Assert.That(a.BurnedCount, Is.EqualTo(b.BurnedCount));
            Assert.That(a.BurningCount, Is.EqualTo(b.BurningCount));
        }

        [Test]
        public void BadInputIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FireSpread(0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Field(2, 2, 0f).Ignite(2, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Field(2, 2, 0f).FuelAt(0, -1));
        }
    }
}
