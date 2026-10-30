using System;
using Cities.World.Grid;
using Unity.Mathematics;

namespace Cities.Gameplay.Tools
{
    /// <summary>Snapt objecten op het cellenraster.</summary>
    public static class GridSnap
    {
        /// <summary>
        /// De footprint waarvan het midden het dichtst bij <paramref name="point"/> ligt: een oneven aantal cellen
        /// centreert op de cel onder het punt, een even aantal op de dichtstbijzijnde rasterlijn.
        /// </summary>
        public static Footprint Footprint(float3 point, int width, int depth, int quarterTurns, float cellSize)
        {
            var shape = new Footprint(default, width, depth, quarterTurns);
            var x = (int)Math.Floor((point.x / cellSize) - (shape.SizeX * 0.5f) + 0.5f);
            var z = (int)Math.Floor((point.z / cellSize) - (shape.SizeZ * 0.5f) + 0.5f);
            return new Footprint(new CellCoord(x, z), width, depth, quarterTurns);
        }
    }
}
