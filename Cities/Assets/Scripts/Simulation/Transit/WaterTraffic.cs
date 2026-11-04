using System;
using Unity.Mathematics;

namespace Cities.Simulation.Transit
{
    /// <summary>Soort schip en daarmee soort haven (E09-08).</summary>
    public enum VesselKind
    {
        /// <summary>Veerboot voor passagiers; de lijn is een gewone <see cref="TransitLine"/>.</summary>
        Ferry = 0,

        /// <summary>Vrachtschip voor goederen tussen havens.</summary>
        CargoShip = 1,
    }

    /// <summary>
    /// De regels van het waterverkeer (E09-08): diepgang, snelheid, laden en lossen, en de afmetingen van een kanaal.
    /// Een schip vaart alleen waar het water diep genoeg is (diepgang plus marge onder de kiel); in een kanaal geldt een lage
    /// snelheid wegens de golfslag en moet het kanaal breed genoeg zijn om elkaar te passeren.
    /// shortcut: de schepen rijden nog niet als voertuig in <see cref="TransitFleet"/>; er is alleen de reistijd- en capaciteitsberekening.
    /// </summary>
    public static class WaterTraffic
    {
        /// <summary>Marge onder de kiel in meter.</summary>
        public const float KeelClearance = 0.5f;

        /// <summary>Snelheid in een kanaal in m/s (ongeveer 11 km/u).</summary>
        public const float CanalSpeed = 3f;

        public const float MinCanalWidth = 20f;
        public const float MaxCanalWidth = 200f;

        /// <summary>Diepste kanaal in meter.</summary>
        public const float MaxCanalDepth = 20f;

        /// <summary>Minimale afstand tussen twee havens in meter.</summary>
        public const float MinPortSpacing = 150f;

        /// <summary>Afstand in meter tussen de dieptemetingen langs een vaarroute.</summary>
        public const float SampleStep = 25f;

        /// <summary>Hoeveel langer een vaarroute is dan de rechte lijn (bochten om ondiepten) als factor.</summary>
        public const float RouteDetour = 1.1f;

        /// <summary>Overslag van één kraan in ton per seconde (vrachtschip); een kade heeft twee kranen.</summary>
        public const float CraneRate = 1.5f;

        public static float Draught(VesselKind kind) => kind switch
        {
            VesselKind.Ferry => 2.5f,
            VesselKind.CargoShip => 8f,
            _ => throw Unknown(kind),
        };

        /// <summary>Breedte van het schip in meter.</summary>
        public static float Beam(VesselKind kind) => kind switch
        {
            VesselKind.Ferry => 10f,
            VesselKind.CargoShip => 30f,
            _ => throw Unknown(kind),
        };

        /// <summary>Kruissnelheid op open water in m/s.</summary>
        public static float Speed(VesselKind kind) => kind switch
        {
            VesselKind.Ferry => 8f,
            VesselKind.CargoShip => 7f,
            _ => throw Unknown(kind),
        };

        /// <summary>Passagiers (veerboot) of ton lading (vrachtschip) per schip.</summary>
        public static int Capacity(VesselKind kind) => kind switch
        {
            VesselKind.Ferry => 250,
            VesselKind.CargoShip => 5000,
            _ => throw Unknown(kind),
        };

        /// <summary>Seconden dat een schip minstens aan de kade ligt: aanmeren, instappen of laden en lossen.</summary>
        public static float DwellTime(VesselKind kind) => kind switch
        {
            VesselKind.Ferry => 120f,
            VesselKind.CargoShip => 1800f,
            _ => throw Unknown(kind),
        };

        /// <summary>Diepte in meter die een schip minimaal onder zich nodig heeft.</summary>
        public static float RequiredDepth(VesselKind kind) => Draught(kind) + KeelClearance;

        public static bool IsDepthOk(VesselKind kind, float depth) => depth >= RequiredDepth(kind);

        public static bool IsSpacingOk(float2 a, float2 b) => math.distance(a, b) >= MinPortSpacing;

        public static bool IsCanalWidthOk(float width) => width >= MinCanalWidth && width <= MaxCanalWidth;

        public static bool IsCanalDepthOk(float depth) => depth > 0f && depth <= MaxCanalDepth;

        /// <summary>Of een schip door een kanaal kan: diep genoeg en minstens drie keer zo breed als het schip (passeren).</summary>
        public static bool CanalAdmits(VesselKind kind, float width, float depth) =>
            IsDepthOk(kind, depth) && width >= 3f * Beam(kind);

        /// <summary>Vaartijd in seconden over <paramref name="distance"/> meter open water, inclusief omweg.</summary>
        public static float SailTime(VesselKind kind, float distance)
        {
            CheckDistance(distance);
            return distance * RouteDetour / Speed(kind);
        }

        /// <summary>Vaartijd in seconden over <paramref name="distance"/> meter kanaal.</summary>
        public static float CanalTime(float distance)
        {
            CheckDistance(distance);
            return distance / CanalSpeed;
        }

        /// <summary>
        /// Seconden aan de kade om <paramref name="tonnes"/> ton te lossen en evenveel te laden met twee kranen
        /// (ten minste de vaste <see cref="DwellTime"/>).
        /// </summary>
        public static float HandlingTime(float tonnes)
        {
            if (!(tonnes >= 0f) || tonnes > Capacity(VesselKind.CargoShip))
            {
                throw new ArgumentOutOfRangeException(nameof(tonnes), tonnes, "De lading past niet op een vrachtschip.");
            }

            return math.max(DwellTime(VesselKind.CargoShip), tonnes / CraneRate);
        }

        /// <summary>Vervoerscapaciteit per dag (passagiers of ton, één richting) van een lijn met <paramref name="vessels"/> schepen.</summary>
        public static float DailyCapacity(VesselKind kind, int vessels, float roundTripSeconds)
        {
            if (vessels < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(vessels), vessels, "Een lijn heeft minstens één schip.");
            }

            if (!(roundTripSeconds > 0f) || float.IsInfinity(roundTripSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(roundTripSeconds), roundTripSeconds, "De rondrit moet langer dan nul duren.");
            }

            return vessels * Capacity(kind) * (86400f / roundTripSeconds);
        }

        private static ArgumentOutOfRangeException Unknown(VesselKind kind) =>
            new ArgumentOutOfRangeException(nameof(kind), kind, "Onbekend soort schip.");

        private static void CheckDistance(float distance)
        {
            if (!(distance >= 0f) || float.IsInfinity(distance))
            {
                throw new ArgumentOutOfRangeException(nameof(distance), distance, "De afstand kan niet negatief zijn.");
            }
        }
    }
}
