using System;
using Cities.Core.Ids;
using Cities.Simulation.Citizens;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Citizens
{
    public class JobMatcherTests
    {
        private static readonly EntityId<BuildingKind> Near = new EntityId<BuildingKind>(1, 1);
        private static readonly EntityId<BuildingKind> Far = new EntityId<BuildingKind>(2, 1);

        private static Vacancy Job(EntityId<BuildingKind> id, float x, int slots, byte min = 0) =>
            new Vacancy { Building = id, Position = new float2(x, 0f), Slots = slots, MinEducation = min };

        [Test]
        public void ASeekerTakesTheNearestVacancy()
        {
            var people = new[] { new Citizen { Age = 30 } };
            var jobs = new[] { Job(Far, 900f, 1), Job(Near, 100f, 1) };

            var placed = JobMatcher.Match(people, new[] { float2.zero }, jobs);

            Assert.That(placed, Is.EqualTo(1));
            Assert.That(people[0].Work, Is.EqualTo(Near));
            Assert.That(jobs[1].Slots, Is.Zero);
        }

        [Test]
        public void TheBestEducatedChooseFirstAndEducationIsRequired()
        {
            var people = new[] { new Citizen { Age = 30, Education = 0 }, new Citizen { Age = 30, Education = 2 } };
            var jobs = new[] { Job(Near, 10f, 1, 2), Job(Far, 20f, 1) };

            JobMatcher.Match(people, new[] { float2.zero, float2.zero }, jobs);

            Assert.That(people[1].Work, Is.EqualTo(Near), "alleen hij mag de hoge baan");
            Assert.That(people[0].Work, Is.EqualTo(Far));
        }

        [Test]
        public void TooFarFullChildrenAndWorkersAreSkipped()
        {
            var people = new[] { new Citizen { Age = 10 }, new Citizen { Age = 30, Work = Far }, new Citizen { Age = 30 } };
            var jobs = new[] { Job(Near, JobMatcher.MaxDistance + 1f, 5), Job(Far, 1f, 0) };

            var placed = JobMatcher.Match(people, new float2[3], jobs);

            Assert.That(placed, Is.Zero);
            Assert.That(people[2].HasWork, Is.False);
        }

        [Test]
        public void UnemploymentCountsOnlyAdults()
        {
            var people = new[] { new Citizen { Age = 5 }, new Citizen { Age = 30, Work = Near }, new Citizen { Age = 30 }, new Citizen { Age = 80 } };

            Assert.That(JobMatcher.UnemploymentRate(people), Is.EqualTo(0.5f));
            Assert.That(JobMatcher.UnemploymentRate(Array.Empty<Citizen>()), Is.Zero);
        }

        [Test]
        public void MismatchedHomesAreRejected()
        {
            Assert.Throws<ArgumentException>(() => JobMatcher.Match(new Citizen[2], new float2[1], Array.Empty<Vacancy>()));
        }
    }
}
