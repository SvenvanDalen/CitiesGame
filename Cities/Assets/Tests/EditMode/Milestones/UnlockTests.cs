using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;
using Cities.Simulation.Milestones;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Milestones
{
    public class UnlockTests
    {
        [Test]
        public void DefaultTableReferencesOnlyKnownMilestonesAndEveryMilestoneUnlocksSomethingUseful()
        {
            var table = UnlockCatalog.CreateDefault();

            Assert.That(table.Rules.Select(r => r.DefinitionId).Distinct().Count(), Is.EqualTo(table.Rules.Count));
            Assert.That(table.Rules.All(r => MilestoneCatalog.Find(r.MilestoneId) != null), Is.True);
            Assert.That(table.DefinitionsFor("milestone.metropolis"), Is.EqualTo(new[] { "tourist-site.attraction.monument" }));
            Assert.That(table.MilestoneFor("tool.metro-line"), Is.EqualTo("milestone.city"));
            Assert.That(table.MilestoneFor("building.house.detached"), Is.Null);
            Assert.That(table.MilestoneFor(null), Is.Null);
        }

        [Test]
        public void DefinitionWithoutRuleIsAlwaysUnlocked()
        {
            var service = new UnlockService(new MilestoneTracker());

            Assert.That(service.IsUnlocked("building.house.detached"), Is.True);
            Assert.That(service.LockedReason("building.house.detached"), Is.Null);
            Assert.That(service.RequiredMilestone("building.house.detached"), Is.Null);
        }

        [Test]
        public void DefinitionUnlocksWhenItsMilestoneIsReached()
        {
            var tracker = new MilestoneTracker();
            var service = new UnlockService(tracker);

            Assert.That(service.IsUnlocked("tool.metro-line"), Is.False);
            Assert.That(service.LockedReason("tool.metro-line"), Does.Contain("Stad van betekenis").And.Contain("25"));
            Assert.That(service.RequiredMilestone("tool.metro-line").Id, Is.EqualTo("milestone.city"));

            tracker.Update(25000, 1);

            Assert.That(service.IsUnlocked("tool.metro-line"), Is.True);
            Assert.That(service.LockedReason("tool.metro-line"), Is.Null);
            Assert.That(service.IsUnlocked("tool.airport"), Is.False);
        }

        [Test]
        public void UnlockStaysAfterPopulationShrinks()
        {
            var tracker = new MilestoneTracker();
            var service = new UnlockService(tracker);
            tracker.Update(600, 1);
            tracker.Update(10, 2);

            Assert.That(service.IsUnlocked("tool.fire-station"), Is.True);
        }

        [Test]
        public void ReachingMilestonesReportsNewlyReleasedDefinitionsOnce()
        {
            var tracker = new MilestoneTracker();
            var service = new UnlockService(tracker);
            var events = new List<string>();
            service.Unlocked += events.Add;

            var released = service.OnMilestonesAchieved(tracker.Update(100, 1));

            Assert.That(released, Is.EqualTo(service.Table.DefinitionsFor("milestone.hamlet")));
            Assert.That(events, Is.EqualTo(released));
            Assert.That(service.OnMilestonesAchieved(tracker.Update(100, 2)), Is.Empty);
            Assert.That(service.OnMilestonesAchieved(null), Is.Empty);
            Assert.That(events.Count, Is.EqualTo(released.Count));
        }

        [Test]
        public void UnlockedDefinitionsListsOnlyOpenOnesInRuleOrder()
        {
            var tracker = new MilestoneTracker();
            var service = new UnlockService(tracker);
            Assert.That(service.UnlockedDefinitions(), Is.Empty);

            tracker.Update(500, 1);

            Assert.That(service.UnlockedDefinitions(), Is.EqualTo(
                service.Table.DefinitionsFor("milestone.hamlet").Concat(service.Table.DefinitionsFor("milestone.village"))));
        }

        [Test]
        public void SandboxSwitchOpensEverythingAndReportsTheRest()
        {
            var tracker = new MilestoneTracker();
            var service = new UnlockService(tracker);
            tracker.Update(100, 1);
            var events = new List<string>();
            service.Unlocked += events.Add;

            var released = service.SetAllUnlocked(true);

            Assert.That(service.AllUnlocked, Is.True);
            Assert.That(service.IsUnlocked("tool.airport"), Is.True);
            Assert.That(released.Count, Is.EqualTo(service.Table.Rules.Count - service.Table.DefinitionsFor("milestone.hamlet").Count));
            Assert.That(events, Is.EqualTo(released));
            Assert.That(service.SetAllUnlocked(true), Is.Empty, "tweede keer meldt niets");
            Assert.That(service.OnMilestonesAchieved(tracker.Update(500, 2)), Is.Empty, "alles stond al open");

            service.SetAllUnlocked(false);
            Assert.That(service.IsUnlocked("tool.airport"), Is.False);
            Assert.That(service.IsUnlocked("tool.fire-station"), Is.True, "gehaalde mijlpaal blijft");
        }

        [Test]
        public void TableRejectsUnknownMilestoneDuplicatesAndNulls()
        {
            var milestones = MilestoneCatalog.All;

            Assert.Throws<ArgumentException>(() => new UnlockTable(new[] { new UnlockRule("a.b", "milestone.nope") }, milestones));
            Assert.Throws<ArgumentException>(() => new UnlockTable(
                new[] { new UnlockRule("a.b", "milestone.town"), new UnlockRule("a.b", "milestone.city") }, milestones));
            Assert.Throws<ArgumentNullException>(() => new UnlockTable(null, milestones));
            Assert.Throws<ArgumentNullException>(() => new UnlockTable(new UnlockRule[0], null));
            Assert.Throws<ArgumentException>(() => new UnlockRule(" ", "milestone.town"));
            Assert.Throws<ArgumentException>(() => new UnlockRule("a.b", null));
            Assert.Throws<ArgumentNullException>(() => new UnlockService(null));
            Assert.Throws<ArgumentNullException>(() => new UnlockService(new MilestoneTracker(), null));
            Assert.Throws<ArgumentNullException>(() => new UnlockSection(null));
        }

        [Test]
        public void SaveRoundTripKeepsSandboxSwitch()
        {
            var service = new UnlockService(new MilestoneTracker());
            service.SetAllUnlocked(true);
            var bytes = SaveBytes.Write(new SaveMetadata(), new UnlockSection(service));
            var loaded = new UnlockService(new MilestoneTracker());

            SaveBytes.Read(bytes, new UnlockSection(loaded));

            Assert.That(loaded.AllUnlocked, Is.True);
            Assert.That(new UnlockSection(loaded).Id, Is.EqualTo("UNLK"));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new UnlockSection(loaded)), Is.EqualTo(bytes));
        }
    }
}
