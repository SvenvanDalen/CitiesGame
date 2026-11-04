using System.Linq;
using Cities.Core.Ids;
using Cities.Simulation.Citizens;
using Cities.World.Buildings;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Citizens
{
    public class DailyScheduleTests
    {
        private static readonly EntityId<BuildingKind> Place = new EntityId<BuildingKind>(3, 1);
        private static readonly uint[] Ids = Enumerable.Range(1, 100).Select(i => (uint)i).ToArray();

        private static Citizen Person(int age, bool work = false, bool home = true) =>
            new Citizen { Age = (byte)age, Household = 7, Home = home ? Place : default, Work = work ? Place : default };

        private static void AssertForEveryone(Citizen citizen, int day, int hour, Activity expected) =>
            Assert.That(Ids.Select(id => DailySchedule.ActivityAt(citizen, id, day, hour)), Has.All.EqualTo(expected), $"dag {day} uur {hour}");

        [Test]
        public void EveryoneSleepsAtNight()
        {
            foreach (var age in new[] { 5, 40, 80 })
            {
                AssertForEveryone(Person(age, true), 1, 3, Activity.Sleep);
                AssertForEveryone(Person(age, true), 1, 0, Activity.Sleep);
            }
        }

        [Test]
        public void AWorkingAdultWorksOnWeekdaysAndRelaxesInTheWeekend()
        {
            AssertForEveryone(Person(40, true), 2, 12, Activity.Work);
            AssertForEveryone(Person(40, true), 5, 12, Activity.Leisure);
            AssertForEveryone(Person(40, true), 6, 12, Activity.Leisure);
            AssertForEveryone(Person(40, true), 7, 12, Activity.Work);
        }

        [Test]
        public void AnUnemployedAdultNeverWorks()
        {
            Assert.That(Enumerable.Range(0, 24).Select(h => DailySchedule.ActivityAt(Person(40), 1, 2, h)), Has.No.Member(Activity.Work));
        }

        [Test]
        public void ChildrenGoToSchoolOnWeekdays()
        {
            AssertForEveryone(Person(10), 0, 11, Activity.School);
            AssertForEveryone(Person(10), 0, 17, Activity.Leisure);
            AssertForEveryone(Person(10), 5, 11, Activity.Leisure);
            Assert.That(Enumerable.Range(0, 24).Select(h => DailySchedule.ActivityAt(Person(10), 1, 0, h)), Has.No.Member(Activity.Shop));
        }

        [Test]
        public void AdultsShopAfterWorkAndRetiredAlsoInTheMorning()
        {
            AssertForEveryone(Person(40, true), 1, 18, Activity.Shop);
            AssertForEveryone(Person(70), 1, 18, Activity.Shop);
            AssertForEveryone(Person(70), 1, 11, Activity.Shop);
            AssertForEveryone(Person(40, true), 1, 21, Activity.Leisure);
        }

        [Test]
        public void StartTimesAreSpreadOverThePopulationAndStablePerCitizen()
        {
            var worker = Person(40, true);

            var atEight = Ids.Select(id => DailySchedule.ActivityAt(worker, id, 1, 8)).Distinct().ToArray();

            Assert.That(atEight, Has.Length.GreaterThan(1), "niet iedereen begint op hetzelfde uur");
            Assert.That(DailySchedule.ActivityAt(worker, 9, 1, 8), Is.EqualTo(DailySchedule.ActivityAt(worker, 9, 1, 8)));
        }

        [Test]
        public void DaysAndHoursWrapAround()
        {
            Assert.That(DailySchedule.IsWeekend(5), Is.True);
            Assert.That(DailySchedule.IsWeekend(12), Is.True);
            Assert.That(DailySchedule.IsWeekend(-1), Is.True, "dag -1 is een zondag");
            Assert.That(DailySchedule.IsWeekend(-3), Is.False);
            Assert.That(DailySchedule.ActivityAt(Person(40, true), 1, 1, 24 + 3), Is.EqualTo(Activity.Sleep));
            Assert.That(DailySchedule.ActivityAt(Person(40, true), 1, 1, -21), Is.EqualTo(Activity.Sleep));
        }

        [Test]
        public void TheDestinationFollowsTheActivity()
        {
            var worker = Person(40, true);

            Assert.That(DailySchedule.DestinationFor(worker, Activity.Work), Is.EqualTo(Destination.Work));
            Assert.That(DailySchedule.DestinationFor(worker, Activity.School), Is.EqualTo(Destination.School));
            Assert.That(DailySchedule.DestinationFor(worker, Activity.Shop), Is.EqualTo(Destination.Shop));
            Assert.That(DailySchedule.DestinationFor(worker, Activity.Sleep), Is.EqualTo(Destination.Home));
            Assert.That(DailySchedule.DestinationFor(Person(40, home: false), Activity.Sleep), Is.EqualTo(Destination.Anywhere));
        }

        [Test]
        public void LeisureIsSpentAtHomeOrInThePark()
        {
            var homeless = Person(40, home: false);
            var destinations = Enumerable.Range(1, 50)
                .Select(h => DailySchedule.DestinationFor(new Citizen { Age = 40, Home = Place, Household = (uint)h }, Activity.Leisure))
                .Distinct()
                .OrderBy(d => d)
                .ToArray();

            Assert.That(destinations, Is.EqualTo(new[] { Destination.Home, Destination.Park }));
            Assert.That(DailySchedule.DestinationFor(homeless, Activity.Leisure), Is.EqualTo(Destination.Park));
        }
    }
}
