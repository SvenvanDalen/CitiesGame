using System;
using System.Collections.Generic;
using Cities.Gameplay.Tools;
using Cities.Simulation.Network;
using Cities.World.Grid;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Gameplay.Zoning
{
    /// <summary>
    /// Zet zones op de <see cref="WorldMap.Zones"/>-laag: alleen op cellen langs een weg, tot
    /// <see cref="ZoneRules.MaxDepthCells"/> cellen diep. Zones blijven staan als de weg verdwijnt; <see cref="Revalidate"/>
    /// ruimt ze dan op voor een gebied (de gebouwspawner van E05-04 controleert de toegang zelf ook).
    /// </summary>
    public sealed class ZoneService
    {
        private readonly WorldMap _map;
        private readonly RoadNetwork _network;

        public ZoneService(WorldMap map, RoadNetwork network)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _network = network ?? throw new ArgumentNullException(nameof(network));
        }

        /// <summary>Cellen (min inclusief, max exclusief) waarvan de zone veranderd is.</summary>
        public event Action<RectInt> ZonesChanged;

        /// <summary>Waarom deze cel niet gezoned mag worden, of <c>null</c>.</summary>
        public string Reason(CellCoord cell)
        {
            if (!_map.Zones.Layout.Contains(cell.X, cell.Z))
            {
                return "Buiten de kaart";
            }

            var size = _map.Dimensions.CellSize;
            var centre = new float3((cell.X + 0.5f) * size, 0f, (cell.Z + 0.5f) * size);
            return ZoneRules.Reason(EdgeDistance(centre), size, _map.IsUnderwater(cell));
        }

        /// <summary>Zet <paramref name="type"/> op alle toegestane cellen in <paramref name="cells"/> en geeft terug hoeveel cellen veranderden.</summary>
        public int Paint(RectInt cells, ZoneType type, ZoneDensity density) => Paint(Cells(cells), type, density);

        /// <summary>Zet <paramref name="type"/> op alle toegestane cellen in <paramref name="cells"/> en geeft terug hoeveel cellen veranderden.</summary>
        public int Paint(IEnumerable<CellCoord> cells, ZoneType type, ZoneDensity density)
        {
            var zone = new ZoneCell(type, density);
            var changed = 0;
            var bounds = new RectInt(0, 0, 0, 0);
            foreach (var cell in cells)
            {
                if (!_map.Zones.Layout.Contains(cell.X, cell.Z))
                {
                    continue;
                }

                if (_map.Zones.GetForCell(cell) == zone || (type != ZoneType.None && Reason(cell) != null))
                {
                    continue;
                }

                _map.Zones.SetForCell(cell, zone);
                bounds = changed == 0 ? new RectInt(cell.X, cell.Z, 1, 1) : Include(bounds, cell);
                changed++;
            }

            if (changed > 0)
            {
                ZonesChanged?.Invoke(bounds);
            }

            return changed;
        }

        /// <summary>Wist de zone op alle cellen in <paramref name="cells"/>.</summary>
        public int Clear(RectInt cells) => Paint(cells, ZoneType.None, ZoneDensity.Low);

        /// <summary>Wist de zone op de cellen in <paramref name="cells"/>.</summary>
        public int Clear(IEnumerable<CellCoord> cells) => Paint(cells, ZoneType.None, ZoneDensity.Low);

        /// <summary>Wist de zones in <paramref name="cells"/> die niet meer langs een weg liggen; geeft terug hoeveel.</summary>
        public int Revalidate(RectInt cells)
        {
            var removed = 0;
            var bounds = new RectInt(0, 0, 0, 0);
            foreach (var cell in Cells(cells))
            {
                if (!_map.Zones.GetForCell(cell).IsZoned || Reason(cell) == null)
                {
                    continue;
                }

                _map.Zones.SetForCell(cell, ZoneCell.Empty);
                bounds = removed == 0 ? new RectInt(cell.X, cell.Z, 1, 1) : Include(bounds, cell);
                removed++;
            }

            if (removed > 0)
            {
                ZonesChanged?.Invoke(bounds);
            }

            return removed;
        }

        // shortcut: loopt per cel langs alle segmenten, prima voor een penseel; bij een hele tegel per keer een ruimtelijke index gebruiken.
        private float EdgeDistance(float3 point)
        {
            var best = float.PositiveInfinity;
            foreach (var id in _network.Segments())
            {
                var road = _network.GetSegment(id);
                best = math.min(best, RoadPlanner.DistanceTo(road.Curve, point) - RoadPicker.HalfWidth(_network, road));
            }

            return best;
        }

        private IEnumerable<CellCoord> Cells(RectInt rect)
        {
            var layout = _map.Zones.Layout;
            for (var z = Math.Max(rect.yMin, 0); z < Math.Min(rect.yMax, layout.Depth); z++)
            {
                for (var x = Math.Max(rect.xMin, 0); x < Math.Min(rect.xMax, layout.Width); x++)
                {
                    yield return new CellCoord(x, z);
                }
            }
        }

        private static RectInt Include(RectInt rect, CellCoord cell)
        {
            var min = new Vector2Int(Math.Min(rect.xMin, cell.X), Math.Min(rect.yMin, cell.Z));
            var max = new Vector2Int(Math.Max(rect.xMax, cell.X + 1), Math.Max(rect.yMax, cell.Z + 1));
            return new RectInt(min.x, min.y, max.x - min.x, max.y - min.y);
        }
    }
}
