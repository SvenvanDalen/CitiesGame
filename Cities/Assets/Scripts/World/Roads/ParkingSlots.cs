using System;
using System.Collections.Generic;
using Cities.Simulation.Network;
using Unity.Mathematics;

namespace Cities.World.Roads
{
    /// <summary>Eén parkeerplek langs de weg: het midden op de parkeerstrook, de rijrichting van de weg en de afmeting.</summary>
    public readonly struct ParkingSlot
    {
        public ParkingSlot(float3 position, float3 heading, float length, float width)
        {
            Position = position;
            Heading = heading;
            Length = length;
            Width = width;
        }

        public float3 Position { get; }

        /// <summary>Richting van de weg op deze plek; de auto staat evenwijdig aan de weg.</summary>
        public float3 Heading { get; }

        public float Length { get; }

        public float Width { get; }
    }

    /// <summary>
    /// De vaste parkeerplekken langs een wegsegment (E04-14). Elke parkeerstrook in het dwarsprofiel (<see cref="LaneTypes.Parking"/>) krijgt
    /// plekken van <see cref="SlotLength"/> meter, evenwijdig aan de weg, met aan beide uiteinden <see cref="EndMargin"/> meter vrij voor het
    /// kruispunt en de zebra. De plekken zijn data: voertuigen reserveren ze, het mesh tekent alleen de strook.
    /// </summary>
    public static class ParkingSlots
    {
        /// <summary>Lengte van een parkeerplek (m) voor een personenauto, evenwijdig aan de weg.</summary>
        public const float SlotLength = 5.5f;

        /// <summary>Vrije ruimte (m) aan elk uiteinde van het segment.</summary>
        public const float EndMargin = 10f;

        private const int LengthSteps = 64;

        public static List<ParkingSlot> Layout(Bezier3 curve, IReadOnlyList<LaneSpec> lanes)
        {
            if (lanes is null)
            {
                throw new ArgumentNullException(nameof(lanes));
            }

            var slots = new List<ParkingSlot>();
            var cumulative = Cumulative(curve);
            var usable = cumulative[LengthSteps] - (2f * EndMargin);
            var count = (int)math.floor(usable / SlotLength);
            if (count < 1)
            {
                return slots;
            }

            // De plekken liggen in het midden van het bruikbare stuk, zodat de marge aan beide kanten even groot is.
            var start = EndMargin + ((usable - (count * SlotLength)) * 0.5f);
            foreach (var lane in lanes)
            {
                if ((lane.Types & LaneTypes.Parking) == LaneTypes.None)
                {
                    continue;
                }

                for (var i = 0; i < count; i++)
                {
                    var t = ParameterAt(cumulative, start + ((i + 0.5f) * SlotLength));
                    var heading = curve.Direction(t);
                    slots.Add(new ParkingSlot(curve.Position(t) + (Bezier3.Right(heading) * lane.Offset), heading, SlotLength, lane.Width));
                }
            }

            return slots;
        }

        /// <summary>Booglengte vanaf het begin tot elke stap van de curve (stap 0 tot <see cref="LengthSteps"/>).</summary>
        private static float[] Cumulative(Bezier3 curve)
        {
            var result = new float[LengthSteps + 1];
            var previous = curve.Position(0f);
            for (var i = 1; i <= LengthSteps; i++)
            {
                var point = curve.Position(i / (float)LengthSteps);
                result[i] = result[i - 1] + math.distance(previous, point);
                previous = point;
            }

            return result;
        }

        /// <summary>De curveparameter op <paramref name="distance"/> meter booglengte, lineair tussen de stappen.</summary>
        private static float ParameterAt(float[] cumulative, float distance)
        {
            var step = 1;
            while (step < LengthSteps && cumulative[step] < distance)
            {
                step++;
            }

            var span = cumulative[step] - cumulative[step - 1];
            var fraction = span > 0f ? (distance - cumulative[step - 1]) / span : 0f;
            return (step - 1 + math.clamp(fraction, 0f, 1f)) / LengthSteps;
        }
    }
}
