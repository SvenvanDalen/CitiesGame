using System;
using System.Collections.Generic;
using Cities.Simulation.Network;
using Cities.World.Terrain;
using Unity.Mathematics;

namespace Cities.World.Roads
{
    /// <summary>Een pijler van een viaduct: staat op de grond (<see cref="Base"/>) en reikt <see cref="Height"/> meter omhoog tot de onderkant van het wegdek.</summary>
    public readonly struct ViaductPier
    {
        public ViaductPier(float3 position, float height)
        {
            Base = position;
            Height = height;
        }

        public float3 Base { get; }

        public float Height { get; }
    }

    /// <summary>
    /// Wegen boven het maaiveld (E04-10). Een weg is verhoogd zodra het wegdek ten minste <see cref="MinClearance"/> boven de grond ligt; daar
    /// komen pijlers, en waar het wegdek de grond weer bereikt (de oprit) zijn ze niet nodig. <see cref="MaxGrade"/> begrenst hoe steil de
    /// oprit mag lopen en dus hoe lang hij is.
    /// </summary>
    public static class Viaduct
    {
        /// <summary>Vanaf deze hoogte (m) tussen wegdek en grond is de weg verhoogd: lager blijft hij een gewone weg op een lage dam.</summary>
        public const float MinClearance = 3f;

        /// <summary>Grootste hart-op-hartafstand (m) van de pijlers langs de weg.</summary>
        public const float PierSpacing = 30f;

        /// <summary>Steilste helling van het wegdek als deel (0,06 = 6%).</summary>
        public const float MaxGrade = 0.06f;

        private const int LengthSteps = 24;

        public static bool IsElevated(float clearance) => clearance >= MinClearance;

        /// <summary>Lengte (m) van de oprit die nodig is om <paramref name="rise"/> meter te stijgen zonder de maximale helling te overschrijden.</summary>
        public static float RampLength(float rise) => math.max(0f, rise) / MaxGrade;

        /// <summary>De steilste helling (hoogteverschil gedeeld door horizontale afstand) van het wegdek, als deel.</summary>
        public static float SteepestGrade(Bezier3 deck)
        {
            var steepest = 0f;
            for (var i = 0; i <= LengthSteps; i++)
            {
                var d = deck.Derivative(i / (float)LengthSteps);
                var run = math.length(d.xz);
                steepest = math.max(steepest, run < 1e-4f ? 0f : math.abs(d.y) / run);
            }

            return steepest;
        }

        /// <summary>Of het wegdek binnen de maximale helling blijft; zo niet is de oprit te kort voor de hoogte.</summary>
        public static bool GradeAllowed(Bezier3 deck) => SteepestGrade(deck) <= MaxGrade + 1e-4f;

        /// <summary>
        /// De pijlers onder een wegdek, hooguit <see cref="PierSpacing"/> meter uit elkaar, op de punten waar het wegdek verhoogd is. De afstand wordt in
        /// stukken van gelijke parameterstap gemeten, niet in exacte booglengte; dat scheelt hooguit enkele procenten bij sterk gebogen wegen.
        /// </summary>
        public static IReadOnlyList<ViaductPier> Piers(Bezier3 deck, ITerrainHeight ground)
        {
            if (ground is null)
            {
                throw new ArgumentNullException(nameof(ground));
            }

            var piers = new List<ViaductPier>();
            var count = Math.Max(1, (int)math.ceil(deck.Length(LengthSteps) / PierSpacing));
            for (var i = 0; i <= count; i++)
            {
                var at = deck.Position(i / (float)count);
                var groundHeight = ground.SurfaceHeight(at.xz);
                var clearance = at.y - groundHeight;
                if (IsElevated(clearance))
                {
                    piers.Add(new ViaductPier(new float3(at.x, groundHeight, at.z), clearance));
                }
            }

            return piers;
        }
    }
}
