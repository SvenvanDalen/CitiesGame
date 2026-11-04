using System;
using System.Linq;
using Cities.Core.Persistence;
using Cities.Simulation.Milestones;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Milestones
{
    public class MilestoneTests
    {
        [Test]
        public void CatalogHasTwelveAscendingMilestonesWithUniqueIds()
        {
            var all = MilestoneCatalog.All;

            Assert.That(all.Count, Is.EqualTo(12));
            Assert.That(all.Select(m => m.Id).Distinct().Count(), Is.EqualTo(12));
            for (var i = 1; i < all.Count; i++)
            {
                Assert.That(all[i].Population, Is.GreaterThan(all[i - 1].Population));
                Assert.That(all[i].RewardCash, Is.GreaterThan(all[i - 1].RewardCash));
            }

            Assert.That(MilestoneCatalog.Find("milestone.town").Population, Is.EqualTo(5000));
            Assert.That(MilestoneCatalog.Find("nope"), Is.Null);
        }

        [Test]
        public void ReachingThresholdAwardsOnceAndOnlyOnce()
        {
            var tracker = new MilestoneTracker();

            Assert.That(tracker.Update(99, 1), Is.Empty);
            var first = tracker.Update(100, 2);
            Assert.That(first.Single().Milestone.Id, Is.EqualTo("milestone.hamlet"));
            Assert.That(first.Single().Tick, Is.EqualTo(2));
            Assert.That(tracker.Update(100, 3), Is.Empty);
            Assert.That(tracker.Update(50, 4), Is.Empty, "krimp neemt niets af");
            Assert.That(tracker.IsAchieved("milestone.hamlet"), Is.True);
            Assert.That(tracker.Next.Id, Is.EqualTo("milestone.village"));
        }

        [Test]
        public void ABigJumpAwardsEveryPassedMilestoneInOrder()
        {
            var tracker = new MilestoneTracker();

            var fresh = tracker.Update(5000, 7);

            Assert.That(fresh.Select(a => a.Milestone.Population), Is.EqualTo(new[] { 100, 500, 1000, 2500, 5000 }));
            Assert.That(tracker.Achieved.Count, Is.EqualTo(5));
        }

        [Test]
        public void ProgressAndNextFollowThePopulation()
        {
            var tracker = new MilestoneTracker();

            Assert.That(tracker.ProgressToNext(50), Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(tracker.ProgressToNext(-5), Is.EqualTo(0f).Within(0.001f));
            tracker.Update(2000000, 1);
            Assert.That(tracker.Next, Is.Null);
            Assert.That(tracker.ProgressToNext(0), Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void MessageNamesTitleThresholdAndReward()
        {
            var m = new PopulationMilestone("milestone.x", "Test", 1000, 500, "Iets.");
            var plain = new PopulationMilestone("milestone.y", null, 10, 0, null);

            Assert.That(m.Message, Does.Contain("Test").And.Contain("500").And.Contain("Iets."));
            Assert.That(plain.Message, Does.EndWith("."));
        }

        [Test]
        public void InvalidDefinitionsAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new PopulationMilestone(" ", "t", 1, 0, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PopulationMilestone("a", "t", 0, 0, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PopulationMilestone("a", "t", 1, -1, null));
            Assert.Throws<ArgumentNullException>(() => new MilestoneTracker(null));
            Assert.Throws<ArgumentNullException>(() => new MilestoneSection(null));
        }

        [Test]
        public void SaveRoundTripKeepsAchievements()
        {
            var tracker = new MilestoneTracker();
            tracker.Update(600, 42);
            var bytes = SaveBytes.Write(new SaveMetadata(), new MilestoneSection(tracker));
            var loaded = new MilestoneTracker();
            loaded.Update(100000, 1);

            SaveBytes.Read(bytes, new MilestoneSection(loaded));

            Assert.That(loaded.Achieved.Select(a => a.Milestone.Id), Is.EqualTo(new[] { "milestone.hamlet", "milestone.village" }));
            Assert.That(loaded.Achieved[1].Tick, Is.EqualTo(42));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new MilestoneSection(loaded)), Is.EqualTo(bytes));
            Assert.That(new MilestoneSection(tracker).Id, Is.EqualTo("MILE"));
        }

        [Test]
        public void ACorruptSaveIsRejectedAndChangesNothing()
        {
            var tracker = new MilestoneTracker();
            tracker.Update(100, 1);
            var section = new MilestoneSection(tracker);

            Assert.Throws<SaveFormatException>(() => section.Read(Save(-1, null), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(Save(13, null), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(Save(1, w => { w.Write("milestone.nope"); w.Write(1L); }), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(Save(1, w => { w.Write("milestone.town"); w.Write(-1L); }), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(Save(2, w =>
            {
                w.Write("milestone.town");
                w.Write(1L);
                w.Write("milestone.town");
                w.Write(2L);
            }), 1));
            Assert.That(tracker.Achieved.Count, Is.EqualTo(1));
        }

        private static SaveReader Save(int count, Action<SaveWriter> body) => SaveBytes.ReaderFor(w =>
        {
            w.Write(count);
            body?.Invoke(w);
        });
    }
}
