using System;
using Unity.Mathematics;

namespace Cities.Simulation.Freight
{
    /// <summary>
    /// Haven en visserij (E25-07): vangst uit boten en visbestand, havenoverslag uit kades en kranen, en afvalverwerking. Het bestand
    /// herstelt langzaam; overbevissing kost vangst op de lange termijn.
    /// </summary>
    public sealed class PortFishing
    {
        public const float StockRecoveryPerDay = 0.02f;

        public float Stock { get; private set; } = 1f;

        /// <summary>Vangst in ton voor een dag met <paramref name="boats"/> boten; het bestand slinkt mee.</summary>
        public int FishDay(int boats)
        {
            if (boats < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(boats), boats, "Aantal boten is niet negatief.");
            }

            var catchTons = (int)(boats * 5f * Stock);
            Stock = math.clamp(Stock - (boats * 0.01f) + StockRecoveryPerDay, 0.05f, 1f);
            return catchTons;
        }

        /// <summary>Overslag in containers per dag: 120 per kade en 160 per kraan, begrensd door het laagste van de twee.</summary>
        public static int Throughput(int quays, int cranes) => Math.Min(quays * 120, cranes * 160);

        /// <summary>Afval dat de haven per dag verwerkt, in ton: 30 per installatie.</summary>
        public static int WasteProcessed(int plants) => plants * 30;
    }
}
