using Unity.Mathematics;

namespace Cities.Gameplay.Cameras
{
    /// <summary>Springt met de camera naar een plek zonder de kijkrichting te veranderen: voor klikken op een melding of dubbelklikken op een gebouw (E27-09, E27-11).</summary>
    public static class CameraFocus
    {
        /// <summary>De stand die <paramref name="location"/> (x, z) in beeld brengt op hoogte <paramref name="groundHeight"/>, hoogstens <paramref name="maxDistance"/> meter ver weg.</summary>
        public static CameraRig On(CameraRig current, float2 location, float groundHeight, float maxDistance) =>
            new CameraRig(new float3(location.x, groundHeight, location.y), current.Yaw, current.Pitch, math.min(current.Distance, maxDistance));
    }
}
