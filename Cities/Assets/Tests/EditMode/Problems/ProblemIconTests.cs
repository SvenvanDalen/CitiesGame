using System;
using System.Collections.Generic;
using Cities.Simulation.Problems;
using Cities.Simulation.Services;
using Cities.Simulation.Utilities;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Problems
{
    public class ProblemIconTests
    {
        [Test]
        public void SetAddsRemovesAndOrdersByPriority()
        {
            var set = default(ProblemSet).With(BuildingProblem.GarbageFull).With(BuildingProblem.NoPower);
            Assert.That(set.Count, Is.EqualTo(2));
            Assert.That(set.Worst, Is.EqualTo(BuildingProblem.NoPower));
            Assert.That(set.Contains(BuildingProblem.Fire), Is.False);

            var buffer = new BuildingProblem[ProblemSet.KindCount];
            Assert.That(set.CopyTo(buffer), Is.EqualTo(2));
            Assert.That(buffer[0], Is.EqualTo(BuildingProblem.NoPower));
            Assert.That(buffer[1], Is.EqualTo(BuildingProblem.GarbageFull));

            var smaller = set.Without(BuildingProblem.NoPower);
            Assert.That(smaller.Worst, Is.EqualTo(BuildingProblem.GarbageFull));
            Assert.That(smaller.Without(BuildingProblem.GarbageFull).IsEmpty, Is.True);
            Assert.That(smaller, Is.EqualTo(default(ProblemSet).With(BuildingProblem.GarbageFull)));
            Assert.That(smaller.GetHashCode(), Is.EqualTo(default(ProblemSet).With(BuildingProblem.GarbageFull).GetHashCode()));
            Assert.That(smaller.Equals((object)"x"), Is.False);
            Assert.Throws<InvalidOperationException>(() => _ = default(ProblemSet).Worst);
        }

        [Test]
        public void CopyToStopsAtTheBufferSize()
        {
            var set = default(ProblemSet).With(BuildingProblem.Fire).With(BuildingProblem.NoWater).With(BuildingProblem.Crime);
            Assert.That(set.CopyTo(new BuildingProblem[2]), Is.EqualTo(2));
        }

        [Test]
        public void ProblemAppearsOnlyAfterShowAfterSteps()
        {
            var tracker = new ProblemTracker(3, 2);
            tracker.Report(1, BuildingProblem.NoPower, true);
            tracker.Report(1, BuildingProblem.NoPower, true);
            Assert.That(tracker.VisibleOf(1).IsEmpty, Is.True);
            tracker.Report(1, BuildingProblem.NoPower, true);
            Assert.That(tracker.VisibleOf(1).Contains(BuildingProblem.NoPower), Is.True);
            Assert.That(tracker.Count(BuildingProblem.NoPower), Is.EqualTo(1));
        }

        [Test]
        public void ABriefGlitchResetsTheStreakAndNeverShows()
        {
            var tracker = new ProblemTracker(3, 2);
            tracker.Report(1, BuildingProblem.NoWater, true);
            tracker.Report(1, BuildingProblem.NoWater, true);
            tracker.Report(1, BuildingProblem.NoWater, false);
            tracker.Report(1, BuildingProblem.NoWater, true);
            tracker.Report(1, BuildingProblem.NoWater, true);
            Assert.That(tracker.VisibleOf(1).IsEmpty, Is.True);
        }

        [Test]
        public void ProblemHidesOnlyAfterHideAfterCleanSteps()
        {
            var tracker = new ProblemTracker(1, 3);
            tracker.Report(1, BuildingProblem.GarbageFull, true);
            tracker.Report(1, BuildingProblem.GarbageFull, false);
            tracker.Report(1, BuildingProblem.GarbageFull, false);
            Assert.That(tracker.VisibleOf(1).Contains(BuildingProblem.GarbageFull), Is.True);
            tracker.Report(1, BuildingProblem.GarbageFull, true);
            tracker.Report(1, BuildingProblem.GarbageFull, false);
            tracker.Report(1, BuildingProblem.GarbageFull, false);
            Assert.That(tracker.VisibleOf(1).Contains(BuildingProblem.GarbageFull), Is.True, "de teller begon opnieuw");
            tracker.Report(1, BuildingProblem.GarbageFull, false);
            Assert.That(tracker.VisibleOf(1).IsEmpty, Is.True);
            Assert.That(tracker.Count(BuildingProblem.GarbageFull), Is.Zero);
        }

        [Test]
        public void FireShowsImmediately()
        {
            var tracker = new ProblemTracker(5, 5);
            tracker.Report(4, BuildingProblem.Fire, true);
            Assert.That(tracker.VisibleOf(4).Contains(BuildingProblem.Fire), Is.True);
        }

        [Test]
        public void ReportingNothingForAnUnknownBuildingStoresNothing()
        {
            var tracker = new ProblemTracker();
            tracker.Report(9, BuildingProblem.Crime, false);
            var all = new List<KeyValuePair<int, ProblemSet>>();
            tracker.CollectVisible(all);
            Assert.That(all, Is.Empty);
        }

        [Test]
        public void ChangedFiresOnAppearAndDisappear()
        {
            var tracker = new ProblemTracker(1, 1);
            var events = new List<(int, BuildingProblem, bool)>();
            tracker.Changed += (b, p, on) => events.Add((b, p, on));
            tracker.Report(2, BuildingProblem.NoSewage, true);
            tracker.Report(2, BuildingProblem.NoSewage, false);
            Assert.That(events, Is.EqualTo(new[] { (2, BuildingProblem.NoSewage, true), (2, BuildingProblem.NoSewage, false) }));
        }

        [Test]
        public void RemoveForgetsTheBuildingAndAnnouncesTheVisibleProblems()
        {
            var tracker = new ProblemTracker(1, 1);
            tracker.Report(2, BuildingProblem.NoSewage, true);
            tracker.Report(2, BuildingProblem.Fire, true);
            var gone = new List<BuildingProblem>();
            tracker.Changed += (_, p, on) =>
            {
                if (!on)
                {
                    gone.Add(p);
                }
            };
            tracker.Remove(2);
            tracker.Remove(2);
            Assert.That(gone, Is.EquivalentTo(new[] { BuildingProblem.NoSewage, BuildingProblem.Fire }));
            Assert.That(tracker.VisibleOf(2).IsEmpty, Is.True);
        }

        [Test]
        public void CollectVisibleListsOnlyBuildingsWithVisibleProblems()
        {
            var tracker = new ProblemTracker(2, 1);
            tracker.Report(1, BuildingProblem.NoPower, true);
            tracker.Report(1, BuildingProblem.NoPower, true);
            tracker.Report(2, BuildingProblem.NoPower, true);
            var all = new List<KeyValuePair<int, ProblemSet>>();
            tracker.CollectVisible(all);
            Assert.That(all.Count, Is.EqualTo(1));
            Assert.That(all[0].Key, Is.EqualTo(1));
        }

        [Test]
        public void InvalidDelaysAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProblemTracker(0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProblemTracker(1, 0));
        }

        [Test]
        public void SourcesTranslateExistingSystemsToProblems()
        {
            var tracker = new ProblemTracker(1, 1);

            var dispatcher = new ServiceDispatcher(10f);
            var fire = new FireModel(dispatcher);
            fire.Add(1, new float2(0f, 0f), 1f);
            ProblemSources.ReportFire(tracker, fire, 1);
            Assert.That(tracker.VisibleOf(1).IsEmpty, Is.True);
            fire.Ignite(1);
            ProblemSources.ReportFire(tracker, fire, 1);
            Assert.That(tracker.VisibleOf(1).Contains(BuildingProblem.Fire), Is.True);

            var bin = new WasteBin(10f);
            ProblemSources.ReportBin(tracker, bin, 2);
            Assert.That(tracker.VisibleOf(2).IsEmpty, Is.True);
            bin.Produce(20f);
            ProblemSources.ReportBin(tracker, bin, 2);
            Assert.That(tracker.VisibleOf(2).Contains(BuildingProblem.GarbageFull), Is.True);

            var tower = new WaterTower(100f);
            ProblemSources.ReportWater(tracker, tower, 3);
            Assert.That(tracker.VisibleOf(3).Contains(BuildingProblem.NoWater), Is.True);
            tower.Step(50f, 0f);
            ProblemSources.ReportWater(tracker, tower, 3);
            Assert.That(tracker.VisibleOf(3).IsEmpty, Is.True);

            ProblemSources.ReportPower(tracker, 4, 7, new HashSet<int> { 7 });
            ProblemSources.ReportPower(tracker, 5, 8, new HashSet<int> { 7 });
            Assert.That(tracker.VisibleOf(4).Contains(BuildingProblem.NoPower), Is.True);
            Assert.That(tracker.VisibleOf(5).IsEmpty, Is.True);
        }

        [Test]
        public void SourcesRejectNulls()
        {
            var tracker = new ProblemTracker();
            Assert.Throws<ArgumentNullException>(() => ProblemSources.ReportFire(null, new FireModel(new ServiceDispatcher(10f)), 1));
            Assert.Throws<ArgumentNullException>(() => ProblemSources.ReportFire(tracker, null, 1));
            Assert.Throws<ArgumentNullException>(() => ProblemSources.ReportBin(tracker, null, 1));
            Assert.Throws<ArgumentNullException>(() => ProblemSources.ReportWater(tracker, null, 1));
            Assert.Throws<ArgumentNullException>(() => ProblemSources.ReportPower(tracker, 1, 1, null));
        }

        [Test]
        public void IconsOfOneBuildingAreCenteredSideBySide()
        {
            Assert.That(ProblemIconLayout.Offset(0, 1, 2f), Is.Zero);
            Assert.That(ProblemIconLayout.Offset(0, 2, 2f), Is.EqualTo(-ProblemIconLayout.Offset(1, 2, 2f)));
            Assert.That(ProblemIconLayout.Offset(1, 3, 2f), Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => ProblemIconLayout.Offset(2, 2, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => ProblemIconLayout.Offset(0, 0, 1f));
        }

        [Test]
        public void LayoutKeepsNearbyBuildingsNearestFirstAndRespectsTheCap()
        {
            var roofs = new Dictionary<int, float3>
            {
                [1] = new float3(10f, 20f, 0f),
                [2] = new float3(100f, 20f, 0f),
                [3] = new float3(5000f, 20f, 0f),
                [4] = new float3(50f, 20f, 0f),
            };
            var two = default(ProblemSet).With(BuildingProblem.NoPower).With(BuildingProblem.Fire);
            var one = default(ProblemSet).With(BuildingProblem.NoWater);
            var buildings = new List<KeyValuePair<int, ProblemSet>>
            {
                new KeyValuePair<int, ProblemSet>(2, one),
                new KeyValuePair<int, ProblemSet>(3, one),
                new KeyValuePair<int, ProblemSet>(1, two),
                new KeyValuePair<int, ProblemSet>(4, one),
            };
            var icons = new List<ProblemIcon>();

            ProblemIconLayout.Build(buildings, id => roofs[id], float3.zero, new float3(1f, 0f, 0f), 2f, 5f, 1000f, 100, icons);
            Assert.That(icons.Count, Is.EqualTo(5), "gebouw 3 is te ver weg");
            Assert.That(icons[0].Problem, Is.EqualTo(BuildingProblem.Fire));
            Assert.That(icons[1].Problem, Is.EqualTo(BuildingProblem.NoPower));
            Assert.That(icons[0].Position.y, Is.EqualTo(25f));
            Assert.That(icons[0].Position.x, Is.LessThan(icons[1].Position.x));
            Assert.That(icons[2].Position.x, Is.EqualTo(50f), "daarna gebouw 4, dan gebouw 2");
            Assert.That(icons[3].Position.x, Is.EqualTo(100f));

            ProblemIconLayout.Build(buildings, id => roofs[id], float3.zero, new float3(1f, 0f, 0f), 2f, 5f, 1000f, 3, icons);
            Assert.That(icons.Count, Is.EqualTo(2), "een gebouw wordt nooit half getekend");
        }

        [Test]
        public void BuildingsWithoutRoofAreSkippedAndTiesBreakByBuildingId()
        {
            var buildings = new List<KeyValuePair<int, ProblemSet>>
            {
                new KeyValuePair<int, ProblemSet>(8, default(ProblemSet).With(BuildingProblem.Crime)),
                new KeyValuePair<int, ProblemSet>(7, default(ProblemSet).With(BuildingProblem.Crime)),
                new KeyValuePair<int, ProblemSet>(6, default(ProblemSet).With(BuildingProblem.Crime)),
            };
            var icons = new List<ProblemIcon>();
            ProblemIconLayout.Build(buildings, id => id == 6 ? new float3(float.MaxValue) : new float3(10f, 0f, 0f), float3.zero,
                new float3(1f, 0f, 0f), 1f, 0f, 50f, 10, icons);
            Assert.That(icons.Count, Is.EqualTo(2));
        }
    }
}
