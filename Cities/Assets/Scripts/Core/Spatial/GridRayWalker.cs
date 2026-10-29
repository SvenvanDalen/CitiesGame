using Unity.Mathematics;

namespace Cities.Core.Spatial
{
    /// <summary>
    /// Loopt de cellen langs een straal af in de volgorde waarin de straal ze raakt (Amanatides–Woo).
    /// Een struct zonder allocaties, dus ook bruikbaar in Burst-jobs.
    /// </summary>
    /// <example>
    /// <code>
    /// var walker = new GridRayWalker(geometry, origin, direction, maxDistance);
    /// while (walker.MoveNext()) { Visit(walker.Cell, walker.EntryDistance); }
    /// </code>
    /// </example>
    public struct GridRayWalker
    {
        private readonly int2 _step;
        private readonly float2 _delta;
        private readonly float _maxDistance;
        private readonly int2 _cellCount;
        private float2 _next;
        private bool _started;
        private bool _finished;

        /// <param name="direction">Genormaliseerde richting in het grondvlak.</param>
        public GridRayWalker(GridGeometry geometry, float2 origin, float2 direction, float maxDistance)
        {
            _cellCount = geometry.CellCount;
            _maxDistance = maxDistance;
            _started = false;
            EntryDistance = 0f;

            // Start op de rand van het raster als de oorsprong erbuiten ligt.
            var bounds = geometry.Bounds;
            var startDistance = 0f;
            _finished = !bounds.Contains(origin) && !bounds.Raycast(origin, direction, maxDistance, out startDistance);
            var start = origin + (direction * startDistance);
            EntryDistance = startDistance;
            Cell = geometry.CellOf(start);

            _step = (int2)math.sign(direction);
            var inverse = math.abs(1f / direction);
            _delta = inverse * geometry.CellSize;
            var cellMin = geometry.Origin + ((float2)Cell * geometry.CellSize);
            var boundary = math.select(cellMin, cellMin + geometry.CellSize, direction > 0f);
            var toBoundary = math.abs(boundary - start) * inverse;
            _next = startDistance + math.select(toBoundary, float.PositiveInfinity, direction == 0f);
        }

        /// <summary>De huidige cel; geldig na een <see cref="MoveNext"/> die true gaf.</summary>
        public int2 Cell { get; private set; }

        /// <summary>Afstand langs de straal waarop de straal de huidige cel binnengaat.</summary>
        public float EntryDistance { get; private set; }

        public bool MoveNext()
        {
            if (_finished)
            {
                return false;
            }

            if (!_started)
            {
                _started = true;
                return true;
            }

            var alongX = _next.x < _next.y;
            EntryDistance = alongX ? _next.x : _next.y;
            if (EntryDistance > _maxDistance)
            {
                _finished = true;
                return false;
            }

            if (alongX)
            {
                Cell = new int2(Cell.x + _step.x, Cell.y);
                _next.x += _delta.x;
            }
            else
            {
                Cell = new int2(Cell.x, Cell.y + _step.y);
                _next.y += _delta.y;
            }

            if (math.any(Cell < 0) || math.any(Cell >= _cellCount))
            {
                _finished = true;
                return false;
            }

            return true;
        }
    }
}
