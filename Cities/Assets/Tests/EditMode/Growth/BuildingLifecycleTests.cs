using Cities.Simulation.Growth;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Growth
{
    public class BuildingLifecycleTests
    {
        private static void Run(ref BuildingLifecycle life, int days, bool inUse)
        {
            for (var i = 0; i < days; i++)
            {
                life.Step(inUse);
            }
        }

        [Test]
        public void AnOccupiedBuildingStaysActive()
        {
            var life = new BuildingLifecycle();

            Run(ref life, 1000, true);

            Assert.That(life.Phase, Is.EqualTo(BuildingPhase.Active));
        }

        [Test]
        public void AnEmptyBuildingIsAbandonedThenDemolishedThenCleared()
        {
            var life = new BuildingLifecycle();

            Run(ref life, BuildingLifecycle.AbandonAfterDays - 1, false);
            Assert.That(life.Phase, Is.EqualTo(BuildingPhase.Active));
            Run(ref life, 1, false);
            Assert.That(life.Phase, Is.EqualTo(BuildingPhase.Abandoned));
            Run(ref life, BuildingLifecycle.DemolishAfterDays, false);
            Assert.That(life.Phase, Is.EqualTo(BuildingPhase.Rubble));
            Assert.That(life.HasRubble, Is.True);
            Assert.That(life.IsBuildable, Is.False, "puin blokkeert de kavel");
            Run(ref life, BuildingLifecycle.ClearAfterDays, false);
            Assert.That(life.Phase, Is.EqualTo(BuildingPhase.Cleared));
            Assert.That(life.IsBuildable, Is.True);
            Run(ref life, 100, false);
            Assert.That(life.Phase, Is.EqualTo(BuildingPhase.Cleared), "blijft opgeruimd");
        }

        [Test]
        public void ABriefVacancyResetsWhenSomeoneMovesIn()
        {
            var life = new BuildingLifecycle();

            Run(ref life, BuildingLifecycle.AbandonAfterDays - 1, false);
            life.Step(true);
            Run(ref life, BuildingLifecycle.AbandonAfterDays - 1, false);

            Assert.That(life.Phase, Is.EqualTo(BuildingPhase.Active), "de teller begon opnieuw");
        }

        [Test]
        public void AnAbandonedBuildingComesBackWhenSomeoneMovesIn()
        {
            var life = new BuildingLifecycle();
            Run(ref life, BuildingLifecycle.AbandonAfterDays, false);
            Assert.That(life.Phase, Is.EqualTo(BuildingPhase.Abandoned));

            life.Step(true);

            Assert.That(life.Phase, Is.EqualTo(BuildingPhase.Active));
            Run(ref life, BuildingLifecycle.AbandonAfterDays - 1, false);
            Assert.That(life.Phase, Is.EqualTo(BuildingPhase.Active), "weer vanaf nul geteld");
        }

        [Test]
        public void RubbleDoesNotReturnToUseWhenSomeoneWouldMoveIn()
        {
            var life = new BuildingLifecycle();
            Run(ref life, BuildingLifecycle.AbandonAfterDays + BuildingLifecycle.DemolishAfterDays, false);
            Assert.That(life.Phase, Is.EqualTo(BuildingPhase.Rubble));

            life.Step(true);

            Assert.That(life.Phase, Is.EqualTo(BuildingPhase.Rubble));
        }
    }
}
