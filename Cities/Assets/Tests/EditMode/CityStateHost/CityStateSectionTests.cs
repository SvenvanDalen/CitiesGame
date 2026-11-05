using System;
using System.Linq;
using Cities.Core.Persistence;
using Cities.Gameplay.Persistence;
using Cities.Gameplay.Services;
using Cities.Gameplay.Zoning;
using Cities.Simulation.Industry;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.CityStateHost
{
    public class CityStateSectionTests
    {
        private static CityState Sample()
        {
            var state = new CityState { Balance = 1_234_567, Population = 840, Beds = 80 };
            state.Buildings.Add(new BuiltBuilding("building.house", 4, 8, 2, 3, 1, 10.5f, 2f, 20.25f));
            state.TransitStops.Add(new float2(1, 2));
            state.TransitStops.Add(new float2(100, 200));
            state.Lines.Add(new SavedLine("Lijn 1", new[] { 0, 1 }, false, 3, 90f));
            state.Services.Add(new SavedService(CityService.Health, new float2(5, 6)));
            state.Sites.Add(new SavedSite(IndustryKind.Ore, 2, 1.5f));
            state.Attractions.Add(new SavedAttraction("Museum", 60, 8, 400));
            return state;
        }

        [Test]
        public void RoundTripKeepsEverything()
        {
            var original = Sample();
            var bytes = SaveBytes.Write(new SaveMetadata(), new CityStateSection(() => original, _ => { }));
            CityState loaded = null;

            SaveBytes.Read(bytes, new CityStateSection(() => original, s => loaded = s));

            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.Balance, Is.EqualTo(1_234_567));
            Assert.That(loaded.Population, Is.EqualTo(840));
            Assert.That(loaded.Beds, Is.EqualTo(80));
            Assert.That(loaded.Buildings.Single().DefinitionId, Is.EqualTo("building.house"));
            Assert.That(loaded.Buildings.Single().QuarterTurns, Is.EqualTo(1));
            Assert.That(loaded.Buildings.Single().Z, Is.EqualTo(20.25f));
            Assert.That(loaded.TransitStops.Count, Is.EqualTo(2));
            Assert.That(loaded.Lines.Single().StopIndices, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(loaded.Services.Single().Kind, Is.EqualTo(CityService.Health));
            Assert.That(loaded.Sites.Single().Kind, Is.EqualTo(IndustryKind.Ore));
            Assert.That(loaded.Attractions.Single().Name, Is.EqualTo("Museum"));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new CityStateSection(() => loaded, _ => { })), Is.EqualTo(bytes));
        }

        [Test]
        public void ALineThatPointsAtAMissingStopIsRejected()
        {
            var bad = Sample();
            bad.Lines.Clear();
            bad.Lines.Add(new SavedLine("Kapot", new[] { 0, 7 }, false, 1, 60f));
            var bytes = SaveBytes.Write(new SaveMetadata(), new CityStateSection(() => bad, _ => { }));
            var applied = false;

            Assert.Throws<SaveFormatException>(() => SaveBytes.Read(bytes, new CityStateSection(() => bad, _ => applied = true)));
            Assert.That(applied, Is.False);
        }

        [Test]
        public void EmptyStateRoundTrips()
        {
            var empty = new CityState();
            var bytes = SaveBytes.Write(new SaveMetadata(), new CityStateSection(() => empty, _ => { }));
            CityState loaded = null;

            SaveBytes.Read(bytes, new CityStateSection(() => empty, s => loaded = s));

            Assert.That(loaded.Buildings, Is.Empty);
            Assert.That(loaded.Balance, Is.Zero);
        }

        [Test]
        public void NullDelegatesAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new CityStateSection(null, _ => { }));
            Assert.Throws<ArgumentNullException>(() => new CityStateSection(() => new CityState(), null));
            Assert.That(new CityStateSection(() => new CityState(), _ => { }).Id, Is.EqualTo("CITY"));
        }
    }
}
