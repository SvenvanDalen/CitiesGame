using System;
using System.Linq;
using Cities.Core.Diagnostics;
using Cities.Simulation.Utilities;
using Cities.World.Terrain;
using Unity.Mathematics;

namespace Cities.World.Utilities
{
    /// <summary>
    /// Overlay van één nutsnet (E06-14): stroom, water of riool krijgt zijn eigen overlay in <c>DebugRegistry</c>. Elke knoop is een kruisje,
    /// gekleurd naar hoeveel van zijn vraag hij krijgt (groen volledig, geel een tekort, rood bijna niets), en leidingen zijn lijnen. Alleen knopen
    /// binnen het kijkbereik worden getekend. <see cref="Describe"/> geeft de belasting van een net voor de inspectie.
    /// </summary>
    public sealed class UtilityOverlay : IDebugOverlay
    {
        /// <summary>Onder deze tevredenheid is een knoop rood.</summary>
        public const float BadBelow = 0.5f;

        private readonly UtilityNetwork _network;
        private readonly ITerrainHeight _terrain;

        public UtilityOverlay(string name, UtilityNetwork network, ITerrainHeight terrain)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
        }

        public string Name { get; }

        public static DebugColor ColorFor(float satisfaction)
        {
            if (satisfaction >= 1f)
            {
                return DebugColor.Good;
            }

            return satisfaction < BadBelow ? DebugColor.Bad : DebugColor.Warning;
        }

        /// <summary>Tekst voor de capaciteitsinspectie van één net.</summary>
        public static string Describe(NetworkLoad load) =>
            $"{load.Nodes} knopen, levert {load.Supply:0.#}, vraag {load.Demand:0.#}" + (load.IsOverloaded ? " (tekort)" : string.Empty);

        public void Collect(DebugGeometry geometry, in DebugView view)
        {
            if (geometry is null)
            {
                throw new ArgumentNullException(nameof(geometry));
            }

            var radiusSq = view.Radius * view.Radius;
            var focus = view.Focus.xz;
            foreach (var id in _network.Nodes.Where(n => math.lengthsq(_network.PositionOf(n) - focus) <= radiusSq))
            {
                var position = _network.PositionOf(id);
                var color = ColorFor(_network.Satisfaction(id));
                var at = Point(position);
                geometry.AddLine(at - new float3(1f, 0f, 0f), at + new float3(1f, 0f, 0f), color);
                geometry.AddLine(at - new float3(0f, 0f, 1f), at + new float3(0f, 0f, 1f), color);
                foreach (var other in _network.LinksOf(id))
                {
                    if (other > id)
                    {
                        geometry.AddLine(at, Point(_network.PositionOf(other)), DebugColor.Info);
                    }
                }
            }
        }

        private float3 Point(float2 position) => new float3(position.x, _terrain.SurfaceHeight(position) + 0.5f, position.y);
    }
}
