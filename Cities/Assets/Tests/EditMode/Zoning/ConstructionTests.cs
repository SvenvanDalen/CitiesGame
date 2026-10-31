using System;
using System.Linq;
using Cities.Gameplay.Zoning;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Zoning
{
    public class ConstructionTests
    {
        [Test]
        public void DurationIsDaysTimesTicksTimesScale()
        {
            Assert.That(ConstructionSchedule.DurationTicks(30, 100, 1f), Is.EqualTo(3000));
            Assert.That(ConstructionSchedule.DurationTicks(30, 100, 0.5f), Is.EqualTo(1500));
            Assert.That(ConstructionSchedule.DurationTicks(0, 100, 1f), Is.EqualTo(0));
            Assert.That(ConstructionSchedule.DurationTicks(30, 100, 0f), Is.EqualTo(0));
        }

        [TestCase(-1, 100, 1f)]
        [TestCase(1, 0, 1f)]
        [TestCase(1, 100, -1f)]
        public void DurationRejectsInvalidInput(int days, int ticksPerDay, float scale)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ConstructionSchedule.DurationTicks(days, ticksPerDay, scale));
        }

        [Test]
        public void ProgressRunsFromZeroToOneAndClamps()
        {
            Assert.That(ConstructionSchedule.Progress(100, 200, 100), Is.EqualTo(0f));
            Assert.That(ConstructionSchedule.Progress(100, 200, 200), Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(ConstructionSchedule.Progress(100, 200, 300), Is.EqualTo(1f));
            Assert.That(ConstructionSchedule.Progress(100, 200, 999), Is.EqualTo(1f));
            Assert.That(ConstructionSchedule.Progress(100, 200, 50), Is.EqualTo(0f));
            Assert.That(ConstructionSchedule.Progress(100, 0, 100), Is.EqualTo(1f), "zonder duur direct klaar");
        }

        [Test]
        public void ScaffoldingStandsOnTheGroundAndRisesAboveTheBuilding()
        {
            var boxes = ScaffoldingLayout.Boxes(16f, 16f, 7f);

            Assert.That(boxes.Min(b => b.Min.y), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(boxes.Max(b => b.Max.y), Is.GreaterThanOrEqualTo(7f + ScaffoldingLayout.LiftHeight - 1e-4f));
            Assert.That(boxes.All(b => b.Min.x < b.Max.x && b.Min.y < b.Max.y && b.Min.z < b.Max.z), Is.True);
            Assert.That(boxes.Min(b => b.Min.x), Is.GreaterThanOrEqualTo(-8f - 0.1f));
            Assert.That(boxes.Max(b => b.Max.x), Is.LessThanOrEqualTo(8f + 0.1f));
        }

        [Test]
        public void ScaffoldingHasPolesOnAllFourSides()
        {
            var boxes = ScaffoldingLayout.Boxes(8f, 8f, 4f);

            // Hoek en midden van elke kant: staanders (hoger dan breed).
            var poles = boxes.Where(b => b.Max.y - b.Min.y > 1f).ToList();
            Assert.That(poles, Has.Count.GreaterThanOrEqualTo(8));
        }

        [Test]
        public void ScaffoldingRejectsEmptySize()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ScaffoldingLayout.Boxes(0f, 8f, 4f));
        }
    }
}
