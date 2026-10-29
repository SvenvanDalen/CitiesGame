using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Cities.Core.Spatial
{
    /// <summary>
    /// Ruimtelijke index voor objecten met een omhullende rechthoek die weinig bewegen, zoals gebouwen, props en
    /// wegsegmenten. Elk object staat in alle cellen die zijn rechthoek raakt.
    /// </summary>
    /// <remarks>
    /// Zoekopdrachten alloceren niets: resultaten gaan naar een <see cref="NativeList{T}"/> van de aanroeper, en dubbele
    /// treffers (een object in meerdere cellen) worden weggefilterd met een zoekstempel per object.
    /// Per cel staat een gekoppelde lijst; de volgorde van resultaten hangt alleen af van de volgorde van wijzigingen
    /// en is dus deterministisch. Niet thread-safe, ook niet voor zoeken, want zoeken werkt de stempels bij.
    /// </remarks>
    public struct SpatialGrid<T> : IDisposable where T : unmanaged
    {
        private const int None = -1;

        private struct Entry
        {
            public T Value;
            public Aabb2 Bounds;
            public uint Stamp;
            public bool InUse;
        }

        private interface IBoundsFilter
        {
            bool Matches(Aabb2 bounds);
        }

        private struct RectFilter : IBoundsFilter
        {
            public Aabb2 Area;

            public bool Matches(Aabb2 bounds) => bounds.Overlaps(Area);
        }

        private struct CircleFilter : IBoundsFilter
        {
            public float2 Center;
            public float Radius;

            public bool Matches(Aabb2 bounds) => bounds.OverlapsCircle(Center, Radius);
        }

        private struct Link
        {
            public int Entry;
            public int Next;
        }

        private readonly GridGeometry _geometry;
        private NativeArray<int> _cellHeads;
        private NativeList<Entry> _entries;
        private NativeList<Link> _links;
        private NativeList<int> _freeEntries;
        private int _freeLinkHead;
        private uint _stamp;
        private int _count;

        // Hoe ver objecten maximaal buiten het raster uitsteken; ze staan in de randcellen en de straal moet die
        // randcellen dan ook vanaf buiten het raster bezoeken. Krimpt niet (behalve bij Clear), dat is veilig.
        private float _overhang;

        public SpatialGrid(GridGeometry geometry, int initialCapacity, Allocator allocator)
        {
            _geometry = geometry;
            _cellHeads = new NativeArray<int>(geometry.Length, allocator);
            for (var i = 0; i < _cellHeads.Length; i++)
            {
                _cellHeads[i] = None;
            }

            _entries = new NativeList<Entry>(initialCapacity, allocator);
            _links = new NativeList<Link>(initialCapacity * 2, allocator);
            _freeEntries = new NativeList<int>(allocator);
            _freeLinkHead = None;
            _stamp = 0;
            _count = 0;
            _overhang = 0f;
        }

        public GridGeometry Geometry => _geometry;

        public bool IsCreated => _cellHeads.IsCreated;

        public int Count => _count;

        /// <summary>Voegt een object toe en geeft een handle terug voor <see cref="Update"/> en <see cref="Remove"/>.</summary>
        public int Insert(T value, Aabb2 bounds)
        {
            int handle;
            if (_freeEntries.Length > 0)
            {
                handle = _freeEntries[_freeEntries.Length - 1];
                _freeEntries.RemoveAt(_freeEntries.Length - 1);
                _entries[handle] = new Entry { Value = value, Bounds = bounds, InUse = true };
            }
            else
            {
                handle = _entries.Length;
                _entries.Add(new Entry { Value = value, Bounds = bounds, InUse = true });
            }

            LinkEntry(handle, bounds);
            TrackOverhang(bounds);
            _count++;
            return handle;
        }

        public void Remove(int handle)
        {
            var entry = GetEntry(handle);
            UnlinkEntry(handle, entry.Bounds);
            entry.InUse = false;
            _entries[handle] = entry;
            _freeEntries.Add(handle);
            _count--;
        }

        /// <summary>Verplaatst of vergroot een object. Blijft het in dezelfde cellen, dan kost dit vrijwel niets.</summary>
        public void Update(int handle, Aabb2 bounds)
        {
            var entry = GetEntry(handle);
            _geometry.CellRange(entry.Bounds, out var oldMin, out var oldMax);
            _geometry.CellRange(bounds, out var newMin, out var newMax);
            if (math.any(oldMin != newMin) || math.any(oldMax != newMax))
            {
                UnlinkEntry(handle, entry.Bounds);
                LinkEntry(handle, bounds);
            }

            entry.Bounds = bounds;
            _entries[handle] = entry;
            TrackOverhang(bounds);
        }

        public T GetValue(int handle) => GetEntry(handle).Value;

        public Aabb2 GetBounds(int handle) => GetEntry(handle).Bounds;

        /// <summary>Alle objecten waarvan de rechthoek het gebied raakt.</summary>
        public void QueryRect(Aabb2 area, NativeList<T> results) =>
            Query(area, new RectFilter { Area = area }, results);

        /// <summary>Alle objecten waarvan de rechthoek binnen <paramref name="radius"/> van een punt ligt.</summary>
        public void QueryRadius(float2 center, float radius, NativeList<T> results) =>
            Query(Aabb2.FromCircle(center, radius), new CircleFilter { Center = center, Radius = radius }, results);

        /// <summary>
        /// Het dichtstbijzijnde object langs een straal in het grondvlak; voor picking. Stopt zodra de volgende cel
        /// verder ligt dan de beste treffer.
        /// </summary>
        /// <param name="direction">Genormaliseerde richting.</param>
        public bool Raycast(float2 origin, float2 direction, float maxDistance, out T hit, out float distance)
        {
            hit = default;
            distance = float.PositiveInfinity;
            var stamp = NextStamp();

            // Loop over een raster dat de uitstekende objecten mee omvat; cellen daarbuiten vallen op de randcellen.
            var margin = (int)math.ceil(_overhang / _geometry.CellSize);
            var walkGeometry = margin == 0 ? _geometry : new GridGeometry(
                _geometry.Origin - (margin * _geometry.CellSize), _geometry.CellSize, _geometry.CellCount + (2 * margin));
            var walker = new GridRayWalker(walkGeometry, origin, direction, maxDistance);
            while (walker.MoveNext() && walker.EntryDistance <= distance)
            {
                var cell = math.clamp(walker.Cell - margin, 0, _geometry.CellCount - 1);
                for (var link = _cellHeads[_geometry.IndexUnchecked(cell)]; link != None; link = _links[link].Next)
                {
                    var index = _links[link].Entry;
                    var entry = _entries[index];
                    if (entry.Stamp == stamp)
                    {
                        continue;
                    }

                    entry.Stamp = stamp;
                    _entries[index] = entry;
                    if (entry.Bounds.Raycast(origin, direction, maxDistance, out var t) && t < distance)
                    {
                        distance = t;
                        hit = entry.Value;
                    }
                }
            }

            return !float.IsPositiveInfinity(distance);
        }

        public void Clear()
        {
            for (var i = 0; i < _cellHeads.Length; i++)
            {
                _cellHeads[i] = None;
            }

            _entries.Clear();
            _links.Clear();
            _freeEntries.Clear();
            _freeLinkHead = None;
            _count = 0;
            _overhang = 0f;
        }

        public void Dispose()
        {
            if (_cellHeads.IsCreated)
            {
                _cellHeads.Dispose();
                _entries.Dispose();
                _links.Dispose();
                _freeEntries.Dispose();
            }
        }

        private void Query<TFilter>(Aabb2 area, TFilter filter, NativeList<T> results) where TFilter : struct, IBoundsFilter
        {
            var stamp = NextStamp();
            _geometry.CellRange(area, out var min, out var max);
            for (var y = min.y; y <= max.y; y++)
            {
                for (var x = min.x; x <= max.x; x++)
                {
                    for (var link = _cellHeads[_geometry.IndexUnchecked(new int2(x, y))]; link != None; link = _links[link].Next)
                    {
                        var index = _links[link].Entry;
                        var entry = _entries[index];
                        if (entry.Stamp == stamp)
                        {
                            continue;
                        }

                        entry.Stamp = stamp;
                        _entries[index] = entry;
                        if (filter.Matches(entry.Bounds))
                        {
                            results.Add(entry.Value);
                        }
                    }
                }
            }
        }

        private Entry GetEntry(int handle)
        {
            if (handle < 0 || handle >= _entries.Length || !_entries[handle].InUse)
            {
                throw new ArgumentException($"Ongeldige handle {handle}.", nameof(handle));
            }

            return _entries[handle];
        }

        private void TrackOverhang(Aabb2 bounds)
        {
            var grid = _geometry.Bounds;
            var outside = math.max(grid.Min - bounds.Min, bounds.Max - grid.Max);
            _overhang = math.max(_overhang, math.cmax(outside));
        }

        private uint NextStamp()
        {
            _stamp++;
            if (_stamp == 0)
            {
                // Na 4 miljard zoekopdrachten opnieuw beginnen, zodat oude stempels niet als "al gezien" tellen.
                for (var i = 0; i < _entries.Length; i++)
                {
                    var entry = _entries[i];
                    entry.Stamp = 0;
                    _entries[i] = entry;
                }

                _stamp = 1;
            }

            return _stamp;
        }

        private void LinkEntry(int handle, Aabb2 bounds)
        {
            _geometry.CellRange(bounds, out var min, out var max);
            for (var y = min.y; y <= max.y; y++)
            {
                for (var x = min.x; x <= max.x; x++)
                {
                    var cell = _geometry.IndexUnchecked(new int2(x, y));
                    int link;
                    if (_freeLinkHead != None)
                    {
                        link = _freeLinkHead;
                        _freeLinkHead = _links[link].Next;
                        _links[link] = new Link { Entry = handle, Next = _cellHeads[cell] };
                    }
                    else
                    {
                        link = _links.Length;
                        _links.Add(new Link { Entry = handle, Next = _cellHeads[cell] });
                    }

                    _cellHeads[cell] = link;
                }
            }
        }

        private void UnlinkEntry(int handle, Aabb2 bounds)
        {
            _geometry.CellRange(bounds, out var min, out var max);
            for (var y = min.y; y <= max.y; y++)
            {
                for (var x = min.x; x <= max.x; x++)
                {
                    var cell = _geometry.IndexUnchecked(new int2(x, y));
                    var previous = None;
                    var link = _cellHeads[cell];
                    while (link != None)
                    {
                        var current = _links[link];
                        if (current.Entry != handle)
                        {
                            previous = link;
                            link = current.Next;
                            continue;
                        }

                        if (previous == None)
                        {
                            _cellHeads[cell] = current.Next;
                        }
                        else
                        {
                            var before = _links[previous];
                            before.Next = current.Next;
                            _links[previous] = before;
                        }

                        _links[link] = new Link { Entry = None, Next = _freeLinkHead };
                        _freeLinkHead = link;
                        break;
                    }
                }
            }
        }
    }
}
