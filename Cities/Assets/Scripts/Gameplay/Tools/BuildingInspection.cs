using System;
using Cities.World.Grid;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Gameplay.Tools
{
    /// <summary>Inspectiebron voor de geplaatste gebouwen: het gebouw waarvan de footprint het punt bevat.</summary>
    public sealed class BuildingInspection : IInspectionSource
    {
        private readonly FootprintOccupancy _occupancy;
        private readonly WorldDimensions _world;

        public BuildingInspection(FootprintOccupancy occupancy, WorldDimensions world)
        {
            _occupancy = occupancy ?? throw new ArgumentNullException(nameof(occupancy));
            _world = world;
        }

        public bool TryInspect(float3 point, out InspectionReport report)
        {
            var cell = _world.WorldToCell(new Vector2(point.x, point.z));
            foreach (var footprint in _occupancy.Placed)
            {
                if (footprint.Contains(cell))
                {
                    var center = footprint.Center(_world);
                    var size = _world.CellSize;
                    report = new InspectionReport(
                        new float3(center.x, point.y, center.y),
                        "Gebouw",
                        $"{footprint.SizeX * size:0} × {footprint.SizeZ * size:0} m",
                        $"{footprint.SizeX * footprint.SizeZ} cellen",
                        $"gedraaid {footprint.YawDegrees:0}°");
                    return true;
                }
            }

            report = default;
            return false;
        }
    }
}
