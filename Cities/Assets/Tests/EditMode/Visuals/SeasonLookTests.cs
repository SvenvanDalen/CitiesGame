using System;
using Cities.Gameplay.Visuals;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Visuals
{
    public class SeasonLookTests
    {
        private static readonly SeasonLook Look = new SeasonLook(9UL);

        [Test]
        public void MonthsMapToSeasons()
        {
            Assert.That(SeasonLook.SeasonOf(1), Is.EqualTo(Season.Winter));
            Assert.That(SeasonLook.SeasonOf(12), Is.EqualTo(Season.Winter));
            Assert.That(SeasonLook.SeasonOf(4), Is.EqualTo(Season.Spring));
            Assert.That(SeasonLook.SeasonOf(7), Is.EqualTo(Season.Summer));
            Assert.That(SeasonLook.SeasonOf(10), Is.EqualTo(Season.Autumn));
            Assert.Throws<ArgumentOutOfRangeException>(() => SeasonLook.SeasonOf(0));
        }

        [Test]
        public void FoliageIsFullInSummerAndBareInWinter()
        {
            Assert.That(Look.Foliage(190, 2030), Is.EqualTo(1f).Within(0.001f));
            Assert.That(Look.Foliage(20, 2030), Is.EqualTo(0f).Within(0.001f));
            Assert.That(Look.Foliage(110, 2030), Is.InRange(0.01f, 0.99f));
            Assert.That(Look.Foliage(290, 2030), Is.InRange(0.01f, 0.99f));
        }

        [Test]
        public void AutumnTintPeaksBetweenSummerAndBareWinter()
        {
            Assert.That(Look.AutumnTint(150, 2030), Is.EqualTo(0f));
            Assert.That(Look.AutumnTint(300, 2030), Is.GreaterThan(0.8f));
            Assert.That(Look.AutumnTint(20, 2030), Is.EqualTo(0f));
        }

        [Test]
        public void SnowOnlyLiesInWinterAndStaysThin()
        {
            Assert.That(Look.SnowCover(180, 2030), Is.EqualTo(0f));
            Assert.That(Look.SnowCover(15, 2030), Is.GreaterThan(0.5f));
            for (var day = 1; day <= 366; day++)
            {
                Assert.That(Look.SnowCover(day, 2031), Is.InRange(0f, SeasonLook.MaxSnow));
            }
        }

        [Test]
        public void YearsShiftALittleAndDeterministically()
        {
            for (var year = 2025; year < 2045; year++)
            {
                Assert.That(Math.Abs(Look.ShiftDays(year)), Is.LessThanOrEqualTo(SeasonLook.MaxShiftDays));
                Assert.That(new SeasonLook(9UL).ShiftDays(year), Is.EqualTo(Look.ShiftDays(year)));
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => Look.Foliage(0, 2030));
        }
    }
}
