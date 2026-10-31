using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Randomness;
using Cities.Gameplay.Tools;
using Cities.World.Buildings;
using Cities.World.Grid;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Gameplay.Zoning
{
    /// <summary>
    /// Laat de stad groeien (E05-04): kiest een gezoneerde cel, een gebouw dat bij zone en dichtheid past, draait de voorkant naar
    /// de weg en zet het neer als de hele kavel gezoned, vrij en vlak genoeg is. De kans op een gebouw per poging is de vraag
    /// (0 tot 1) van het zonetype. Niveau 1 groeit; hogere niveaus komen met E05-09.
    /// </summary>
    public sealed class BuildingSpawner
    {
        public const int GrowLevel = 1;

        /// <summary>Grootste hoogteverschil binnen een kavel in meters.</summary>
        public const float MaxRise = 1.5f;

        private readonly WorldMap _map;
        private readonly ZoneService _zones;
        private readonly FootprintOccupancy _occupancy;
        private readonly List<BuildingDefinition> _definitions;
        private readonly Func<ZoneType, float> _demand;
        private readonly Action<BuildingDefinition, Footprint, float3> _place;
        private DeterministicRandom _random;
        private readonly List<CellCoord> _cells = new List<CellCoord>();
        private readonly Dictionary<CellCoord, int> _index = new Dictionary<CellCoord, int>();
        private readonly float2[] _samples = new float2[5];

        /// <param name="definitions">Alle gebouwtypes; alleen groeigebouwen met een model doen mee.</param>
        /// <param name="demand">De vraag van -1 tot 1 per zonetype; alleen een positieve vraag laat een gebouw groeien.</param>
        /// <param name="place">Zet het gebouw neer: neemt de kavel in beslag en maakt het model; de spawner doet dat zelf niet.</param>
        public BuildingSpawner(
            WorldMap map,
            ZoneService zones,
            FootprintOccupancy occupancy,
            IEnumerable<BuildingDefinition> definitions,
            Func<ZoneType, float> demand,
            Action<BuildingDefinition, Footprint, float3> place,
            ulong seed)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _zones = zones ?? throw new ArgumentNullException(nameof(zones));
            _occupancy = occupancy ?? throw new ArgumentNullException(nameof(occupancy));
            _demand = demand ?? throw new ArgumentNullException(nameof(demand));
            _place = place ?? throw new ArgumentNullException(nameof(place));
            _random = new DeterministicRandom(seed);
            _definitions = new List<BuildingDefinition>();
            foreach (var definition in definitions ?? throw new ArgumentNullException(nameof(definitions)))
            {
                if (definition != null && definition.Prefab != null && definition.Spec.IsGrowable && definition.Spec.Level == GrowLevel)
                {
                    _definitions.Add(definition);
                }
            }

            ScanAll();
            _zones.ZonesChanged += Rescan;
        }

        /// <summary>Het aantal gezoneerde cellen waaruit gekozen wordt.</summary>
        public int ZonedCells => _cells.Count;

        public void Detach() => _zones.ZonesChanged -= Rescan;

        /// <summary>Probeert hoogstens <paramref name="attempts"/> keer een gebouw te laten groeien; geeft terug hoeveel er kwamen.</summary>
        public int Grow(int attempts)
        {
            var grown = 0;
            for (var i = 0; i < attempts && _cells.Count > 0; i++)
            {
                if (TryGrow(_cells[_random.NextInt(0, _cells.Count)]))
                {
                    grown++;
                }
            }

            return grown;
        }

        private bool TryGrow(CellCoord anchor)
        {
            var zone = _map.Zones.GetForCell(anchor);
            if (!zone.IsZoned || _random.NextDouble() >= _demand(zone.Type) || _occupancy.Check(new Footprint(anchor, 1, 1, 0)) != null)
            {
                return false;
            }

            var size = _map.Dimensions.CellSize;
            var centre = new float3((anchor.X + 0.5f) * size, 0f, (anchor.Z + 0.5f) * size);
            if (!_zones.TryNearestRoad(centre, out var roadPoint))
            {
                return false;
            }

            var turns = BuildingPlacement.QuarterTurnsToFace(roadPoint.xz - centre.xz);
            var fits = new List<(BuildingDefinition Definition, Footprint Footprint)>();
            foreach (var definition in _definitions)
            {
                var spec = definition.Spec;
                if (spec.Zone != zone.Type || spec.Density != zone.Density)
                {
                    continue;
                }

                var candidates = BuildingPlacement.Candidates(anchor, spec.FootprintWidth, spec.FootprintDepth, turns);
                var start = _random.NextInt(0, candidates.Count);
                for (var i = 0; i < candidates.Count; i++)
                {
                    var footprint = candidates[(start + i) % candidates.Count];
                    if (Fits(footprint, zone, out _))
                    {
                        fits.Add((definition, footprint));
                        break;
                    }
                }
            }

            var chosen = BuildingPlacement.PickWeighted(fits, f => f.Definition.Spec.FootprintCells, _random.NextDouble());
            if (chosen.Definition == null)
            {
                return false;
            }

            Fits(chosen.Footprint, zone, out var baseHeight);
            var middle = chosen.Footprint.Center(_map.Dimensions);
            _place(chosen.Definition, chosen.Footprint, new float3(middle.x, baseHeight, middle.y));
            return true;
        }

        // shortcut: Reason loopt per cel langs alle segmenten; bij veel wegen en kavels per cel cachen.
        private bool Fits(Footprint footprint, ZoneCell zone, out float baseHeight)
        {
            baseHeight = 0f;
            if (!footprint.IsInside(_map.Dimensions) || _occupancy.Check(footprint) != null)
            {
                return false;
            }

            if (BuildingPlacement.Cells(footprint).Any(cell => _map.Zones.GetForCell(cell) != zone || _zones.Reason(cell) != null))
            {
                return false;
            }

            var min = _map.Dimensions.CellMin(footprint.Origin);
            var max = _map.Dimensions.CellMin(footprint.Max) + new Vector2(_map.Dimensions.CellSize, _map.Dimensions.CellSize);
            var centre = footprint.Center(_map.Dimensions);
            _samples[0] = new float2(min.x, min.y);
            _samples[1] = new float2(max.x, min.y);
            _samples[2] = new float2(min.x, max.y);
            _samples[3] = new float2(max.x, max.y);
            _samples[4] = new float2(centre.x, centre.y);
            var low = float.MaxValue;
            var high = float.MinValue;
            foreach (var sample in _samples)
            {
                var height = _map.Height.Sample(new Vector2(sample.x, sample.y));
                low = math.min(low, height);
                high = math.max(high, height);
            }

            baseHeight = low;
            return high - low <= MaxRise;
        }

        private void ScanAll()
        {
            var layout = _map.Zones.Layout;
            Rescan(new RectInt(0, 0, layout.Width, layout.Depth));
        }

        private void Rescan(RectInt bounds)
        {
            var layout = _map.Zones.Layout;
            for (var z = Math.Max(bounds.yMin, 0); z < Math.Min(bounds.yMax, layout.Depth); z++)
            {
                for (var x = Math.Max(bounds.xMin, 0); x < Math.Min(bounds.xMax, layout.Width); x++)
                {
                    var cell = new CellCoord(x, z);
                    var zoned = _map.Zones.GetForCell(cell).IsZoned;
                    if (zoned && !_index.ContainsKey(cell))
                    {
                        _index[cell] = _cells.Count;
                        _cells.Add(cell);
                    }
                    else if (!zoned && _index.TryGetValue(cell, out var at))
                    {
                        var last = _cells[_cells.Count - 1];
                        _cells[at] = last;
                        _index[last] = at;
                        _cells.RemoveAt(_cells.Count - 1);
                        _index.Remove(cell);
                    }
                }
            }
        }
    }
}
