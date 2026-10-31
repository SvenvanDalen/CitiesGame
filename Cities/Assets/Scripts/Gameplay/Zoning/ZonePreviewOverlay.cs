using System;
using System.Collections.Generic;
using Cities.Core.Diagnostics;
using Cities.World.Grid;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Gameplay.Zoning
{
    /// <summary>Overlay <c>Zones</c>: de cellen die de <see cref="ZoneTool"/> zou aanpassen en de bebouwbare cellen in de buurt.</summary>
    public sealed class ZonePreviewOverlay : IDebugOverlay
    {
        public const string OverlayName = "Zones";

        private const float Lift = 0.25f;
        private readonly WorldMap _map;
        private readonly List<(CellCoord Cell, DebugColor Color, float Inset)> _cells = new List<(CellCoord, DebugColor, float)>();

        public ZonePreviewOverlay(WorldMap map) => _map = map ?? throw new ArgumentNullException(nameof(map));

        public string Name => OverlayName;

        public void Clear() => _cells.Clear();

        /// <summary>Tekent <paramref name="cell"/> als vierkant, <paramref name="inset"/> meter van de rand af.</summary>
        public void Add(CellCoord cell, DebugColor color, float inset = 0f) => _cells.Add((cell, color, inset));

        public void Collect(DebugGeometry geometry, in DebugView view)
        {
            if (geometry is null)
            {
                throw new ArgumentNullException(nameof(geometry));
            }

            var size = _map.Dimensions.CellSize;
            foreach (var (cell, color, inset) in _cells)
            {
                var x0 = (cell.X * size) + inset;
                var z0 = (cell.Z * size) + inset;
                var x1 = ((cell.X + 1) * size) - inset;
                var z1 = ((cell.Z + 1) * size) - inset;
                var a = Corner(x0, z0);
                var b = Corner(x1, z0);
                var c = Corner(x1, z1);
                var d = Corner(x0, z1);
                geometry.AddLine(a, b, color);
                geometry.AddLine(b, c, color);
                geometry.AddLine(c, d, color);
                geometry.AddLine(d, a, color);
            }
        }

        private float3 Corner(float x, float z) => new float3(x, _map.Height.Sample(new Vector2(x, z)) + Lift, z);
    }
}
