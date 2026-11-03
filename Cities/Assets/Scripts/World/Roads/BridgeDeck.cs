using System;
using Cities.Simulation.Network;
using Cities.World.Terrain;
using Unity.Mathematics;

namespace Cities.World.Roads
{
    /// <summary>
    /// Bruggen en tunnels (E04-06a): het hoogteprofiel van een segment ten opzichte van het terrein. Een brug ligt boven het terrein, een tunnel
    /// eronder; beide mogen hoogstens <see cref="MaxGrade"/> hellen. <see cref="Suggest"/> kiest zelf wat past, <see cref="Check"/> beoordeelt de
    /// keuze van de speler en <see cref="Cost"/> geeft de kosten voor de preview. Het brugdek zelf staat in <see cref="RoadMeshBuilder.BuildDeck"/>;
    /// pijlers en tunnelmond volgen met E04-06b.
    /// </summary>
    public static class BridgeDeck
    {
        /// <summary>Steilste helling van een brug of tunnel als deel (0,15 = 15%).</summary>
        public const float MaxGrade = 0.15f;

        /// <summary>Marge (m) waarbinnen het wegdek bij de landhoofden en tunnelmonden nog "op de grond" telt.</summary>
        public const float Tolerance = 0.5f;

        /// <summary>Kostenfactor ten opzichte van een gewone weg; schattingen tot de economie (E11) ze kalibreert.</summary>
        public const float BridgeCostFactor = 4f;

        public const float TunnelCostFactor = 6f;

        private const int Steps = 24;

        /// <summary>De laagste en hoogste afstand (m) van het wegdek tot het terrein langs de curve; negatief is onder het terrein.</summary>
        public static (float Lowest, float Highest) Clearance(Bezier3 deck, ITerrainHeight ground)
        {
            if (ground is null)
            {
                throw new ArgumentNullException(nameof(ground));
            }

            var lowest = float.MaxValue;
            var highest = float.MinValue;
            for (var i = 0; i <= Steps; i++)
            {
                var at = deck.Position(i / (float)Steps);
                var clearance = at.y - ground.SurfaceHeight(at.xz);
                lowest = math.min(lowest, clearance);
                highest = math.max(highest, clearance);
            }

            return (lowest, highest);
        }

        /// <summary>Brug als het wegdek ergens ten minste <see cref="Viaduct.MinClearance"/> boven het terrein ligt, tunnel als het zo ver eronder zit; het grootste verschil wint.</summary>
        public static RoadElevation Suggest(Bezier3 deck, ITerrainHeight ground)
        {
            var (lowest, highest) = Clearance(deck, ground);
            var above = highest >= Viaduct.MinClearance;
            var below = -lowest >= Viaduct.MinClearance;
            if (above && (!below || highest >= -lowest))
            {
                return RoadElevation.Bridge;
            }

            return below ? RoadElevation.Tunnel : RoadElevation.Ground;
        }

        /// <summary>Waarom dit segment geen brug of tunnel kan zijn, of <c>null</c> als het kan. Een gewone weg heeft hier niets te controleren.</summary>
        public static string Check(RoadElevation elevation, Bezier3 deck, ITerrainHeight ground)
        {
            if (elevation == RoadElevation.Ground)
            {
                return null;
            }

            var bridge = elevation == RoadElevation.Bridge;
            if (Viaduct.SteepestGrade(deck) > MaxGrade + 1e-4f)
            {
                return $"Te steil voor een {(bridge ? "brug" : "tunnel")}: maximaal {MaxGrade * 100f:0}% helling";
            }

            var (lowest, highest) = Clearance(deck, ground);
            if (bridge)
            {
                return lowest < -Tolerance ? "Het brugdek ligt onder het terrein" : null;
            }

            return highest > Tolerance ? "De tunnel komt boven het terrein uit" : null;
        }

        /// <summary>Kosten in hele munteenheden van <paramref name="length"/> meter weg met kosten <paramref name="costPerMeter"/> per meter.</summary>
        public static long Cost(float length, long costPerMeter, RoadElevation elevation)
        {
            var factor = 1f;
            if (elevation == RoadElevation.Bridge)
            {
                factor = BridgeCostFactor;
            }
            else if (elevation == RoadElevation.Tunnel)
            {
                factor = TunnelCostFactor;
            }

            return (long)math.round(length * costPerMeter * factor);
        }
    }
}
