using System;
using Unity.Mathematics;

namespace Cities.Simulation.Utilities
{
    /// <summary>
    /// Het riool (E06-07). De afvoerleidingen zijn een <see cref="PipeNetwork"/> met de uitlaat als bron; hier staat wat er bij de uitlaat gebeurt:
    /// wat de zuivering aankan wordt gereinigd, de rest gaat ongezuiverd de rivier in en vervuilt stroomafwaarts.
    /// </summary>
    public static class Sewage
    {
        /// <summary>Vervuiling die per stap in de rivier verdwijnt door verdunning en afbraak, als deel van 0 tot 1.</summary>
        public const float RiverRecovery = 0.1f;

        /// <summary>
        /// Het ongezuiverde deel van <paramref name="flow"/> dat de uitlaat in gaat: wat boven de <paramref name="treatmentCapacity"/> uitkomt,
        /// plus het deel dat de zuivering zelf niet haalt (<paramref name="efficiency"/> van 0 tot 1).
        /// </summary>
        public static float Untreated(float flow, float treatmentCapacity, float efficiency)
        {
            if (!(flow >= 0f) || !(treatmentCapacity >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(flow), "Afvoer en zuiveringscapaciteit moeten nul of meer zijn.");
            }

            var treated = math.min(flow, treatmentCapacity);
            return (flow - treated) + (treated * (1f - math.clamp(efficiency, 0f, 1f)));
        }

        /// <summary>
        /// Vervuiling per punt van een rivier, stroomafwaarts geteld: elk punt krijgt het herstelde deel van het vorige plus de eigen
        /// lozing gedeeld door de waterstroom (<paramref name="discharge"/> in vuillast, <paramref name="waterFlow"/> in volume per stap), begrensd op 1.
        /// </summary>
        public static float[] Downstream(float[] discharge, float waterFlow)
        {
            if (discharge == null)
            {
                throw new ArgumentNullException(nameof(discharge));
            }

            if (!(waterFlow > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(waterFlow), waterFlow, "De rivier moet water voeren.");
            }

            var pollution = new float[discharge.Length];
            var previous = 0f;
            for (var i = 0; i < discharge.Length; i++)
            {
                previous = math.min(1f, (previous * (1f - RiverRecovery)) + (math.max(0f, discharge[i]) / waterFlow));
                pollution[i] = previous;
            }

            return pollution;
        }
    }
}
