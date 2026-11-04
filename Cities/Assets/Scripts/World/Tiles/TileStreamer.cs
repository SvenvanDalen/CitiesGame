using System;
using System.Collections.Generic;
using Cities.World.Grid;
using UnityEngine;

namespace Cities.World.Tiles
{
    /// <summary>
    /// Bepaalt welke tegels in het geheugen horen (E23-07). Rond een brandpunt (meestal de camera) worden tegels binnen
    /// <c>LoadRadius</c> gevraagd, de dichtstbijzijnde eerst, nooit meer dan het geheugenbudget toelaat. Tegels buiten
    /// <c>LoadRadius + UnloadMargin</c> of boven het budget worden vrijgegeven, de verste eerst; de marge voorkomt dat
    /// een tegel op de rand telkens laadt en weer vrijkomt. Het laden zelf (en de culling van wat zichtbaar is) doet de
    /// aanroeper met de lijsten uit <see cref="Update"/>.
    /// </summary>
    public sealed class TileStreamer
    {
        private readonly WorldDimensions _dimensions;
        private readonly HashSet<TileCoord> _resident = new HashSet<TileCoord>();
        private readonly List<(float Distance, TileCoord Tile)> _scratch = new List<(float, TileCoord)>();
        private readonly List<TileCoord> _toLoad = new List<TileCoord>();
        private readonly List<TileCoord> _toUnload = new List<TileCoord>();

        /// <param name="bytesPerTile">Geschat geheugen van een geladen tegel.</param>
        /// <param name="memoryBudgetBytes">Vast budget voor geladen tegels; bepaalt <see cref="MaxResident"/>.</param>
        /// <param name="loadRadius">Afstand in meters tot het brandpunt waarbinnen tegels geladen worden.</param>
        /// <param name="unloadMargin">Extra afstand voordat een geladen tegel weer vrijkomt.</param>
        public TileStreamer(WorldDimensions dimensions, long bytesPerTile, long memoryBudgetBytes, float loadRadius, float unloadMargin = 0f)
        {
            if (bytesPerTile < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(bytesPerTile), "Een tegel heeft minstens één byte.");
            }

            if (memoryBudgetBytes < bytesPerTile)
            {
                throw new ArgumentOutOfRangeException(nameof(memoryBudgetBytes), "Het budget past niet eens één tegel.");
            }

            if (!(loadRadius > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(loadRadius), "De straal moet positief zijn.");
            }

            if (unloadMargin < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(unloadMargin), "De marge kan niet negatief zijn.");
            }

            _dimensions = dimensions;
            BytesPerTile = bytesPerTile;
            MemoryBudgetBytes = memoryBudgetBytes;
            LoadRadius = loadRadius;
            UnloadMargin = unloadMargin;
            MaxResident = (int)Math.Min(dimensions.TileCount, memoryBudgetBytes / bytesPerTile);
        }

        public long BytesPerTile { get; }

        public long MemoryBudgetBytes { get; }

        public float LoadRadius { get; }

        public float UnloadMargin { get; }

        /// <summary>Hoeveel tegels tegelijk geladen mogen zijn.</summary>
        public int MaxResident { get; }

        public int ResidentCount => _resident.Count;

        public long ResidentBytes => ResidentCount * BytesPerTile;

        public bool IsResident(TileCoord tile) => _resident.Contains(tile);

        /// <summary>Tegels die de laatste <see cref="Update"/> wil laden, dichtstbijzijnde eerst.</summary>
        public IReadOnlyList<TileCoord> ToLoad => _toLoad;

        /// <summary>Tegels die de laatste <see cref="Update"/> wil vrijgeven, verste eerst.</summary>
        public IReadOnlyList<TileCoord> ToUnload => _toUnload;

        /// <summary>Afstand in meters van een positie tot de dichtstbijzijnde rand van een tegel (0 als erbinnen).</summary>
        public float DistanceTo(TileCoord tile, Vector2 focus)
        {
            var size = _dimensions.TileSize;
            var minX = tile.X * size;
            var minZ = tile.Z * size;
            var dx = Math.Max(Math.Max(minX - focus.x, 0f), focus.x - (minX + size));
            var dz = Math.Max(Math.Max(minZ - focus.y, 0f), focus.y - (minZ + size));
            return (float)Math.Sqrt((dx * dx) + (dz * dz));
        }

        /// <summary>
        /// Werkt de set bij voor een nieuw brandpunt en vult <see cref="ToLoad"/> en <see cref="ToUnload"/>. De tegels
        /// gelden daarna als geladen/vrijgegeven; <paramref name="maxLoads"/> beperkt het werk per aanroep.
        /// </summary>
        public void Update(Vector2 focus, int maxLoads = int.MaxValue)
        {
            _toLoad.Clear();
            _toUnload.Clear();
            Unload(focus);
            Load(focus, maxLoads);
        }

        private void Unload(Vector2 focus)
        {
            _scratch.Clear();
            foreach (var tile in _resident)
            {
                _scratch.Add((DistanceTo(tile, focus), tile));
            }

            // Van ver naar dichtbij: te ver weg valt af, en bij een overschot vallen de verste af.
            _scratch.Sort(CompareDescending);
            var kept = 0;
            foreach (var entry in _scratch)
            {
                if (entry.Distance > LoadRadius + UnloadMargin)
                {
                    _toUnload.Add(entry.Tile);
                }
                else
                {
                    kept++;
                }
            }

            var surplus = kept - MaxResident;
            foreach (var entry in _scratch)
            {
                if (surplus <= 0)
                {
                    break;
                }

                if (entry.Distance <= LoadRadius + UnloadMargin)
                {
                    _toUnload.Add(entry.Tile);
                    surplus--;
                }
            }

            foreach (var tile in _toUnload)
            {
                _resident.Remove(tile);
            }
        }

        private void Load(Vector2 focus, int maxLoads)
        {
            _scratch.Clear();
            var tileRange = (int)Math.Ceiling(LoadRadius / _dimensions.TileSize) + 1;
            var centerX = (int)Math.Floor(focus.x / _dimensions.TileSize);
            var centerZ = (int)Math.Floor(focus.y / _dimensions.TileSize);
            for (var z = Math.Max(centerZ - tileRange, 0); z <= Math.Min(centerZ + tileRange, _dimensions.TilesZ - 1); z++)
            {
                for (var x = Math.Max(centerX - tileRange, 0); x <= Math.Min(centerX + tileRange, _dimensions.TilesX - 1); x++)
                {
                    var tile = new TileCoord(x, z);
                    var distance = DistanceTo(tile, focus);
                    if (distance <= LoadRadius && !_resident.Contains(tile))
                    {
                        _scratch.Add((distance, tile));
                    }
                }
            }

            _scratch.Sort(CompareAscending);
            foreach (var entry in _scratch)
            {
                if (_toLoad.Count >= maxLoads)
                {
                    break;
                }

                if (_resident.Count >= MaxResident && !EvictFartherThan(entry.Distance, focus))
                {
                    break;
                }

                _resident.Add(entry.Tile);
                _toLoad.Add(entry.Tile);
            }
        }

        /// <summary>Geeft de verste geladen tegel vrij als die verder weg ligt dan <paramref name="distance"/>.</summary>
        private bool EvictFartherThan(float distance, Vector2 focus)
        {
            var farthest = default(TileCoord);
            var farthestDistance = distance;
            var found = false;
            foreach (var tile in _resident)
            {
                var d = DistanceTo(tile, focus);
                if (d > farthestDistance || (found && d.CompareTo(farthestDistance) == 0 && CompareTiles(tile, farthest) > 0))
                {
                    farthest = tile;
                    farthestDistance = d;
                    found = true;
                }
            }

            if (!found)
            {
                return false;
            }

            _resident.Remove(farthest);
            _toUnload.Add(farthest);
            return true;
        }

        private static int CompareTiles(TileCoord a, TileCoord b)
        {
            var byZ = a.Z.CompareTo(b.Z);
            return byZ != 0 ? byZ : a.X.CompareTo(b.X);
        }

        private static int CompareAscending((float Distance, TileCoord Tile) first, (float Distance, TileCoord Tile) second)
        {
            var byDistance = first.Distance.CompareTo(second.Distance);
            if (byDistance != 0)
            {
                return byDistance;
            }

            return CompareTiles(first.Tile, second.Tile);
        }

        private static int CompareDescending((float Distance, TileCoord Tile) a, (float Distance, TileCoord Tile) b) =>
            CompareAscending(b, a);
    }
}
