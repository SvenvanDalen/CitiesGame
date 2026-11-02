using System;
using System.Collections.Generic;

namespace Cities.Simulation.Vehicles
{
    /// <summary>Een voertuig op een lane (E08-02): plaats van de voorbumper in meters vanaf het begin, snelheid in m/s en lengte.</summary>
    public struct VehicleAgent
    {
        public float Position { get; set; }
        public float Speed { get; set; }
        public float Length { get; set; }
        public Idm Driver { get; set; }
    }

    /// <summary>
    /// Laat de voertuigen van één lane een tijdstap rijden. De lijst is gesorteerd van voor naar achter (hoogste positie eerst); elk voertuig
    /// volgt zijn voorganger met <see cref="Idm"/>. Het voorste voertuig rijdt vrij, of remt voor het einde van de lane als dat dicht is
    /// (rood licht, voorrang). Semi-impliciete Euler: snelheid eerst, dan plaats; een voertuig rijdt nooit achteruit.
    /// </summary>
    public static class LaneTraffic
    {
        // shortcut: één lane, geen inhalen of lanewissel en geen overgang naar de volgende lane; upgrade met E08-03 (kruispunten).
        public static void Step(IList<VehicleAgent> vehicles, float laneLength, bool endBlocked, float dt)
        {
            if (vehicles is null)
            {
                throw new ArgumentNullException(nameof(vehicles));
            }

            if (!(dt > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(dt), dt, "De tijdstap moet positief zijn.");
            }

            for (var i = 0; i < vehicles.Count; i++)
            {
                var car = vehicles[i];
                float a;
                if (i > 0)
                {
                    var leader = vehicles[i - 1];
                    a = car.Driver.Acceleration(car.Speed, leader.Position - leader.Length - car.Position, leader.Speed);
                }
                else if (endBlocked)
                {
                    a = car.Driver.Acceleration(car.Speed, laneLength - car.Position, 0f);
                }
                else
                {
                    a = car.Driver.Acceleration(car.Speed);
                }

                car.Speed = Math.Max(0f, car.Speed + (a * dt));
                car.Position += car.Speed * dt;
                vehicles[i] = car;
            }
        }
    }
}
