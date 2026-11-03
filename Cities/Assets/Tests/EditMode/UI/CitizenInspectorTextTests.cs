using Cities.Core.Ids;
using Cities.Presentation.UI;
using Cities.Simulation.Citizens;
using Cities.World.Buildings;
using NUnit.Framework;

namespace Cities.Tests.EditMode.UI
{
    public class CitizenInspectorTextTests
    {
        private static readonly EntityId<BuildingKind> Place = new EntityId<BuildingKind>(3, 1);

        [TestCase(5, "Kind (5 jaar)")]
        [TestCase(40, "Volwassene (40 jaar)")]
        [TestCase(70, "Gepensioneerde (70 jaar)")]
        public void LifeStageNamesTheAgeGroup(int age, string expected) =>
            Assert.That(CitizenInspectorText.LifeStage(new Citizen { Age = (byte)age }), Is.EqualTo(expected));

        [TestCase(90, "Blij")]
        [TestCase(50, "Tevreden")]
        [TestCase(10, "Ontevreden")]
        public void MoodFollowsHappiness(int happiness, string expected) =>
            Assert.That(CitizenInspectorText.Mood(new Citizen { Happiness = (byte)happiness }), Is.EqualTo(expected));

        [TestCase(0, "Laag")]
        [TestCase(1, "Middelbaar")]
        [TestCase(2, "Hoog")]
        public void EducationIsNamed(int level, string expected) =>
            Assert.That(CitizenInspectorText.Education(new Citizen { Education = (byte)level }), Is.EqualTo(expected));

        [Test]
        public void HomeAndWorkShowBuildingOrReason()
        {
            Assert.That(CitizenInspectorText.Home(new Citizen { Home = Place }), Is.EqualTo("Gebouw 3"));
            Assert.That(CitizenInspectorText.Home(default), Is.EqualTo("Dakloos"));
            Assert.That(CitizenInspectorText.Work(new Citizen { Age = 30, Work = Place }), Is.EqualTo("Gebouw 3"));
            Assert.That(CitizenInspectorText.Work(new Citizen { Age = 30 }), Is.EqualTo("Werkzoekend"));
            Assert.That(CitizenInspectorText.Work(new Citizen { Age = 10 }), Is.EqualTo(CitizenInspectorText.None));
        }

        [Test]
        public void EveryActivityAndDestinationHasText()
        {
            foreach (Activity activity in System.Enum.GetValues(typeof(Activity)))
            {
                Assert.That(CitizenInspectorText.Doing(activity), Is.Not.Empty);
            }

            foreach (Destination destination in System.Enum.GetValues(typeof(Destination)))
            {
                Assert.That(CitizenInspectorText.Route(destination), Is.Not.Empty);
            }
        }

        [Test]
        public void LinesShowWhatAWorkingAdultDoesOnAWeekdayMorning()
        {
            var worker = new Citizen { Age = 30, Home = Place, Work = Place, Household = 7, Health = 80, Happiness = 75 };
            var lines = CitizenInspectorText.Lines(worker, 1, 0, 11);
            Assert.That(lines, Does.Contain("Nu: Werkt"));
            Assert.That(lines, Does.Contain("Bestemming: Werk"));
            Assert.That(lines, Does.Contain("Stemming: Blij (75)"));
            Assert.That(lines, Does.Contain("Woning: Gebouw 3"));
        }
    }
}
