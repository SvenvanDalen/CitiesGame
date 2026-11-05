using System.Linq;
using Cities.Gameplay.Events;
using Cities.Simulation.Economy;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.EventsHost
{
    public class CityEventsTests
    {
        private static readonly float2 Area = new float2(4000f, 4000f);

        [Test]
        public void ReachingAMilestoneRewardsCashAndPostsAMessage()
        {
            var events = new CityEvents(1);
            events.Disasters.RandomEnabled = false;
            var ledger = new Ledger(0);

            events.Daily(1, 1, 100_000, ledger, Area);

            Assert.That(events.Milestones.Achieved, Is.Not.Empty);
            Assert.That(ledger.Balance, Is.GreaterThan(0));
            Assert.That(events.Notifications.Items, Is.Not.Empty);
        }

        [Test]
        public void MilestoneIsOnlyRewardedOnce()
        {
            var events = new CityEvents(1);
            events.Disasters.RandomEnabled = false;
            var ledger = new Ledger(0);
            events.Daily(1, 1, 5_000, ledger, Area);
            var balance = ledger.Balance;

            events.Daily(2, 1, 5_000, ledger, Area);

            Assert.That(ledger.Balance, Is.EqualTo(balance));
        }

        [Test]
        public void TriggeredDisasterCostsMoneyAndIsAnnounced()
        {
            var events = new CityEvents(1);
            events.Disasters.RandomEnabled = false;
            var ledger = new Ledger(10_000_000);
            events.Disasters.Trigger("Aardbeving", new float2(100, 100));

            events.Daily(3, 1, 0, ledger, Area);

            Assert.That(events.TotalDamage, Is.GreaterThan(0));
            Assert.That(ledger.Balance, Is.LessThan(10_000_000));
            Assert.That(events.Latest(5).Any(n => n.Text.Contains("Aardbeving")));
        }

        [Test]
        public void LatestReturnsNewestFirstAndRespectsCount()
        {
            var events = new CityEvents(1);
            events.Disasters.RandomEnabled = false;
            events.Daily(1, 1, 1_000_000, new Ledger(0), Area);

            var latest = events.Latest(1);

            Assert.That(latest.Count, Is.EqualTo(1));
        }

        [Test]
        public void RandomDisastersAreReproducibleForTheSameSeed()
        {
            string Run()
            {
                var events = new CityEvents(77);
                var ledger = new Ledger(0);
                for (var day = 1; day <= 400; day++)
                {
                    events.Daily(day, 1 + (day / 7), 0, ledger, Area);
                }

                return events.TotalDamage + "|" + events.Notifications.Items.Count;
            }

            Assert.That(Run(), Is.EqualTo(Run()));
        }
    }
}
