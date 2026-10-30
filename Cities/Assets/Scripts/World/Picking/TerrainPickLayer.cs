using System;
using Cities.World.Terrain;
using Unity.Mathematics;

namespace Cities.World.Picking
{
    /// <summary>
    /// Straal tegen het hoogteveld (terrein of water, wat hoger is) binnen de kaart.
    /// </summary>
    /// <remarks>
    /// Stapt langs de straal met een stapgrootte uit de hoogte boven het oppervlak en een bovengrens voor de helling
    /// (<see cref="MaxSlope"/>): zolang het terrein niet steiler is, kan de straal het oppervlak binnen die stap niet
    /// raken. Hoog boven de grond zijn dat dus grote stappen, vlak erboven kleine. Na het doorsnijden wordt het punt
    /// met bisectie verfijnd tot op enkele centimeters.
    /// </remarks>
    public sealed class TerrainPickLayer : IPickLayer
    {
        private const int MaxSteps = 4096;
        private const int RefineSteps = 20;

        private readonly ITerrainHeight _terrain;
        private readonly float2 _min;
        private readonly float2 _max;

        /// <param name="worldSize">Afmetingen van de kaart in meters (X en Z), vanaf de oorsprong.</param>
        /// <param name="maxSlope">Hoogteverschil per meter dat het terrein hoogstens heeft; 2 is ruim 63 graden.</param>
        /// <param name="minStep">Kleinste stap in meters; begrenst de kosten bij scheervlakke stralen.</param>
        public TerrainPickLayer(ITerrainHeight terrain, float2 worldSize, float maxSlope = 2f, float minStep = 1f)
        {
            _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
            if (!math.all(worldSize > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(worldSize), worldSize, "De kaart moet een positieve grootte hebben.");
            }

            if (!(maxSlope > 0f) || !(minStep > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(maxSlope), "Helling en stap moeten groter dan 0 zijn.");
            }

            _min = float2.zero;
            _max = worldSize;
            MaxSlope = maxSlope;
            MinStep = minStep;
        }

        public PickKind Kind => PickKind.Terrain;

        public bool IsSolid => true;

        public float MaxSlope { get; }

        public float MinStep { get; }

        public bool Raycast(in PickRay ray, float maxDistance, out PickHit hit)
        {
            hit = PickHit.None;
            if (!ClipToMap(ray, math.min(maxDistance, ray.Length), out var t, out var end))
            {
                return false;
            }

            var gap = Gap(ray, t);
            if (gap <= 0f)
            {
                // De straal begint onder het oppervlak of komt de kaart binnen door de zijkant van een berg.
                hit = HitAt(ray, t);
                return true;
            }

            // Hoe snel de afstand tot het oppervlak hoogstens krimpt per meter langs de straal.
            var closing = math.max((MaxSlope * math.length(ray.Direction.xz)) - ray.Direction.y, 1e-4f);
            for (var step = 0; step < MaxSteps && t < end; step++)
            {
                var next = math.min(t + math.max(gap / closing, MinStep), end);
                var nextGap = Gap(ray, next);
                if (nextGap <= 0f)
                {
                    hit = HitAt(ray, Refine(ray, t, next));
                    return true;
                }

                t = next;
                gap = nextGap;
            }

            return false;
        }

        private float Gap(in PickRay ray, float t)
        {
            var point = ray.Point(t);
            return point.y - _terrain.SurfaceHeight(point.xz);
        }

        private float Refine(in PickRay ray, float above, float below)
        {
            for (var i = 0; i < RefineSteps; i++)
            {
                var middle = (above + below) * 0.5f;
                if (Gap(ray, middle) > 0f)
                {
                    above = middle;
                }
                else
                {
                    below = middle;
                }
            }

            return below;
        }

        private PickHit HitAt(in PickRay ray, float t)
        {
            var point = ray.Point(t);
            point.y = _terrain.SurfaceHeight(point.xz);
            return new PickHit(PickKind.Terrain, 0UL, point, t);
        }

        /// <summary>Het deel van de straal boven de kaart (in het grondvlak), begrensd op <paramref name="maxDistance"/>.</summary>
        private bool ClipToMap(in PickRay ray, float maxDistance, out float enter, out float exit)
        {
            enter = 0f;
            exit = maxDistance;
            for (var axis = 0; axis < 2; axis++)
            {
                var origin = ray.Origin.xz[axis];
                var direction = ray.Direction.xz[axis];
                if (math.abs(direction) < 1e-9f)
                {
                    if (origin < _min[axis] || origin > _max[axis])
                    {
                        return false;
                    }

                    continue;
                }

                var t0 = (_min[axis] - origin) / direction;
                var t1 = (_max[axis] - origin) / direction;
                enter = math.max(enter, math.min(t0, t1));
                exit = math.min(exit, math.max(t0, t1));
            }

            return enter <= exit;
        }
    }
}
