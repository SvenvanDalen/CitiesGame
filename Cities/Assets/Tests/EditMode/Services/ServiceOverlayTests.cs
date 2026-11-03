using System;
using System.Linq;
using Cities.Core.Diagnostics;
using Cities.Simulation.Services;
using Cities.World.Services;
using Cities.World.Terrain;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Services
{
    public class ServiceOverlayTests
    {
        private static readonly DebugView View = new DebugView(new float3(0f, 100f, 0f), new float3(0f, 0f, 0f), 500f);

        private static ServiceOverlay Overlay(ServiceDispatcher dispatcher, params float2[] buildings) =>
            new ServiceOverlay("Dekking brandweer", ServiceKind.Fire, dispatcher, () => buildings, new FlatTerrain(0f));

        [Test]
        public void UnservedBuildingsAreTheOnesOutOfRange()
        {
            var dispatcher = new ServiceDispatcher(10f);
            dispatcher.Add(new ServiceStation(ServiceKind.Fire, float2.zero, 100f, 1, 0));
            dispatcher.Add(new ServiceStation(ServiceKind.Police, new float2(300f, 0f), 100f, 1, 0));
            var overlay = Overlay(dispatcher, new float2(50f, 0f), new float2(300f, 0f));

            Assert.That(overlay.Unserved(), Is.EqualTo(new[] { new float2(300f, 0f) }), "een politiebureau dekt niet voor de brandweer");
            Assert.That(dispatcher.Stations.Count, Is.EqualTo(2));
        }

        [Test]
        public void TheOverlayDrawsRangeCirclesAndColouredCrosses()
        {
            var dispatcher = new ServiceDispatcher(10f);
            dispatcher.Add(new ServiceStation(ServiceKind.Fire, float2.zero, 100f, 1, 0));
            dispatcher.Add(new ServiceStation(ServiceKind.Police, new float2(10f, 0f), 50f, 1, 0));
            var overlay = Overlay(dispatcher, new float2(50f, 0f), new float2(300f, 0f), new float2(9000f, 0f));
            var geometry = new DebugGeometry();

            overlay.Collect(geometry, View);

            Assert.That(overlay.Name, Is.EqualTo("Dekking brandweer"));
            Assert.That(geometry.Lines.Count(l => l.Color == DebugColor.Info), Is.EqualTo(32), "één cirkel, alleen van de brandweer");
            Assert.That(geometry.Lines.Count(l => l.Color == DebugColor.Good), Is.EqualTo(2));
            Assert.That(geometry.Lines.Count(l => l.Color == DebugColor.Bad), Is.EqualTo(2), "de ver weg gelegen woning wordt niet getekend");
        }

        [Test]
        public void StationsOutOfViewAreSkipped()
        {
            var dispatcher = new ServiceDispatcher(10f);
            dispatcher.Add(new ServiceStation(ServiceKind.Fire, new float2(9000f, 0f), 100f, 1, 0));
            var geometry = new DebugGeometry();

            Overlay(dispatcher).Collect(geometry, View);

            Assert.That(geometry.Lines, Is.Empty);
        }

        [Test]
        public void NullArgumentsAreRejected()
        {
            var dispatcher = new ServiceDispatcher(10f);
            Func<System.Collections.Generic.IEnumerable<float2>> none = () => Array.Empty<float2>();

            Assert.Throws<ArgumentNullException>(() => new ServiceOverlay(null, ServiceKind.Fire, dispatcher, none, new FlatTerrain(0f)));
            Assert.Throws<ArgumentNullException>(() => new ServiceOverlay("x", ServiceKind.Fire, null, none, new FlatTerrain(0f)));
            Assert.Throws<ArgumentNullException>(() => new ServiceOverlay("x", ServiceKind.Fire, dispatcher, null, new FlatTerrain(0f)));
            Assert.Throws<ArgumentNullException>(() => new ServiceOverlay("x", ServiceKind.Fire, dispatcher, none, null));
            Assert.Throws<ArgumentNullException>(() => Overlay(dispatcher).Collect(null, View));
        }
    }
}
