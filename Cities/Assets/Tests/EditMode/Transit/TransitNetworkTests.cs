using System;
using System.Linq;
using Cities.Core.Persistence;
using Cities.Simulation.Transit;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Transit
{
    public class TransitNetworkTests
    {
        private static TransitNetwork NewNetwork(out int a, out int b, out int c)
        {
            var network = new TransitNetwork();
            a = network.AddStop(new float2(0f, 0f));
            b = network.AddStop(new float2(300f, 0f));
            c = network.AddStop(new float2(300f, 400f));
            return network;
        }

        [Test]
        public void ALineConnectsStopsInOrderAndMeasuresItsLength()
        {
            var network = NewNetwork(out var a, out var b, out var c);

            var id = network.AddLine("Lijn 1", new[] { a, b, c }, false, 2, 300f);

            Assert.That(network.GetLine(id).Stops.ToArray(), Is.EqualTo(new[] { a, b, c }));
            Assert.That(network.Length(id), Is.EqualTo(700f).Within(1e-3f));
            Assert.That(network.PositionOf(b), Is.EqualTo(new float2(300f, 0f)));
            Assert.That(network.Lines.Count(), Is.EqualTo(1));
        }

        [Test]
        public void ALoopAlsoDrivesBackToTheStart()
        {
            var network = NewNetwork(out var a, out var b, out var c);

            var id = network.AddLine("Ring", new[] { a, b, c }, true, 1, 60f);

            Assert.That(network.Length(id), Is.EqualTo(1200f).Within(1e-3f));
        }

        [Test]
        public void TooFewVehiclesMakeTheEffectiveHeadwayLongerThanWanted()
        {
            var network = NewNetwork(out var a, out var b, out var c);
            var id = network.AddLine("Lijn 1", new[] { a, b, c }, false, 1, 60f);

            // Heen en terug 1400 m bij 10 m/s is 140 s met één voertuig.
            Assert.That(network.EffectiveHeadway(id, 10f), Is.EqualTo(140f).Within(1e-3f));
            network.Update(id, "Lijn 1", new[] { a, b, c }, false, 4, 60f);
            Assert.That(network.EffectiveHeadway(id, 10f), Is.EqualTo(60f), "met vier voertuigen haalt de lijn de gewenste 60 s");
            Assert.Throws<ArgumentOutOfRangeException>(() => network.EffectiveHeadway(id, 0f));
        }

        [Test]
        public void ALineCanBeEditedAndRemoved()
        {
            var network = NewNetwork(out var a, out var b, out var c);
            var id = network.AddLine("Lijn 1", new[] { a, b }, false, 1, 120f);

            network.Update(id, "Lijn A", new[] { a, c, b }, false, 3, 90f);

            var line = network.GetLine(id);
            Assert.That(line.Name, Is.EqualTo("Lijn A"));
            Assert.That(line.Stops.ToArray(), Is.EqualTo(new[] { a, c, b }));
            Assert.That(line.Vehicles, Is.EqualTo(3));
            Assert.That(line.Headway, Is.EqualTo(90f));
            Assert.That(network.RemoveLine(id), Is.True);
            Assert.That(network.RemoveLine(id), Is.False);
            Assert.Throws<ArgumentException>(() => network.GetLine(id));
        }

        [Test]
        public void AFailedEditLeavesTheLineAsItWas()
        {
            var network = NewNetwork(out var a, out var b, out _);
            var id = network.AddLine("Lijn 1", new[] { a, b }, false, 1, 120f);

            Assert.Throws<ArgumentException>(() => network.Update(id, "Lijn 1", new[] { a, 99 }, false, 1, 120f));
            Assert.Throws<ArgumentException>(() => network.Update(7, "x", new[] { a, b }, false, 1, 120f));

            Assert.That(network.GetLine(id).Stops.ToArray(), Is.EqualTo(new[] { a, b }));
        }

        [Test]
        public void AStopInUseCannotBeRemoved()
        {
            var network = NewNetwork(out var a, out var b, out var c);
            var id = network.AddLine("Lijn 1", new[] { a, b }, false, 1, 120f);

            Assert.That(network.TryRemoveStop(a), Is.False);
            Assert.That(network.TryRemoveStop(c), Is.True);
            Assert.That(network.TryRemoveStop(c), Is.False, "bestaat niet meer");
            Assert.That(network.HasStop(c), Is.False);
            network.RemoveLine(id);
            Assert.That(network.TryRemoveStop(a), Is.True);
            Assert.Throws<ArgumentException>(() => network.PositionOf(a));
        }

        [Test]
        public void InvalidLinesAreRejected()
        {
            var network = NewNetwork(out var a, out var b, out _);

            Assert.Throws<ArgumentException>(() => network.AddLine(" ", new[] { a, b }, false, 1, 60f));
            Assert.Throws<ArgumentException>(() => network.AddLine("x", new[] { a }, false, 1, 60f));
            Assert.Throws<ArgumentException>(() => network.AddLine("x", null, false, 1, 60f));
            Assert.Throws<ArgumentException>(() => network.AddLine("x", new[] { a, a }, false, 1, 60f));
            Assert.Throws<ArgumentException>(() => network.AddLine("x", new[] { a, b, a }, true, 1, 60f));
            Assert.Throws<ArgumentOutOfRangeException>(() => network.AddLine("x", new[] { a, b }, false, 0, 60f));
            Assert.Throws<ArgumentOutOfRangeException>(() => network.AddLine("x", new[] { a, b }, false, 1, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => network.AddLine("x", new[] { a, b }, false, 1, float.PositiveInfinity));
            Assert.That(network.Lines, Is.Empty);
        }

        [Test]
        public void TheNetworkRoundTripsThroughASave()
        {
            var network = NewNetwork(out var a, out var b, out var c);
            network.AddLine("Lijn 1", new[] { a, b, c }, false, 2, 300f);
            network.AddLine("Ring", new[] { c, b, a }, true, 1, 60f);
            network.TryRemoveStop(network.AddStop(float2.zero));
            var bytes = SaveBytes.Write(new SaveMetadata(), new TransitSection(network));
            var loaded = new TransitNetwork();
            loaded.AddStop(new float2(5f, 5f));

            SaveBytes.Read(bytes, new TransitSection(loaded));

            Assert.That(loaded.Lines.Select(l => l.Name).ToArray(), Is.EqualTo(new[] { "Lijn 1", "Ring" }));
            Assert.That(loaded.Length(2), Is.EqualTo(1200f).Within(1e-3f));
            Assert.That(loaded.NextStopId, Is.EqualTo(network.NextStopId));
            Assert.That(loaded.AddStop(float2.zero), Is.EqualTo(network.NextStopId), "volgnummers lopen door");
            Assert.That(SaveBytes.Write(new SaveMetadata(), new TransitSection(network)), Is.EqualTo(bytes));
        }

        [Test]
        public void ACorruptSaveIsRejectedAndChangesNothing()
        {
            var loaded = new TransitNetwork();
            loaded.AddStop(float2.zero);
            var section = new TransitSection(loaded);

            Assert.Throws<SaveFormatException>(() => section.Read(Save(w => w.Write(-1), 2, 2), 1), "negatief aantal haltes");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(w => { w.Write(1); w.Write(5); w.Write(0f); w.Write(0f); w.Write(0); }, 3, 1), 1), "volgnummer te laag");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(w => { w.Write(2); StopBody(w, 1); StopBody(w, 1); w.Write(0); }, 3, 1), 1), "dubbele halte");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(w => { w.Write(1); StopBody(w, 1); w.Write(1); LineBody(w, 1, 1, 9); }, 3, 3), 1), "lijn met onbekende halte");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(w => { w.Write(1); StopBody(w, 1); w.Write(1); LineBody(w, 1, 1, 1); }, 3, 3), 1), "lijn met twee keer dezelfde halte");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(w => { w.Write(2); StopBody(w, 1); StopBody(w, 2); w.Write(2); LineBody(w, 1, 1, 2); LineBody(w, 1, 1, 2); }, 3, 3), 1), "dubbele lijn");
            Assert.That(loaded.Stops.Count(), Is.EqualTo(1));
        }

        private static SaveReader Save(Action<SaveWriter> body, int nextStop, int nextLine) => SaveBytes.ReaderFor(w =>
        {
            w.Write(nextStop);
            w.Write(nextLine);
            body(w);
        });

        private static void StopBody(SaveWriter w, int id)
        {
            w.Write(id);
            w.Write(0f);
            w.Write(0f);
        }

        private static void LineBody(SaveWriter w, int id, int first, int second)
        {
            w.Write(id);
            w.Write("Lijn");
            w.Write(false);
            w.Write(1);
            w.Write(60f);
            w.Write(2);
            w.Write(first);
            w.Write(second);
        }
    }
}
