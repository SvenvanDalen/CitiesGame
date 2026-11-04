using System;
using System.Collections.Generic;
using Cities.Core.Persistence;
using Cities.Simulation.Disasters;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Disasters
{
    public class WarningNetworkTests
    {
        private static readonly float2 Far = new float2(5000f, 5000f);

        [Test]
        public void WithoutStationsNobodyIsAlertedAndThereIsNoExtraTime()
        {
            var network = new WarningNetwork();

            Assert.That(network.IsAlerted(float2.zero), Is.False);
            Assert.That(network.LeadDays(Storm.Spec, float2.zero), Is.EqualTo(1f), "alleen de eigen voorspelling");
            Assert.That(network.ReactionDays(Storm.Spec, float2.zero, float2.zero), Is.Zero, "niemand gewaarschuwd");
        }

        [Test]
        public void ASirenAlertsOnlyPeopleWithinItsReach()
        {
            var network = new WarningNetwork();
            network.Add(WarningKind.Siren, new float2(100f, 100f), 200f);

            Assert.That(network.IsAlerted(new float2(150f, 100f)), Is.True);
            Assert.That(network.IsAlerted(Far), Is.False);
            Assert.That(network.ReactionDays(Storm.Spec, float2.zero, new float2(150f, 100f)), Is.EqualTo(1f));
            Assert.That(network.ReactionDays(Storm.Spec, float2.zero, Far), Is.Zero);
        }

        [Test]
        public void TheCenterAlertsTheWholeCityAndAddsADayForPredictableDisasters()
        {
            var network = new WarningNetwork();
            network.Add(WarningKind.EarlyWarningCenter, float2.zero);

            Assert.That(network.IsAlerted(Far), Is.True);
            Assert.That(network.LeadDays(Storm.Spec, Far), Is.EqualTo(1f + WarningNetwork.CenterBonusDays));
            Assert.That(network.LeadDays(Earthquake.Spec, Far), Is.Zero, "een aardbeving is niet te voorspellen");
        }

        [Test]
        public void RadarAddsADayForWeatherDisastersWithinItsReachOnly()
        {
            var network = new WarningNetwork();
            network.Add(WarningKind.Radar, float2.zero, 1000f);

            Assert.That(network.LeadDays(Storm.Spec, new float2(500f, 0f)), Is.EqualTo(1f + WarningNetwork.RadarBonusDays));
            Assert.That(network.LeadDays(Storm.Spec, Far), Is.EqualTo(1f), "buiten het bereik");
            Assert.That(network.LeadDays(Flood.RiverSpec, new float2(500f, 0f)), Is.EqualTo(1f + WarningNetwork.RadarBonusDays));
            Assert.That(network.LeadDays(Earthquake.Spec, new float2(500f, 0f)), Is.Zero);
            Assert.That(network.IsAlerted(float2.zero), Is.False, "radar waarschuwt zelf niemand");
        }

        [Test]
        public void CenterAndRadarStack()
        {
            var network = new WarningNetwork();
            network.Add(WarningKind.EarlyWarningCenter, float2.zero);
            network.Add(WarningKind.Radar, float2.zero, 1000f);

            Assert.That(network.LeadDays(Storm.Spec, float2.zero), Is.EqualTo(3f));
            Assert.That(network.ReactionDays(Storm.Spec, float2.zero, Far), Is.EqualTo(3f));
        }

        [Test]
        public void CoverageIsTheShareOfAlertedPoints()
        {
            var network = new WarningNetwork();
            network.Add(WarningKind.Siren, float2.zero, 100f);
            var points = new List<float2> { new float2(10f, 0f), new float2(50f, 0f), Far, Far };

            Assert.That(network.Coverage(points), Is.EqualTo(0.5f));
            Assert.That(network.Coverage(new List<float2>()), Is.Zero);
            Assert.Throws<ArgumentNullException>(() => network.Coverage(null));
        }

        [Test]
        public void StationsCanBeRemoved()
        {
            var network = new WarningNetwork();
            var siren = network.Add(WarningKind.Siren, float2.zero, 100f);

            Assert.That(network.Remove(siren.Id), Is.True);
            Assert.That(network.Remove(siren.Id), Is.False);
            Assert.That(network.IsAlerted(float2.zero), Is.False);
        }

        [Test]
        public void BadInputIsRejected()
        {
            var network = new WarningNetwork();

            Assert.Throws<ArgumentOutOfRangeException>(() => network.Add(WarningKind.Siren, float2.zero, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => network.Add((WarningKind)9, float2.zero, 10f));
            Assert.Throws<ArgumentNullException>(() => network.LeadDays(null, float2.zero));
        }

        [Test]
        public void TheSectionRoundTripsDeterministically()
        {
            var network = new WarningNetwork();
            network.Add(WarningKind.Siren, new float2(1f, 2f), 300f);
            network.Add(WarningKind.EarlyWarningCenter, new float2(9f, 9f));
            network.Add(WarningKind.Radar, new float2(4f, 4f), 800f);
            var bytes = SaveBytes.Write(new SaveMetadata(), new WarningSection(network));

            var loaded = new WarningNetwork();
            SaveBytes.Read(bytes, new WarningSection(loaded));

            Assert.That(loaded.Stations, Has.Count.EqualTo(3));
            Assert.That(loaded.NextId, Is.EqualTo(4));
            Assert.That(loaded.Stations[2].Kind, Is.EqualTo(WarningKind.Radar));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new WarningSection(loaded)), Is.EqualTo(bytes));
            Assert.That(new WarningSection(loaded).Id, Is.EqualTo("WARN"));
        }

        [Test]
        public void ACorruptSaveIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new WarningSection(null));
            var section = new WarningSection(new WarningNetwork());

            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Corrupt(w, 0, 1, 0, 10f)), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Corrupt(w, 5, 9, 0, 10f)), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Corrupt(w, 5, 1, 7, 10f)), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Corrupt(w, 5, 1, 0, 0f)), 1));
        }

        private static void Corrupt(SaveWriter w, int nextId, int id, byte kind, float radius)
        {
            w.Write(nextId);
            w.Write(1);
            w.Write(id);
            w.Write(kind);
            w.Write(0f);
            w.Write(0f);
            w.Write(radius);
        }
    }
}
