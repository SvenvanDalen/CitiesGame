using System;
using Unity.Mathematics;

namespace Cities.Simulation.Services
{
    /// <summary>
    /// Helikopters en speciale voertuigen (E10-11): een politie-, brand- of ambulancehelikopter voor lange afstanden. Hij vliegt hemelsbreed
    /// en sneller dan een wagen, maar is pas zinvol boven een minimale afstand, en kan bij storm niet op.
    /// </summary>
    public sealed class ServiceHelicopter
    {
        public const float SpeedMetersPerSecond = 60f;

        /// <summary>Onder deze afstand is de wagen sneller (opstijgen kost tijd).</summary>
        public const float MinUsefulDistance = 3_000f;

        public const float TakeOffSeconds = 45f;

        public ServiceHelicopter(ServiceKind kind, float2 home)
        {
            if (kind == ServiceKind.Death)
            {
                throw new ArgumentException("Voor overledenen is geen helikopter.", nameof(kind));
            }

            Kind = kind;
            Base = home;
        }

        public ServiceKind Kind { get; }

        public float2 Base { get; }

        public bool Grounded { get; set; }

        /// <summary>Aankomsttijd in seconden bij een melding op <paramref name="target"/>, of NaN als hij niet kan of niet loont.</summary>
        public float ArrivalSeconds(float2 target)
        {
            var distance = math.distance(Base, target);
            if (Grounded || distance < MinUsefulDistance)
            {
                return float.NaN;
            }

            return TakeOffSeconds + (distance / SpeedMetersPerSecond);
        }
    }
}
