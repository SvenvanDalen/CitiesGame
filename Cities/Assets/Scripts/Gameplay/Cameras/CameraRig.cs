using System;
using Unity.Mathematics;

namespace Cities.Gameplay.Cameras
{
    /// <summary>
    /// Stand van een orbitcamera: een draaipunt op de grond, met de camera op <see cref="Distance"/> meter daarvandaan,
    /// gedraaid om de verticale as (<see cref="Yaw"/>) en gekanteld (<see cref="Pitch"/>, 90° is recht naar beneden).
    /// </summary>
    public readonly struct CameraRig : IEquatable<CameraRig>
    {
        public CameraRig(float3 pivot, float yaw, float pitch, float distance)
        {
            Pivot = pivot;
            Yaw = yaw;
            Pitch = pitch;
            Distance = distance;
        }

        public float3 Pivot { get; }

        /// <summary>Richting in graden, 0 = kijkt naar +Z (noord), met de klok mee van boven gezien.</summary>
        public float Yaw { get; }

        /// <summary>Kanteling in graden boven de horizon.</summary>
        public float Pitch { get; }

        /// <summary>Afstand van camera tot draaipunt in meters.</summary>
        public float Distance { get; }

        public quaternion Rotation => quaternion.Euler(math.radians(Pitch), math.radians(Yaw), 0f);

        /// <summary>Kijkrichting van de camera (genormaliseerd).</summary>
        public float3 Forward => math.mul(Rotation, new float3(0f, 0f, 1f));

        public float3 Position => Pivot - (Forward * Distance);

        /// <summary>Hoogte van de camera boven het draaipunt.</summary>
        public float Height => Distance * math.sin(math.radians(Pitch));

        /// <summary>Vooruit langs de grond (zonder kanteling), voor pannen met de toetsen.</summary>
        public float3 GroundForward
        {
            get
            {
                var yaw = math.radians(Yaw);
                return new float3(math.sin(yaw), 0f, math.cos(yaw));
            }
        }

        /// <summary>Rechts langs de grond.</summary>
        public float3 GroundRight
        {
            get
            {
                var yaw = math.radians(Yaw);
                return new float3(math.cos(yaw), 0f, -math.sin(yaw));
            }
        }

        public CameraRig WithPivot(float3 pivot) => new CameraRig(pivot, Yaw, Pitch, Distance);

        /// <summary>
        /// Interpoleert naar <paramref name="target"/>; de draaiing neemt de kortste weg om, ook over 0/360° heen.
        /// </summary>
        public CameraRig Lerp(CameraRig target, float t)
        {
            var yawDelta = DeltaAngle(Yaw, target.Yaw);
            return new CameraRig(
                math.lerp(Pivot, target.Pivot, t),
                NormalizeYaw(Yaw + (yawDelta * t)),
                math.lerp(Pitch, target.Pitch, t),
                math.lerp(Distance, target.Distance, t));
        }

        /// <summary>Kortste hoek van <paramref name="from"/> naar <paramref name="to"/>, tussen -180 en 180 graden.</summary>
        public static float DeltaAngle(float from, float to)
        {
            var delta = (to - from) % 360f;
            if (delta > 180f)
            {
                delta -= 360f;
            }
            else if (delta < -180f)
            {
                delta += 360f;
            }

            return delta;
        }

        public static float NormalizeYaw(float yaw)
        {
            var result = yaw % 360f;
            return result < 0f ? result + 360f : result;
        }

        public bool Equals(CameraRig other) =>
            math.all(Pivot == other.Pivot) && BitConverter.SingleToInt32Bits(Yaw) == BitConverter.SingleToInt32Bits(other.Yaw) &&
            BitConverter.SingleToInt32Bits(Pitch) == BitConverter.SingleToInt32Bits(other.Pitch) &&
            BitConverter.SingleToInt32Bits(Distance) == BitConverter.SingleToInt32Bits(other.Distance);

        public override bool Equals(object obj) => obj is CameraRig other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Pivot, Yaw, Pitch, Distance);

        public override string ToString() => $"pivot ({Pivot.x:0.#}, {Pivot.y:0.#}, {Pivot.z:0.#}), yaw {Yaw:0.#}°, pitch {Pitch:0.#}°, {Distance:0.#} m";

        public static bool operator ==(CameraRig left, CameraRig right) => left.Equals(right);

        public static bool operator !=(CameraRig left, CameraRig right) => !left.Equals(right);
    }
}
