using System;
using System.Collections.Generic;

namespace Cities.Simulation.Vehicles
{
    /// <summary>
    /// Lanewissel (E08-03), een vereenvoudigd MOBIL: een voertuig wisselt alleen als het veilig kan (de achterligger in de doellane hoeft niet harder
    /// te remmen dan comfortabel en de voorligger is ver genoeg weg) en het iets oplevert (meer versnelling dan op de eigen lane). De doellane is
    /// gesorteerd van voor naar achter, net als bij <see cref="LaneTraffic"/>.
    /// </summary>
    public static class LaneChange
    {
        // shortcut: geen beleefdheidsfactor voor de achterligger en geen voorkeur voor rechts; upgrade als verkeer er onnatuurlijk uitziet.
        /// <summary>Extra versnelling (m/s²) die een wissel minimaal moet opleveren, zodat voertuigen niet heen en weer zwabberen.</summary>
        public const float Threshold = 0.2f;

        /// <summary>Of <paramref name="car"/> met huidige versnelling <paramref name="currentAcceleration"/> naar <paramref name="target"/> mag wisselen.</summary>
        public static bool CanChange(in VehicleAgent car, float currentAcceleration, IReadOnlyList<VehicleAgent> target)
        {
            if (target is null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            // Eerste voertuig in de doellane dat niet meer vóór ons zit: de achterligger. Het ene daarvoor is de voorligger.
            var follower = 0;
            while (follower < target.Count && target[follower].Position >= car.Position)
            {
                follower++;
            }

            var acceleration = car.Driver.Acceleration(car.Speed);
            if (follower > 0)
            {
                var leader = target[follower - 1];
                var gap = leader.Position - leader.Length - car.Position;
                if (gap <= 0f)
                {
                    return false;
                }

                acceleration = car.Driver.Acceleration(car.Speed, gap, leader.Speed);
            }

            if (follower < target.Count)
            {
                var back = target[follower];
                var backGap = car.Position - car.Length - back.Position;
                if (backGap <= 0f || back.Driver.Acceleration(back.Speed, backGap, car.Speed) < -back.Driver.ComfortBraking)
                {
                    return false;
                }
            }

            return acceleration > currentAcceleration + Threshold;
        }
    }
}
