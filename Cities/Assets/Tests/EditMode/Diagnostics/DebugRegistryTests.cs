using System;
using Cities.Core.Diagnostics;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Diagnostics
{
    public class DebugRegistryTests
    {
        private sealed class LineOverlay : IDebugOverlay
        {
            public LineOverlay(string name) => Name = name;

            public string Name { get; }

            public int Calls { get; private set; }

            public void Collect(DebugGeometry geometry, in DebugView view)
            {
                Calls++;
                geometry.AddLine(view.Focus, view.Focus + new float3(0f, 10f, 0f), DebugColor.Accent);
                geometry.AddLabel(view.Focus, Name);
            }
        }

        private sealed class FailingOverlay : IDebugOverlay
        {
            public string Name => "Kapot";

            public void Collect(DebugGeometry geometry, in DebugView view) => throw new InvalidOperationException("stuk");
        }

        private static readonly DebugView View = new DebugView(new float3(0f, 500f, 0f), new float3(100f, 20f, 100f), 800f);

        [Test]
        public void CountersAreAddedReadAndRemoved()
        {
            var registry = new DebugRegistry();
            var changes = 0;
            registry.Changed += () => changes++;
            var value = 41L;

            var token = registry.AddCounter("Simulatie", "Tick", () => value);
            value++;

            Assert.That(registry.Counters, Has.Count.EqualTo(1));
            Assert.That(registry.Counters[0].Read(), Is.EqualTo(42));
            Assert.That(registry.Counters[0].Category, Is.EqualTo("Simulatie"));
            token.Dispose();
            token.Dispose();
            Assert.That(registry.Counters, Is.Empty);
            Assert.That(changes, Is.EqualTo(2));
        }

        [Test]
        public void OnlyEnabledOverlaysDraw()
        {
            var registry = new DebugRegistry();
            var cells = new LineOverlay("Cellen");
            var lanes = new LineOverlay("Lanegraaf");
            registry.AddOverlay(cells);
            var lanesToken = registry.AddOverlay(lanes);
            var geometry = new DebugGeometry();

            registry.Collect(geometry, View);
            Assert.That(geometry.Lines, Is.Empty, "Overlays staan standaard uit");

            registry.Toggle("Lanegraaf");
            registry.Collect(geometry, View);
            Assert.That(lanes.Calls, Is.EqualTo(1));
            Assert.That(cells.Calls, Is.Zero);
            Assert.That(geometry.Labels[0].Text, Is.EqualTo("Lanegraaf"));
            Assert.That(registry.IsEnabled("Lanegraaf"), Is.True);

            registry.Toggle("Lanegraaf");
            lanesToken.Dispose();
            Assert.That(registry.Find("Lanegraaf"), Is.Null);
            Assert.That(registry.Find("Cellen"), Is.SameAs(cells));
        }

        [Test]
        public void AFailingOverlayDoesNotStopTheOthers()
        {
            var registry = new DebugRegistry();
            var good = new LineOverlay("Goed");
            registry.AddOverlay(new FailingOverlay());
            registry.AddOverlay(good);
            registry.SetEnabled("Kapot", true);
            registry.SetEnabled("Goed", true);
            string failed = null;

            registry.Collect(new DebugGeometry(), View, (overlay, _) => failed = overlay.Name);

            Assert.That(failed, Is.EqualTo("Kapot"));
            Assert.That(good.Calls, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => registry.Collect(new DebugGeometry(), View), "Zonder foutafhandeling komt de fout door");
        }

        [Test]
        public void InvalidRegistrationsAreRefused()
        {
            var registry = new DebugRegistry();
            registry.AddOverlay(new LineOverlay("Cellen"));

            Assert.Throws<ArgumentException>(() => registry.AddOverlay(new LineOverlay("Cellen")));
            Assert.Throws<ArgumentNullException>(() => registry.AddOverlay(null));
            Assert.Throws<ArgumentNullException>(() => registry.AddCounter("A", "B", null));
            Assert.Throws<ArgumentNullException>(() => registry.AddCounter(null, "B", () => 0));
            Assert.Throws<ArgumentNullException>(() => registry.AddCounter("A", null, () => 0));
            Assert.Throws<ArgumentNullException>(() => registry.Collect(null, View));
            Assert.That(DebugRegistry.Shared, Is.Not.Null);
        }

        [Test]
        public void SettingTheSameStateTwiceRaisesOneChange()
        {
            var registry = new DebugRegistry();
            var changes = 0;
            registry.Changed += () => changes++;

            registry.SetEnabled("Cellen", true);
            registry.SetEnabled("Cellen", true);
            registry.SetEnabled("Cellen", false);

            Assert.That(changes, Is.EqualTo(2));
        }

        [Test]
        public void GeometryRespectsItsLimits()
        {
            var geometry = new DebugGeometry(maxLines: 2, maxLabels: 1);

            for (var i = 0; i < 5; i++)
            {
                geometry.AddLine(float3.zero, new float3(i, 0f, 0f));
                geometry.AddLabel(float3.zero, null);
            }

            Assert.That(geometry.Lines, Has.Count.EqualTo(2));
            Assert.That(geometry.DroppedLines, Is.EqualTo(3));
            Assert.That(geometry.Labels, Has.Count.EqualTo(1));
            Assert.That(geometry.Labels[0].Text, Is.Empty);
            Assert.That(geometry.DroppedLabels, Is.EqualTo(4));
            Assert.That(geometry.Lines[1].To.x, Is.EqualTo(1f));

            geometry.Clear();
            Assert.That(geometry.Lines, Is.Empty);
            Assert.That(geometry.DroppedLines, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => new DebugGeometry(maxLines: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DebugGeometry(maxLabels: -1));
            Assert.That(View.Altitude, Is.EqualTo(480f));
        }
    }
}
