using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;
using Cities.Simulation.TouristSites;
using Cities.Tests.EditMode.Persistence;
using Cities.World.Tourism;
using NUnit.Framework;

namespace Cities.Tests.EditMode.TouristSites
{
    public sealed class TouristSiteTests
    {
        private static TouristSiteProfile Attraction(float value = 50f) => new TouristSiteProfile(TouristSiteKind.Attraction, value, 0, 500f);

        private static TouristSiteProfile Hotel(int beds = 100) => new TouristSiteProfile(TouristSiteKind.Hotel, 20f, beds, 300f);

        private static TouristSiteProfile Beach() => new TouristSiteProfile(TouristSiteKind.Beach, 60f, 0, 800f);

        private static TouristSiteProfile Route() => new TouristSiteProfile(TouristSiteKind.WalkingRoute, 40f, 0, 600f);

        private static TouristSite Site(TouristSiteProfile profile, int id = 1, int x = 0, int y = 0) =>
            new TouristSite(id, "tourist-site.test", profile, x, y, TouristSiteConditions.Neutral);

        [Test]
        public void AProfileRejectsImpossibleValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TouristSiteProfile((TouristSiteKind)9, 10f, 0, 100f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TouristSiteProfile(TouristSiteKind.Attraction, 101f, 0, 100f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TouristSiteProfile(TouristSiteKind.Attraction, float.NaN, 0, 100f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TouristSiteProfile(TouristSiteKind.Hotel, 10f, -1, 100f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TouristSiteProfile(TouristSiteKind.Hotel, 10f, TouristSiteProfile.MaxBeds + 1, 100f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TouristSiteProfile(TouristSiteKind.Attraction, 10f, 0, 0f));
            Assert.Throws<ArgumentException>(() => new TouristSiteProfile(TouristSiteKind.Hotel, 10f, 0, 100f));
            Assert.Throws<ArgumentException>(() => new TouristSiteProfile(TouristSiteKind.Beach, 10f, 5, 100f));
        }

        [Test]
        public void ConditionsAndSitesRejectImpossibleValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TouristSiteConditions(-0.1f, 0f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TouristSiteConditions(0f, 1.1f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TouristSiteConditions(0f, 0f, float.NaN, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TouristSiteConditions(0f, 0f, 0f, 2f));
            Assert.Throws<ArgumentException>(() => new TouristSite(1, " ", Attraction(), 0, 0, TouristSiteConditions.Neutral));
        }

        [Test]
        public void PollutionAndNoiseReduceAttractiveness()
        {
            var clean = TouristAppeal.Effective(Attraction(), TouristSiteConditions.Neutral);
            var dirty = TouristAppeal.Effective(Attraction(), new TouristSiteConditions(1f, 0f, 0.5f, 1f));
            var loud = TouristAppeal.Effective(Attraction(), new TouristSiteConditions(0f, 1f, 0.5f, 1f));

            Assert.That(clean, Is.EqualTo(50f).Within(0.001f));
            Assert.That(dirty, Is.EqualTo(50f * 0.3f).Within(0.001f));
            Assert.That(loud, Is.EqualTo(25f).Within(0.001f));
        }

        [Test]
        public void ABeachFollowsTheSunAndARouteFollowsTheScenery()
        {
            var dull = new TouristSiteConditions(0f, 0f, 0f, 0f);

            Assert.That(TouristAppeal.Effective(Beach(), dull), Is.EqualTo(60f * 0.3f).Within(0.001f));
            Assert.That(TouristAppeal.Effective(Beach(), TouristSiteConditions.Neutral), Is.EqualTo(60f).Within(0.001f));
            Assert.That(TouristAppeal.Effective(Route(), dull), Is.EqualTo(40f * 0.6f).Within(0.001f));
            Assert.That(TouristAppeal.Effective(Route(), new TouristSiteConditions(0f, 0f, 1f, 0f)), Is.EqualTo(40f).Within(0.001f));
        }

        [Test]
        public void NothingIsNotAttractive()
        {
            var result = TouristAppeal.ForCity(Array.Empty<TouristSite>());

            Assert.That(result.Score, Is.EqualTo(0f));
            Assert.That(result.Beds, Is.EqualTo(0));
            Assert.That(result.Sites, Is.EqualTo(0));
        }

        [Test]
        public void MoreAttractionsRaiseTheScoreWithDiminishingReturns()
        {
            float Score(int n) => TouristAppeal.ForCity(Enumerable.Range(1, n).Select(i => Site(Attraction(), i))).Score;

            Assert.That(Score(2), Is.GreaterThan(Score(1)));
            Assert.That(Score(3) - Score(2), Is.LessThan(Score(2) - Score(1)));
            Assert.That(Score(50), Is.LessThan(TouristSiteProfile.MaxAttractiveness));
        }

        [Test]
        public void AVarietyOfKindsBeatsMoreOfTheSame()
        {
            var same = TouristAppeal.ForCity(new[] { Site(Attraction(), 1), Site(Attraction(), 2) }).Score;
            var mixed = TouristAppeal.ForCity(new[] { Site(Attraction(), 1), Site(new TouristSiteProfile(TouristSiteKind.Beach, 50f, 0, 500f), 2) }).Score;

            Assert.That(mixed, Is.GreaterThan(same));
        }

        [Test]
        public void TheOrderOfTheSitesDoesNotMatter()
        {
            var sites = new[] { Site(Attraction(30f), 1), Site(Attraction(70f), 2), Site(Hotel(), 3), Site(Beach(), 4) };

            Assert.That(TouristAppeal.ForCity(sites.Reverse()).Score, Is.EqualTo(TouristAppeal.ForCity(sites).Score));
        }

        [Test]
        public void HotelsAddBedsAndAppealAtAPlaceFallsOffWithDistance()
        {
            var sites = new[] { Site(Hotel(80), 1, 0, 0), Site(Attraction(), 2, 0, 0) };

            var near = TouristAppeal.At(sites, 0, 0, 8f);
            var middle = TouristAppeal.At(sites, 20, 0, 8f);
            var far = TouristAppeal.At(sites, 200, 0, 8f);

            Assert.That(near.Beds, Is.EqualTo(80));
            Assert.That(near.Score, Is.GreaterThan(middle.Score));
            Assert.That(middle.Sites, Is.EqualTo(1));
            Assert.That(far.Score, Is.EqualTo(0f));
            Assert.That(far.Sites, Is.EqualTo(0));
        }

        [Test]
        public void ArgumentsAreChecked()
        {
            Assert.Throws<ArgumentNullException>(() => TouristAppeal.ForCity(null));
            Assert.Throws<ArgumentNullException>(() => TouristAppeal.At(null, 0, 0, 8f));
            Assert.Throws<ArgumentOutOfRangeException>(() => TouristAppeal.At(Array.Empty<TouristSite>(), 0, 0, 0f));
            Assert.Throws<ArgumentNullException>(() => new TouristSitesSection(null));
        }

        [Test]
        public void TheRegistryAddsRemovesAndUpdates()
        {
            var registry = new TouristSiteRegistry();
            var hotel = registry.Add("tourist-site.hotel", Hotel(60), 3, 4, TouristSiteConditions.Neutral);
            var beach = registry.Add("tourist-site.beach", Beach(), 5, 6, TouristSiteConditions.Neutral);

            Assert.That(registry.Count, Is.EqualTo(2));
            Assert.That(registry.TotalBeds, Is.EqualTo(60));
            Assert.That(registry.CityAppeal().Score, Is.GreaterThan(0f));
            Assert.That(registry.AppealAt(5, 6, 8f).Score, Is.GreaterThan(0f));
            Assert.That(registry.TryGet(beach, out var found) && found.X == 5, Is.True);

            var before = registry.CityAppeal().Score;
            Assert.That(registry.SetConditions(beach, new TouristSiteConditions(1f, 1f, 0f, 0f)), Is.True);
            Assert.That(registry.CityAppeal().Score, Is.LessThan(before));
            Assert.That(registry.SetConditions(99, TouristSiteConditions.Neutral), Is.False);

            Assert.That(registry.Remove(hotel), Is.True);
            Assert.That(registry.Remove(hotel), Is.False);
            Assert.That(registry.TryGet(hotel, out _), Is.False);
            Assert.That(registry.TotalBeds, Is.EqualTo(0));
            Assert.That(registry.Add("tourist-site.route", Route(), 0, 0, TouristSiteConditions.Neutral), Is.EqualTo(3), "ID's worden niet hergebruikt");
        }

        [Test]
        public void TheSectionRoundTrips()
        {
            var registry = new TouristSiteRegistry();
            registry.Add("tourist-site.hotel", Hotel(60), 3, 4, new TouristSiteConditions(0.2f, 0.1f, 0.5f, 0.9f));
            registry.Remove(registry.Add("tourist-site.tijdelijk", Attraction(), 0, 0, TouristSiteConditions.Neutral));
            registry.Add("tourist-site.beach", Beach(), 5, 6, TouristSiteConditions.Neutral);
            var bytes = SaveBytes.Write(new SaveMetadata(), new TouristSitesSection(registry));
            var loaded = new TouristSiteRegistry();
            loaded.Add("tourist-site.weg", Attraction(), 1, 1, TouristSiteConditions.Neutral);

            SaveBytes.Read(bytes, new TouristSitesSection(loaded));

            Assert.That(loaded.Sites.Select(s => s.DefinitionId).ToArray(), Is.EqualTo(new[] { "tourist-site.hotel", "tourist-site.beach" }));
            Assert.That(loaded.NextId, Is.EqualTo(registry.NextId));
            Assert.That(loaded.CityAppeal().Score, Is.EqualTo(registry.CityAppeal().Score));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new TouristSitesSection(loaded)), Is.EqualTo(bytes));
            Assert.That(new TouristSitesSection(registry).Id, Is.EqualTo("TRST"));
        }

        [Test]
        public void ACorruptSaveIsRejectedAndChangesNothing()
        {
            var registry = new TouristSiteRegistry();
            registry.Add("tourist-site.blijft", Attraction(), 0, 0, TouristSiteConditions.Neutral);
            var section = new TouristSitesSection(registry);

            Assert.Throws<SaveFormatException>(() => section.Read(Save(2, w => w.Write(-1)), 1), "negatief aantal");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(1, w => { w.Write(1); Write(w, 1, "a", 0, 10f, 0); }), 1), "volgnummer te laag");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(5, w => { w.Write(2); Write(w, 1, "a", 0, 10f, 0); Write(w, 1, "b", 0, 10f, 0); }), 1), "dubbel ID");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(5, w => { w.Write(1); Write(w, 1, "", 0, 10f, 0); }), 1), "zonder definitie");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(5, w => { w.Write(1); Write(w, 1, "a", 0, 500f, 0); }), 1), "aantrekkingskracht te hoog");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(5, w => { w.Write(1); Write(w, 1, "a", 9, 10f, 0); }), 1), "onbekend soort");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(5, w => { w.Write(1); Write(w, 1, "a", 1, 10f, 0); }), 1), "hotel zonder bedden");
            Assert.That(registry.Sites.Single().DefinitionId, Is.EqualTo("tourist-site.blijft"));
        }

        [Test]
        public void ASpecValidatesItsFields()
        {
            Assert.That(Errors(Spec(TouristSiteKind.Attraction, 60f, 0, false)), Is.Empty);
            Assert.That(Errors(Spec(TouristSiteKind.Hotel, 20f, 100, false)), Is.Empty);
            Assert.That(Errors(Spec(TouristSiteKind.Beach, 60f, 0, true)), Is.Empty);
            Assert.That(Errors(Spec(TouristSiteKind.Hotel, 20f, 0, false)), Is.Not.Empty);
            Assert.That(Errors(Spec(TouristSiteKind.Attraction, 20f, 10, false)), Is.Not.Empty);
            Assert.That(Errors(Spec(TouristSiteKind.Beach, 60f, 0, false)), Is.Not.Empty);
            Assert.That(Errors(Spec(TouristSiteKind.Attraction, 60f, 0, true)), Is.Not.Empty);
            Assert.That(Errors(Spec(TouristSiteKind.Attraction, 160f, 0, false)), Is.Not.Empty);

            var negative = Spec(TouristSiteKind.Attraction, 60f, 0, false);
            negative.ConstructionCost = -1;
            negative.FootprintWidth = 0;
            Assert.That(Errors(negative).Count, Is.EqualTo(2));
        }

        [Test]
        public void ASpecGivesItsProfile()
        {
            var profile = Spec(TouristSiteKind.Hotel, 25f, 120, false).ToProfile();

            Assert.That(profile.Kind, Is.EqualTo(TouristSiteKind.Hotel));
            Assert.That(profile.Beds, Is.EqualTo(120));
            Assert.That(profile.Attractiveness, Is.EqualTo(25f));
        }

        private static TouristSiteSpec Spec(TouristSiteKind kind, float attractiveness, int beds, bool needsWater) => new TouristSiteSpec
        {
            Kind = kind,
            FootprintWidth = 4,
            FootprintDepth = 4,
            Attractiveness = attractiveness,
            Beds = beds,
            ReachMeters = 500f,
            ConstructionCost = 1000,
            UpkeepPerMonth = 100,
            Noise = 10f,
            NeedsWater = needsWater,
        };

        private static List<string> Errors(TouristSiteSpec spec)
        {
            var errors = new List<string>();
            spec.Validate(errors);
            return errors;
        }

        private static SaveReader Save(int nextId, Action<SaveWriter> body) => SaveBytes.ReaderFor(w =>
        {
            w.Write(nextId);
            body(w);
        });

        private static void Write(SaveWriter w, int id, string definition, int kind, float attractiveness, int beds)
        {
            w.Write(id);
            w.Write(definition);
            w.Write(kind);
            w.Write(attractiveness);
            w.Write(beds);
            w.Write(100f);
            w.Write(0);
            w.Write(0);
            w.Write(0f);
            w.Write(0f);
            w.Write(0.5f);
            w.Write(1f);
        }
    }
}
