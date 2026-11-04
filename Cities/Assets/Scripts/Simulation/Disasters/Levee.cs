using System;
using Unity.Mathematics;

namespace Cities.Simulation.Disasters
{
    /// <summary>
    /// Een dijk (E15-12): een lijnstuk met een kruinhoogte (meter boven zeeniveau), een toestand van 0 (vervallen) tot 1 (nieuw) en een
    /// bereik dat aangeeft hoe ver het land erachter beschermd is. Een dijk houdt het water tegen zolang het peil onder zijn effectieve
    /// kruin blijft; een slecht onderhouden dijk bezwijkt eerder (<see cref="FailureMargin"/> meter bij toestand 0).
    /// </summary>
    public sealed class Levee
    {
        /// <summary>Meter waarmee de effectieve kruin daalt bij een volledig vervallen dijk.</summary>
        public const float FailureMargin = 1f;

        public Levee(int id, float2 start, float2 end, float crestLevel, float reach, float condition = 1f)
        {
            if (!(reach > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(reach), reach, "Het bereik van een dijk is groter dan nul.");
            }

            if (math.distance(start, end) <= 0f)
            {
                throw new ArgumentException("Een dijk heeft lengte nodig.", nameof(end));
            }

            Id = id;
            Start = start;
            End = end;
            CrestLevel = crestLevel;
            Reach = reach;
            Condition = ValidCondition(condition);
        }

        public int Id { get; }

        public float2 Start { get; }

        public float2 End { get; }

        /// <summary>Kruinhoogte in meter boven zeeniveau.</summary>
        public float CrestLevel { get; }

        /// <summary>Hoe ver (meter) het land achter de dijk beschermd is.</summary>
        public float Reach { get; }

        /// <summary>Toestand van 0 (vervallen) tot 1 (nieuw).</summary>
        public float Condition { get; private set; }

        public float Length => math.distance(Start, End);

        /// <summary>Het peil waarboven de dijk het begeeft: de kruin min het verlies door slijtage.</summary>
        public float EffectiveCrest => CrestLevel - (FailureMargin * (1f - Condition));

        /// <summary>Afstand van een punt tot het lijnstuk van de dijk.</summary>
        public float DistanceTo(float2 point)
        {
            var line = End - Start;
            var t = math.saturate(math.dot(point - Start, line) / math.dot(line, line));
            return math.distance(point, Start + (line * t));
        }

        public bool Covers(float2 point) => DistanceTo(point) <= Reach;

        /// <summary>Houdt de dijk dit waterpeil tegen?</summary>
        public bool Holds(float waterLevel) => waterLevel <= EffectiveCrest;

        /// <summary>Onderhoud of slijtage: zet de toestand (0 tot 1).</summary>
        public void SetCondition(float condition) => Condition = ValidCondition(condition);

        private static float ValidCondition(float condition) =>
            condition >= 0f && condition <= 1f
                ? condition
                : throw new ArgumentOutOfRangeException(nameof(condition), condition, "De toestand ligt tussen 0 en 1.");
    }
}
