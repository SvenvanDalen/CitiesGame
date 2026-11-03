using System;
using Cities.Simulation.Services;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Services
{
    public class CultureVenueTests
    {
        [Test]
        public void AVisitInRangeGivesHappinessUntilTheVenueIsFull()
        {
            var venue = new CultureVenue(CultureKind.Theatre, float2.zero, 100f, 2, 0.2f, 0, 0);

            Assert.That(venue.Visit(new float2(50f, 0f)), Is.EqualTo(0.2f));
            Assert.That(venue.Visit(new float2(500f, 0f)), Is.Zero, "buiten bereik");
            Assert.That(venue.Visit(float2.zero), Is.EqualTo(0.2f));
            Assert.That(venue.IsFull, Is.True);
            Assert.That(venue.Visit(float2.zero), Is.Zero, "vol");
            Assert.That(venue.Visits, Is.EqualTo(2));
        }

        [Test]
        public void ANewDayEmptiesTheCounter()
        {
            var venue = CultureVenue.Library(float2.zero);
            venue.Visit(float2.zero);

            venue.NewDay();

            Assert.That(venue.Visits, Is.Zero);
        }

        [Test]
        public void ThePresetsDifferInKindAndOnlyAttractionsBringTourists()
        {
            Assert.That(CultureVenue.Library(float2.zero).Kind, Is.EqualTo(CultureKind.Library));
            Assert.That(CultureVenue.Theatre(float2.zero).Kind, Is.EqualTo(CultureKind.Theatre));
            Assert.That(CultureVenue.Library(float2.zero).TouristsPerDay, Is.Zero);
            Assert.That(CultureVenue.Theatre(float2.zero).TouristsPerDay, Is.Zero);
            Assert.That(CultureVenue.Museum(float2.zero).TouristsPerDay, Is.GreaterThan(0));
            Assert.That(CultureVenue.Landmark(float2.zero).TouristsPerDay, Is.GreaterThan(CultureVenue.Museum(float2.zero).TouristsPerDay));
            Assert.That(CultureVenue.Landmark(float2.zero).Radius, Is.GreaterThan(CultureVenue.Library(float2.zero).Radius));
        }

        [Test]
        public void BadInputIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CultureVenue(CultureKind.Library, float2.zero, 0f, 1, 0f, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CultureVenue(CultureKind.Library, float2.zero, 1f, 0, 0f, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CultureVenue(CultureKind.Library, float2.zero, 1f, 1, -1f, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CultureVenue(CultureKind.Library, float2.zero, 1f, 1, 0f, -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CultureVenue(CultureKind.Library, float2.zero, 1f, 1, 0f, 0, -1));
        }
    }
}
