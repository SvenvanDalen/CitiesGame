using System;
using Cities.Core.Diagnostics;
using Cities.World.Terrain;
using Unity.Mathematics;

namespace Cities.World.Grid
{
    /// <summary>
    /// Overlay <c>Cellen</c>: het raster van de wereldkaart over het terrein. Dichtbij elke cel (8 m), verder weg de
    /// blokken van de grove lagen (32 m), en altijd de tegelranden over de hele kaart. Lijnen volgen het terrein en
    /// blijven binnen een vast aantal per frame.
    /// </summary>
    public sealed class CellGridOverlay : IDebugOverlay
    {
        public const string OverlayName = "Cellen";

        /// <summary>Boven deze camerahoogte (m) verdwijnen de losse cellen en blijven de blokken over.</summary>
        public const float CellAltitude = 600f;

        /// <summary>Boven deze camerahoogte (m) blijven alleen de tegelranden over.</summary>
        public const float BlockAltitude = 3000f;

        /// <summary>Lijnen komen iets boven het oppervlak, zodat ze niet in het terrein verdwijnen.</summary>
        public const float Lift = 0.3f;

        private const float CellRadius = 400f;
        private const float BlockRadius = 1600f;
        private const float TileSegment = 64f;

        private readonly WorldDimensions _dimensions;
        private readonly ITerrainHeight _terrain;

        public CellGridOverlay(WorldDimensions dimensions, ITerrainHeight terrain)
        {
            _dimensions = dimensions;
            _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
        }

        public string Name => OverlayName;

        public void Collect(DebugGeometry geometry, in DebugView view)
        {
            if (geometry is null)
            {
                throw new ArgumentNullException(nameof(geometry));
            }

            var focus = view.Focus.xz;
            var altitude = view.Altitude;
            if (altitude < CellAltitude)
            {
                AddGrid(geometry, focus, Math.Min(view.Radius, CellRadius), _dimensions.CellSize, DebugColor.Neutral);
            }

            if (altitude < BlockAltitude)
            {
                var block = _dimensions.CellSize * WorldMap.CoarseCellsPerSample;
                AddGrid(geometry, focus, Math.Min(view.Radius, BlockRadius), block, DebugColor.Info);
            }

            AddTileBorders(geometry);
        }

        /// <summary>Rasterlijnen met afstand <paramref name="spacing"/> in een vierkant rond <paramref name="center"/>.</summary>
        private void AddGrid(DebugGeometry geometry, float2 center, float radius, float spacing, DebugColor color)
        {
            var min = math.max(float2.zero, math.floor((center - radius) / spacing) * spacing);
            var max = math.min(new float2(_dimensions.SizeX, _dimensions.SizeZ), math.ceil((center + radius) / spacing) * spacing);
            if (math.any(max <= min))
            {
                return;
            }

            for (var x = min.x; x <= max.x + 0.001f; x += spacing)
            {
                AddPolyline(geometry, new float2(x, min.y), new float2(x, max.y), spacing, color);
            }

            for (var z = min.y; z <= max.y + 0.001f; z += spacing)
            {
                AddPolyline(geometry, new float2(min.x, z), new float2(max.x, z), spacing, color);
            }
        }

        private void AddTileBorders(DebugGeometry geometry)
        {
            var size = new float2(_dimensions.SizeX, _dimensions.SizeZ);
            for (var tile = 0; tile <= _dimensions.TilesX; tile++)
            {
                var x = tile * _dimensions.TileSize;
                AddPolyline(geometry, new float2(x, 0f), new float2(x, size.y), TileSegment, DebugColor.Accent);
            }

            for (var tile = 0; tile <= _dimensions.TilesZ; tile++)
            {
                var z = tile * _dimensions.TileSize;
                AddPolyline(geometry, new float2(0f, z), new float2(size.x, z), TileSegment, DebugColor.Accent);
            }
        }

        /// <summary>Recht lijnstuk in het grondvlak, opgedeeld in stukken van hooguit <paramref name="step"/> die het terrein volgen.</summary>
        private void AddPolyline(DebugGeometry geometry, float2 from, float2 to, float step, DebugColor color)
        {
            var length = math.distance(from, to);
            var segments = Math.Max(1, (int)math.ceil(length / step));
            var previous = Lifted(from);
            for (var i = 1; i <= segments; i++)
            {
                var next = Lifted(math.lerp(from, to, (float)i / segments));
                geometry.AddLine(previous, next, color);
                previous = next;
            }
        }

        private float3 Lifted(float2 position) => new float3(position.x, _terrain.SurfaceHeight(position) + Lift, position.y);
    }
}
