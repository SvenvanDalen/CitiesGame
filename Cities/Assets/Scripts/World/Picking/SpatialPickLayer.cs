using System;
using System.Collections.Generic;
using Cities.Core.Spatial;
using Unity.Collections;
using Unity.Mathematics;

namespace Cities.World.Picking
{
    /// <summary>
    /// Pickbare vormen in een <see cref="SpatialGrid{T}"/>. De straal loopt alleen de cellen eronder af en test daar de
    /// echte 3D-vorm; zo kost picken tussen honderdduizenden objecten een fractie van een milliseconde. Systemen houden
    /// hun objecten bij met <see cref="Set(in TShape)"/> en <see cref="Remove"/>.
    /// </summary>
    /// <remarks>Gebruikt native geheugen; altijd <see cref="Dispose"/> aanroepen. Alleen op de main thread.</remarks>
    public abstract class SpatialPickLayer<TShape> : IPickLayer, IDisposable where TShape : unmanaged, IPickShape
    {
        private readonly Dictionary<ulong, List<int>> _handles = new Dictionary<ulong, List<int>>();
        private readonly Stack<List<int>> _spareLists = new Stack<List<int>>();
        private SpatialGrid<TShape> _grid;

        /// <param name="margin">Extra pickstraal per meter afstand; 0,003 is ongeveer 3 pixels bij 60 graden beeldhoek op 1080p.</param>
        /// <param name="maxMargin">Grootste extra straal in meters.</param>
        protected SpatialPickLayer(PickKind kind, GridGeometry geometry, int initialCapacity, float margin, float maxMargin)
        {
            if (kind == PickKind.None)
            {
                throw new ArgumentException("Een laag heeft een soort nodig.", nameof(kind));
            }

            if (margin < 0f || maxMargin < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(margin), "De marge kan niet negatief zijn.");
            }

            Kind = kind;
            Margin = margin;
            MaxMargin = maxMargin;
            _grid = new SpatialGrid<TShape>(geometry, initialCapacity, Allocator.Persistent);
        }

        public PickKind Kind { get; }

        public abstract bool IsSolid { get; }

        public float Margin { get; }

        public float MaxMargin { get; }

        /// <summary>Aantal objecten (niet vormen).</summary>
        public int Count => _handles.Count;

        public bool Contains(ulong id) => _handles.ContainsKey(id);

        /// <summary>Plaatst of verplaatst een object met één vorm.</summary>
        public void Set(in TShape shape)
        {
            Span<TShape> one = stackalloc TShape[1];
            one[0] = shape;
            Set(shape.Id, one);
        }

        /// <summary>Vervangt alle vormen van een object, bijvoorbeeld de stukken van een gebogen weg.</summary>
        public void Set(ulong id, ReadOnlySpan<TShape> shapes)
        {
            if (shapes.IsEmpty)
            {
                Remove(id);
                return;
            }

            foreach (var shape in shapes)
            {
                if (shape.Id != id)
                {
                    throw new ArgumentException($"Vorm voor object {shape.Id} hoort niet bij object {id}.", nameof(shapes));
                }
            }

            if (!_handles.TryGetValue(id, out var handles))
            {
                handles = _spareLists.Count > 0 ? _spareLists.Pop() : new List<int>(shapes.Length);
                _handles.Add(id, handles);
            }

            var padding = MaxMargin;
            for (var i = 0; i < shapes.Length; i++)
            {
                var footprint = shapes[i].Footprint(padding);
                if (i < handles.Count)
                {
                    _grid.Update(handles[i], footprint);
                    _grid.SetValue(handles[i], shapes[i]);
                }
                else
                {
                    handles.Add(_grid.Insert(shapes[i], footprint));
                }
            }

            for (var i = handles.Count - 1; i >= shapes.Length; i--)
            {
                _grid.Remove(handles[i]);
                handles.RemoveAt(i);
            }
        }

        public bool Remove(ulong id)
        {
            if (!_handles.TryGetValue(id, out var handles))
            {
                return false;
            }

            foreach (var handle in handles)
            {
                _grid.Remove(handle);
            }

            handles.Clear();
            _spareLists.Push(handles);
            _handles.Remove(id);
            return true;
        }

        public void Clear()
        {
            _grid.Clear();
            foreach (var handles in _handles.Values)
            {
                handles.Clear();
                _spareLists.Push(handles);
            }

            _handles.Clear();
        }

        public bool Raycast(in PickRay ray, float maxDistance, out PickHit hit)
        {
            hit = PickHit.None;
            maxDistance = math.min(maxDistance, ray.Length);
            var horizontal = math.length(ray.Direction.xz);
            var vertical = horizontal < 1e-5f;
            var visitor = new NearestShapeVisitor<TShape>
            {
                Ray = ray,
                PlanarOrigin = ray.Origin.xz,
                PlanarDirection = vertical ? float2.zero : ray.Direction.xz / horizontal,
                Vertical = vertical,
                MaxDistance = maxDistance,
                Horizontal = vertical ? 0f : horizontal,
                Margin = Margin,
                MaxMargin = MaxMargin,
                PlanarLimit = vertical ? 0f : maxDistance * horizontal,
                BestScore = float.PositiveInfinity,
                BestDistance = float.PositiveInfinity,
            };

            _grid.Raycast(visitor.PlanarOrigin, visitor.PlanarDirection, visitor.PlanarLimit, ref visitor);
            if (float.IsPositiveInfinity(visitor.BestScore))
            {
                return false;
            }

            hit = new PickHit(Kind, visitor.BestId, ray.Point(visitor.BestDistance), visitor.BestDistance);
            return true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _grid.Dispose();
                _handles.Clear();
                _spareLists.Clear();
            }
        }
    }

    /// <summary>Beste vorm langs de straal: eerst de laagste score (echte treffer), dan de kleinste afstand.</summary>
    internal struct NearestShapeVisitor<TShape> : ISpatialRayVisitor<TShape> where TShape : unmanaged, IPickShape
    {
        public PickRay Ray;
        public float2 PlanarOrigin;
        public float2 PlanarDirection;
        public bool Vertical;
        public float MaxDistance;
        public float Horizontal;
        public float Margin;
        public float MaxMargin;
        public float PlanarLimit;
        public float BestScore;
        public float BestDistance;
        public ulong BestId;

        // Na een echte treffer kan niets verder weg nog winnen; treffers binnen de marge kunnen nog verbeteren.
        public float StopDistance => BestScore <= 0f ? BestDistance * Horizontal : PlanarLimit;

        public void Visit(in TShape value, Aabb2 bounds)
        {
            // Eerst goedkoop in het grondvlak: de meeste objecten in een cel liggen naast de straal.
            var crosses = Vertical ? bounds.Contains(PlanarOrigin) : bounds.Raycast(PlanarOrigin, PlanarDirection, PlanarLimit, out _);
            if (crosses && value.Test(Ray, MaxDistance, Margin, MaxMargin, out var distance, out var score)
                && (score < BestScore || (score <= BestScore && distance < BestDistance)))
            {
                BestScore = score;
                BestDistance = distance;
                BestId = value.Id;
            }
        }
    }
}
