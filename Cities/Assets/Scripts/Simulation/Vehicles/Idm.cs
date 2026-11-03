using System;

namespace Cities.Simulation.Vehicles
{
    /// <summary>
    /// Intelligent Driver Model (E08-02): hoe een voertuig versnelt en remt op basis van zijn snelheid, de afstand tot de voorganger
    /// en het snelheidsverschil. Vrije weg geeft een vloeiende versnelling naar de wensnelheid; een kleine opening geeft remmen,
    /// begrensd op <see cref="MaxBraking"/> zodat het nooit onmogelijk hard remt.
    /// </summary>
    public readonly struct Idm
    {
        /// <summary>Remvertraging waarboven het model niet remt (m/s²); een noodstop in de praktijk.</summary>
        public const float MaxBraking = 9f;

        public Idm(float desiredSpeed, float maxAcceleration, float comfortBraking, float timeHeadway = 1.5f, float minGap = 2f)
        {
            if (!(desiredSpeed > 0f) || !(maxAcceleration > 0f) || !(comfortBraking > 0f) || !(timeHeadway >= 0f) || !(minGap >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(desiredSpeed), "Snelheid, versnelling en remming moeten positief zijn.");
            }

            DesiredSpeed = desiredSpeed;
            MaxAcceleration = maxAcceleration;
            ComfortBraking = comfortBraking;
            TimeHeadway = timeHeadway;
            MinGap = minGap;
        }

        /// <summary>Wensnelheid in m/s.</summary>
        public float DesiredSpeed { get; }

        public float MaxAcceleration { get; }

        public float ComfortBraking { get; }

        /// <summary>Gewenste tijdsafstand tot de voorganger in seconden.</summary>
        public float TimeHeadway { get; }

        /// <summary>Minimale opening in meters bij stilstand.</summary>
        public float MinGap { get; }

        /// <summary>Het model voor een voertuigtype; de wensnelheid is de laagste van topsnelheid en <paramref name="speedLimit"/> (m/s).</summary>
        public static Idm For(in VehicleSpec spec, float speedLimit) =>
            new Idm(Math.Min(spec.MaxSpeedMetersPerSecond, speedLimit), spec.AccelerationMs2, spec.BrakingMs2, spec.TimeHeadwaySeconds, spec.MinGapMeters);

        /// <summary>Gewenste opening bij snelheid <paramref name="speed"/> en naderingssnelheid <paramref name="closing"/>.</summary>
        public float DesiredGap(float speed, float closing) =>
            MinGap + Math.Max(0f, (speed * TimeHeadway) + (speed * closing / (2f * MathF.Sqrt(MaxAcceleration * ComfortBraking))));

        /// <summary>Vrije weg: geen voorganger.</summary>
        public float Acceleration(float speed) =>
            Math.Max(-MaxBraking, MaxAcceleration * (1f - MathF.Pow(speed / DesiredSpeed, 4f)));

        /// <summary>Versnelling in m/s² met een voorganger op <paramref name="gap"/> meter (bumper tot bumper) met snelheid <paramref name="leaderSpeed"/>.</summary>
        public float Acceleration(float speed, float gap, float leaderSpeed)
        {
            var desired = DesiredGap(speed, speed - leaderSpeed);
            var a = MaxAcceleration * (1f - MathF.Pow(speed / DesiredSpeed, 4f) - MathF.Pow(desired / Math.Max(gap, 0.1f), 2f));
            return Math.Max(-MaxBraking, a);
        }
    }
}
