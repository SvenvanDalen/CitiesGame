using System;
using System.Linq;
using Cities.Simulation.Milestones;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Milestones
{
    public class MilestoneAnnouncementTests
    {
        private static readonly UnlockTable Table = UnlockCatalog.CreateDefault();

        [Test]
        public void GroupsWhatIsNewPerKindInAFixedOrder()
        {
            var announcement = MilestoneAnnouncement.Create(MilestoneCatalog.Find("milestone.small-town"), Table);

            Assert.That(announcement.Groups.Select(g => g.Category), Is.EqualTo(new[] { "Tools", "Wegen", "Voertuigen", "Beleid" }));
            Assert.That(announcement.NewCount, Is.EqualTo(Table.DefinitionsFor("milestone.small-town").Count));
            Assert.That(announcement.Groups[0].Names, Is.EqualTo(new[] { "Bus line" }));
        }

        [Test]
        public void UsesTheDisplayNameWhenItIsGivenAndFallsBackOtherwise()
        {
            var announcement = MilestoneAnnouncement.Create(
                MilestoneCatalog.Find("milestone.village"),
                Table,
                id => id == "tool.fire-station" ? "Brandweerkazerne" : null);

            var tools = announcement.Groups.Single(g => g.Category == "Tools");
            Assert.That(tools.Names, Is.EquivalentTo(new[] { "Brandweerkazerne", "Primary school" }));
        }

        [Test]
        public void BodyExplainsRewardAndNewContent()
        {
            var milestone = MilestoneCatalog.Find("milestone.village");

            var announcement = MilestoneAnnouncement.Create(milestone, Table);

            Assert.That(announcement.Title, Is.EqualTo("Dorp bereikt"));
            Assert.That(announcement.Body, Does.StartWith(milestone.Message));
            Assert.That(announcement.Body, Does.Contain("Nieuw beschikbaar:"));
            Assert.That(announcement.Body, Does.Contain("Tools: Fire station, Primary school"));
            Assert.That(announcement.Body, Does.Contain("Voertuigen: Emergency van"));
        }

        [Test]
        public void AMilestoneWithoutUnlocksSaysSo()
        {
            var announcement = MilestoneAnnouncement.Create(MilestoneCatalog.Find("milestone.megacity"), Table);

            Assert.That(announcement.Groups, Is.Empty);
            Assert.That(announcement.NewCount, Is.Zero);
            Assert.That(announcement.Body, Does.Contain("Er komt niets nieuws beschikbaar."));
        }

        [Test]
        public void UnknownKindsGetTheirOwnGroupAfterTheKnownOnes()
        {
            var milestones = new[] { new PopulationMilestone("milestone.a", "A", 10, 0, string.Empty) };
            var table = new UnlockTable(
                new[] { new UnlockRule("zzz.thing", "milestone.a"), new UnlockRule("tool.saw", "milestone.a") },
                milestones);

            var announcement = MilestoneAnnouncement.Create(milestones[0], table);

            Assert.That(announcement.Groups.Select(g => g.Category), Is.EqualTo(new[] { "Tools", "Zzz" }));
        }

        [Test]
        public void CreateAllBuildsOneAnnouncementPerAchievement()
        {
            var tracker = new MilestoneTracker();
            var fresh = tracker.Update(600, 5);

            var all = MilestoneAnnouncement.CreateAll(fresh, Table);

            Assert.That(all.Select(a => a.Milestone.Id), Is.EqualTo(new[] { "milestone.hamlet", "milestone.village" }));
        }

        [Test]
        public void RejectsMissingArguments()
        {
            Assert.Throws<ArgumentNullException>(() => MilestoneAnnouncement.Create(null, Table));
            Assert.Throws<ArgumentNullException>(() => MilestoneAnnouncement.Create(MilestoneCatalog.All[0], null));
            Assert.Throws<ArgumentNullException>(() => MilestoneAnnouncement.CreateAll(null, Table));
        }
    }
}
