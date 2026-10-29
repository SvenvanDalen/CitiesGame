using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Cities.Core.Spatial
{
    /// <summary>
    /// Ruimtelijke index voor veel bewegende punten (burgers, voertuigen), die elke tick in één keer opnieuw wordt
    /// opgebouwd met een counting sort: O(n), zonder allocaties zolang de capaciteit volstaat, en een geheugenvolgorde
    /// waarin buren dicht bij elkaar liggen.
    /// </summary>
    /// <remarks>
    /// Na <see cref="ScheduleBuild"/> zijn <see cref="CellStart"/>, <see cref="CellCount"/> en <see cref="SortedIndices"/>
    /// alleen-lezen bruikbaar in jobs die van de build-handle afhangen. Indices verwijzen naar de positie in de
    /// invoerarray.
    /// </remarks>
    public struct PointGrid : IDisposable
    {
        private readonly GridGeometry _geometry;
        private NativeArray<int> _cellStart;
        private NativeArray<int> _cellCount;
        private NativeList<int> _sorted;
        private NativeList<int> _cellOfPoint;

        public PointGrid(GridGeometry geometry, int initialCapacity, Allocator allocator)
        {
            _geometry = geometry;
            _cellStart = new NativeArray<int>(geometry.Length, allocator);
            _cellCount = new NativeArray<int>(geometry.Length, allocator);
            _sorted = new NativeList<int>(initialCapacity, allocator);
            _cellOfPoint = new NativeList<int>(initialCapacity, allocator);
        }

        public GridGeometry Geometry => _geometry;

        public bool IsCreated => _cellStart.IsCreated;

        /// <summary>Eerste plek in <see cref="SortedIndices"/> per cel.</summary>
        public NativeArray<int> CellStart => _cellStart;

        /// <summary>Aantal punten per cel.</summary>
        public NativeArray<int> CellCount => _cellCount;

        /// <summary>Indices van de punten, gegroepeerd per cel.</summary>
        public NativeArray<int> SortedIndices => _sorted.AsArray();

        /// <summary>Plant de herbouw voor de opgegeven posities.</summary>
        public JobHandle ScheduleBuild(NativeArray<float2> positions, JobHandle dependency = default)
        {
            _sorted.ResizeUninitialized(positions.Length);
            _cellOfPoint.ResizeUninitialized(positions.Length);
            return new BuildJob
            {
                Geometry = _geometry,
                Positions = positions,
                CellStart = _cellStart,
                CellCount = _cellCount,
                CellOfPoint = _cellOfPoint.AsArray(),
                Sorted = _sorted.AsArray(),
            }.Schedule(dependency);
        }

        /// <summary>Herbouwt direct op de huidige thread.</summary>
        public void Build(NativeArray<float2> positions) => ScheduleBuild(positions).Complete();

        /// <summary>Indices van alle punten binnen <paramref name="radius"/> van <paramref name="center"/>.</summary>
        public readonly void QueryRadius(NativeArray<float2> positions, float2 center, float radius, NativeList<int> results)
        {
            var radiusSq = radius * radius;
            _geometry.CellRange(Aabb2.FromCircle(center, radius), out var min, out var max);
            var sorted = _sorted.AsArray();
            for (var y = min.y; y <= max.y; y++)
            {
                for (var x = min.x; x <= max.x; x++)
                {
                    var cell = _geometry.IndexUnchecked(new int2(x, y));
                    var end = _cellStart[cell] + _cellCount[cell];
                    for (var i = _cellStart[cell]; i < end; i++)
                    {
                        var point = sorted[i];
                        if (math.distancesq(positions[point], center) <= radiusSq)
                        {
                            results.Add(point);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Het dichtstbijzijnde punt binnen <paramref name="maxRadius"/>, of -1. Zoekt in ringen van cellen naar buiten
        /// en stopt zodra een ring verder ligt dan de beste treffer.
        /// </summary>
        public readonly int FindNearest(NativeArray<float2> positions, float2 center, float maxRadius, int excludeIndex = -1)
        {
            var best = -1;
            var bestSq = maxRadius * maxRadius;
            var origin = _geometry.CellOf(center);
            var maxRing = (int)math.ceil(maxRadius / _geometry.CellSize) + 1;
            var sorted = _sorted.AsArray();
            for (var ring = 0; ring <= maxRing; ring++)
            {
                // De dichtstbijzijnde rand van deze ring ligt minstens (ring - 1) cellen ver.
                var ringDistance = math.max(ring - 1, 0) * _geometry.CellSize;
                if (best >= 0 && ringDistance * ringDistance > bestSq)
                {
                    break;
                }

                for (var y = origin.y - ring; y <= origin.y + ring; y++)
                {
                    for (var x = origin.x - ring; x <= origin.x + ring; x++)
                    {
                        var onRing = math.abs(x - origin.x) == ring || math.abs(y - origin.y) == ring;
                        if (!onRing || x < 0 || y < 0 || x >= _geometry.CellCount.x || y >= _geometry.CellCount.y)
                        {
                            continue;
                        }

                        var cell = _geometry.IndexUnchecked(new int2(x, y));
                        var end = _cellStart[cell] + _cellCount[cell];
                        for (var i = _cellStart[cell]; i < end; i++)
                        {
                            var point = sorted[i];
                            var distanceSq = math.distancesq(positions[point], center);
                            if (point != excludeIndex && (distanceSq < bestSq || (distanceSq <= bestSq && best < 0)))
                            {
                                best = point;
                                bestSq = distanceSq;
                            }
                        }
                    }
                }
            }

            return best;
        }

        public void Dispose()
        {
            if (_cellStart.IsCreated)
            {
                _cellStart.Dispose();
                _cellCount.Dispose();
                _sorted.Dispose();
                _cellOfPoint.Dispose();
            }
        }

        [BurstCompile]
        private struct BuildJob : IJob
        {
            public GridGeometry Geometry;
            [ReadOnly] public NativeArray<float2> Positions;
            public NativeArray<int> CellStart;
            public NativeArray<int> CellCount;
            public NativeArray<int> CellOfPoint;
            public NativeArray<int> Sorted;

            public void Execute()
            {
                for (var i = 0; i < CellCount.Length; i++)
                {
                    CellCount[i] = 0;
                }

                for (var i = 0; i < Positions.Length; i++)
                {
                    var cell = Geometry.IndexOf(Positions[i]);
                    CellOfPoint[i] = cell;
                    CellCount[cell]++;
                }

                var offset = 0;
                for (var i = 0; i < CellStart.Length; i++)
                {
                    CellStart[i] = offset;
                    offset += CellCount[i];
                }

                // Tweede pass vult per cel in invoervolgorde; CellCount dient tijdelijk als schrijfpositie.
                for (var i = 0; i < CellCount.Length; i++)
                {
                    CellCount[i] = 0;
                }

                for (var i = 0; i < Positions.Length; i++)
                {
                    var cell = CellOfPoint[i];
                    Sorted[CellStart[cell] + CellCount[cell]] = i;
                    CellCount[cell]++;
                }
            }
        }
    }
}
