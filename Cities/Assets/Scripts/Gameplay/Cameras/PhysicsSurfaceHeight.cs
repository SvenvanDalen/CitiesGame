using Cities.World.Terrain;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Gameplay.Cameras
{
    /// <summary>
    /// Oppervlaktehoogte via een verticale raycast tegen colliders; voor testscenes zonder <c>WorldMap</c>.
    /// Raakt de straal niets, dan geldt <see cref="FallbackHeight"/>.
    /// </summary>
    public sealed class PhysicsSurfaceHeight : ITerrainHeight
    {
        private const float RayStart = 10_000f;

        private readonly LayerMask _layers;

        public PhysicsSurfaceHeight(LayerMask layers, float fallbackHeight = 0f)
        {
            _layers = layers;
            FallbackHeight = fallbackHeight;
        }

        public float FallbackHeight { get; }

        public float SurfaceHeight(float2 position)
        {
            var origin = new Vector3(position.x, RayStart, position.y);
            return Physics.Raycast(origin, Vector3.down, out var hit, RayStart * 2f, _layers, QueryTriggerInteraction.Ignore)
                ? hit.point.y
                : FallbackHeight;
        }

        /// <summary>
        /// Punt onder de muis: eerst tegen colliders, anders tegen het horizontale vlak op <paramref name="fallbackPlaneHeight"/>.
        /// </summary>
        public bool TryGetCursorPoint(Ray ray, float fallbackPlaneHeight, out float3 point)
        {
            if (Physics.Raycast(ray, out var hit, 50_000f, _layers, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                return true;
            }

            var plane = new Plane(Vector3.up, new Vector3(0f, fallbackPlaneHeight, 0f));
            if (plane.Raycast(ray, out var enter))
            {
                point = ray.GetPoint(enter);
                return true;
            }

            point = default;
            return false;
        }
    }
}
