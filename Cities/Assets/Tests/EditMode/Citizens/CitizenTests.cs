using System.Collections.Generic;
using System.Runtime.InteropServices;
using Cities.Core.Ids;
using Cities.Simulation.Citizens;
using Cities.World.Buildings;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Citizens
{
    public class CitizenTests
    {
        private static readonly EntityId<BuildingKind> Building = new EntityId<BuildingKind>(3, 1);

        private static List<string> Errors(Citizen citizen)
        {
            var errors = new List<string>();
            citizen.Validate(errors);
            return errors;
        }

        [Test]
        public void ACitizenStaysSmall()
        {
            Assert.That(Marshal.SizeOf<Citizen>(), Is.LessThanOrEqualTo(24), "honderdduizenden burgers moeten in het geheugen passen");
        }

        [Test]
        public void DefaultIsANewbornWithoutHouseholdHomeOrWork()
        {
            var citizen = default(Citizen);

            Assert.That(citizen.Age, Is.Zero);
            Assert.That(citizen.HasHome, Is.False);
            Assert.That(citizen.HasWork, Is.False);
            Assert.That(citizen.HasHousehold, Is.False);
            Assert.That(citizen.Group, Is.EqualTo(AgeGroup.Child));
            Assert.That(Errors(citizen), Is.Empty);
        }

        [TestCase(0, AgeGroup.Child)]
        [TestCase(17, AgeGroup.Child)]
        [TestCase(18, AgeGroup.Adult)]
        [TestCase(66, AgeGroup.Adult)]
        [TestCase(67, AgeGroup.Retired)]
        [TestCase(120, AgeGroup.Retired)]
        public void AgeDecidesTheGroup(int age, AgeGroup expected)
        {
            Assert.That(new Citizen { Age = (byte)age }.Group, Is.EqualTo(expected));
        }

        [Test]
        public void OnlyAdultsWithoutWorkSeekJobs()
        {
            Assert.That(new Citizen { Age = 30 }.IsJobSeeker, Is.True);
            Assert.That(new Citizen { Age = 30, Work = Building }.IsJobSeeker, Is.False);
            Assert.That(new Citizen { Age = 10 }.IsJobSeeker, Is.False);
            Assert.That(new Citizen { Age = 70 }.IsJobSeeker, Is.False);
        }

        [Test]
        public void HomeWorkAndHouseholdAreReported()
        {
            var citizen = new Citizen { Age = 30, Home = Building, Work = Building, Household = 7 };

            Assert.That(citizen.HasHome, Is.True);
            Assert.That(citizen.HasWork, Is.True);
            Assert.That(citizen.HasHousehold, Is.True);
            Assert.That(Errors(citizen), Is.Empty);
        }

        [Test]
        public void ValuesOutOfRangeAreReported()
        {
            var errors = Errors(new Citizen { Age = 200, Education = 3, Health = 101, Happiness = 255 });

            Assert.That(errors, Has.Count.EqualTo(4));
            Assert.That(errors, Has.Some.Contains("Leeftijd"));
            Assert.That(errors, Has.Some.Contains("Opleiding"));
            Assert.That(errors, Has.Some.Contains("Gezondheid"));
            Assert.That(errors, Has.Some.Contains("Geluk"));
        }

        [Test]
        public void ChildrenAndRetireesCannotHaveWork()
        {
            Assert.That(Errors(new Citizen { Age = 12, Work = Building }), Has.Some.Contains("werk"));
            Assert.That(Errors(new Citizen { Age = 70, Work = Building }), Has.Some.Contains("werk"));
        }

        [Test]
        public void EducationMatchesTheBuildingEducationLevels()
        {
            Assert.That((int)EducationLevel.Low, Is.EqualTo(0));
            Assert.That((int)EducationLevel.Medium, Is.EqualTo(1));
            Assert.That((int)EducationLevel.High, Is.EqualTo(Citizen.MaxEducation));
        }
    }
}
