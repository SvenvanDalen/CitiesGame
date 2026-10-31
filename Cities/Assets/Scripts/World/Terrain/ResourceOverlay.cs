using System;
using Cities.Core.Diagnostics;
using Unity.Mathematics;

namespace Cities.World.Terrain
{
    /// <summary>
    /// Overlay per grondstof (E03-08): kruisjes op het terrein waar de laag rijk genoeg is (<see cref="ResourceLayers.Deposit"/>), op een
    /// raster rond het kijkpunt. Hoe verder uitgezoomd, hoe grover het raster, zodat het aantal lijnen binnen het budget blijft.
    /// </summary>
    public sealed class ResourceOverlay : IDebugOverlay
    {
        private const int Samples = 40;

        private readonly ResourceKind _kind;
        private readonly TerrainShapeSettings _settings;
        private readonly ITerrainHeight _terrain;
        private readonly DebugColor _color;

        public ResourceOverlay(ResourceKind kind, string name, DebugColor color, TerrainShapeSettings settings, ITerrainHeight terrain)
        {
            _kind = kind;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _color = color;
            _settings = settings;
            _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
        }

        public string Name { get; }

        public void Collect(DebugGeometry geometry, in DebugView view)
        {
            if (geometry is null)
            {
                throw new ArgumentNullException(nameof(geometry));
            }

            var radius = math.max(view.Radius, 50f);
            var spacing = 2f * radius / Samples;
            var start = math.floor((view.Focus.xz - radius) / spacing) * spacing;
            for (var ix = 0; ix <= Samples; ix++)
            {
                for (var iz = 0; iz <= Samples; iz++)
                {
                    var p = start + (new float2(ix, iz) * spacing);
                    if (ResourceLayers.Richness(_kind, p, _settings) >= ResourceLayers.Deposit)
                    {
                        Cross(geometry, p, spacing * 0.35f);
                    }
                }
            }
        }

        private void Cross(DebugGeometry geometry, float2 centre, float arm)
        {
            var y = _terrain.SurfaceHeight(centre) + 0.5f;
            geometry.AddLine(new float3(centre.x - arm, y, centre.y), new float3(centre.x + arm, y, centre.y), _color);
            geometry.AddLine(new float3(centre.x, y, centre.y - arm), new float3(centre.x, y, centre.y + arm), _color);
        }
    }
}
