using System;
using Unity.Mathematics;

namespace Cities.Gameplay.Cameras
{
    public enum FollowMode
    {
        /// <summary>Achter en boven het doel.</summary>
        Chase,

        /// <summary>Op ooghoogte in het doel, kijkend langs de straat.</summary>
        FirstPerson,
    }

    /// <summary>
    /// Logica van de volgcamera. Levert een <see cref="CameraRig"/> (bij eerstepersoon met afstand 0), zodat de
    /// vrije camera daarna zonder sprong verder kan vanaf dezelfde stand.
    /// </summary>
    public sealed class FollowCameraController
    {
        public const float ChaseDistance = 8f;
        public const float ChaseHeight = 2.5f;
        public const float LookPitchLimit = 80f;

        private const float ChaseBasePitch = 12f;
        private const float ChaseHalfLife = 0.15f;
        private const float FirstPersonHalfLife = 0.03f;

        // Rondkijken is een afwijking van de kijkrichting van het doel.
        private float _lookYaw;
        private float _lookPitch;

        public IFollowTarget Target { get; private set; }

        public FollowMode Mode { get; private set; }

        public bool IsFollowing => Target != null;

        /// <summary>Huidige stand; alleen geldig terwijl <see cref="IsFollowing"/>.</summary>
        public CameraRig Current { get; private set; }

        public void Follow(IFollowTarget target, FollowMode mode)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Mode = mode;
            _lookYaw = 0f;
            _lookPitch = 0f;
            Current = Desired();
        }

        public void Stop() => Target = null;

        public void ToggleMode()
        {
            Mode = Mode == FollowMode.Chase ? FollowMode.FirstPerson : FollowMode.Chase;
            _lookYaw = 0f;
            _lookPitch = 0f;
        }

        /// <summary>
        /// Volgt het doel en verwerkt rondkijken (graden, positief is rechts en omlaag). Valt het doel weg, dan stopt het
        /// volgen en geeft de methode false terug.
        /// </summary>
        public bool Update(float2 lookDelta, float deltaSeconds)
        {
            if (deltaSeconds < 0f || float.IsNaN(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "Frametijd moet 0 of groter zijn.");
            }

            if (Target == null || !Target.IsValid)
            {
                Target = null;
                return false;
            }

            var basePitch = BasePitch;
            var minPitch = Mode == FollowMode.Chase ? 0f : -LookPitchLimit;
            _lookYaw = CameraRig.NormalizeYaw(_lookYaw + lookDelta.x);
            _lookPitch = math.clamp(_lookPitch + lookDelta.y, minPitch - basePitch, LookPitchLimit - basePitch);

            var halfLife = Mode == FollowMode.Chase ? ChaseHalfLife : FirstPersonHalfLife;
            Current = Current.Lerp(Desired(), 1f - math.exp2(-deltaSeconds / halfLife));
            return true;
        }

        private float BasePitch => Mode == FollowMode.Chase ? ChaseBasePitch : 0f;

        private CameraRig Desired()
        {
            var yaw = Target.Yaw + _lookYaw;
            var pitch = BasePitch + _lookPitch;
            return Mode == FollowMode.Chase
                ? new CameraRig(Target.Position + new float3(0f, ChaseHeight, 0f), yaw, pitch, ChaseDistance)
                : new CameraRig(Target.Position + new float3(0f, Target.EyeHeight, 0f), yaw, pitch, 0f);
        }
    }
}
