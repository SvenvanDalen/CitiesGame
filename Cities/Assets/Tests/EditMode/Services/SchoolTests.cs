using System;
using Cities.Simulation.Citizens;
using Cities.Simulation.Services;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Services
{
    public class SchoolTests
    {
        [Test]
        public void TheSchoolTakesPupilsInRangeUntilItIsFull()
        {
            var school = new School(SchoolLevel.Basic, float2.zero, 100f, 2, 0);

            Assert.That(school.TryEnroll(new float2(50f, 0f)), Is.True);
            Assert.That(school.TryEnroll(new float2(500f, 0f)), Is.False, "buiten bereik");
            Assert.That(school.TryEnroll(float2.zero), Is.True);
            Assert.That(school.IsFull, Is.True);
            Assert.That(school.TryEnroll(float2.zero), Is.False, "vol");
        }

        [Test]
        public void GraduatesGetTheLevelOfTheSchoolAndDropoutsDoNot()
        {
            var school = School.HighSchool(float2.zero);
            var graduate = new Citizen { Age = 17 };
            var dropout = new Citizen { Age = 17 };
            school.TryEnroll(float2.zero);
            school.TryEnroll(float2.zero);

            school.Leave(ref graduate, true);
            school.Leave(ref dropout, false);

            Assert.That(graduate.Education, Is.EqualTo((byte)SchoolLevel.Secondary));
            Assert.That(dropout.Education, Is.Zero);
            Assert.That(school.Enrolled, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => school.Leave(ref dropout, false));
        }

        [TestCase(5, 0, null)]
        [TestCase(6, 0, SchoolLevel.Basic)]
        [TestCase(12, 0, SchoolLevel.Secondary)]
        [TestCase(17, 2, null)]
        [TestCase(18, 0, null)]
        [TestCase(18, 1, SchoolLevel.University)]
        [TestCase(26, 1, null)]
        public void TheAgeAndEducationDecideWhichSchoolFits(int age, int education, SchoolLevel? expected)
        {
            var citizen = new Citizen { Age = (byte)age, Education = (byte)education };

            Assert.That(School.LevelFor(citizen), Is.EqualTo(expected));
        }

        [Test]
        public void BiggerSchoolsReachFurtherAndHoldMore()
        {
            Assert.That(School.College(float2.zero).Radius, Is.GreaterThan(School.HighSchool(float2.zero).Radius));
            Assert.That(School.HighSchool(float2.zero).Capacity, Is.GreaterThan(School.Elementary(float2.zero).Capacity));
        }

        [Test]
        public void BadInputIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new School(SchoolLevel.Basic, float2.zero, 0f, 1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new School(SchoolLevel.Basic, float2.zero, 1f, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new School(SchoolLevel.Basic, float2.zero, 1f, 1, -1));
        }
    }
}
