using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Diagnostics;
using Cities.Simulation.Services;
using Cities.World.Terrain;
using Unity.Mathematics;

namespace Cities.World.Services
{
    /// <summary>
    /// Dekkingsoverlay van één dienst (E10-09): elk dienstgebouw krijgt een cirkel met zijn bereik en elk gebouw een kruisje, groen als het
    /// gedekt is en rood als het onbediend is. Alleen wat binnen het kijkbereik valt wordt getekend. <see cref="Unserved"/> geeft de
    /// onbediende gebouwen voor de statistiek.
    /// </summary>
    public sealed class ServiceOverlay : IDebugOverlay
    {
        private const int CircleSegments = 32;

        private readonly ServiceKind _kind;
        private readonly ServiceDispatcher _dispatcher;
        private readonly Func<IEnumerable<float2>> _buildings;
        private readonly ITerrainHeight _terrain;

        public ServiceOverlay(string name, ServiceKind kind, ServiceDispatcher dispatcher, Func<IEnumerable<float2>> buildings, ITerrainHeight terrain)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _kind = kind;
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
            _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
        }

        public string Name { get; }

        /// <summary>Gebouwen die geen enkel dienstgebouw van deze soort bereikt.</summary>
        public IReadOnlyList<float2> Unserved() => _buildings().Where(b => !_dispatcher.IsCovered(_kind, b)).ToList();

        public void Collect(DebugGeometry geometry, in DebugView view)
        {
            if (geometry is null)
            {
                throw new ArgumentNullException(nameof(geometry));
            }

            var focus = view.Focus.xz;
            var radius = view.Radius;
            foreach (var station in _dispatcher.Stations.Where(s => s.Kind == _kind && InView(s.Position, s.Radius, focus, radius)))
            {
                var previous = Point(station.Position + new float2(station.Radius, 0f));
                for (var i = 1; i <= CircleSegments; i++)
                {
                    var angle = i * math.PI * 2f / CircleSegments;
                    var next = Point(station.Position + (station.Radius * new float2(math.cos(angle), math.sin(angle))));
                    geometry.AddLine(previous, next, DebugColor.Info);
                    previous = next;
                }
            }

            foreach (var building in _buildings().Where(b => InView(b, 0f, focus, radius)))
            {
                var at = Point(building);
                var color = _dispatcher.IsCovered(_kind, building) ? DebugColor.Good : DebugColor.Bad;
                geometry.AddLine(at - new float3(1f, 0f, 0f), at + new float3(1f, 0f, 0f), color);
                geometry.AddLine(at - new float3(0f, 0f, 1f), at + new float3(0f, 0f, 1f), color);
            }
        }

        private static bool InView(float2 position, float margin, float2 focus, float radius) =>
            math.length(position - focus) <= radius + margin;

        private float3 Point(float2 position) => new float3(position.x, _terrain.SurfaceHeight(position) + 0.5f, position.y);
    }
}
