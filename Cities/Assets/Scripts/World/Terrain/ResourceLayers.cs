using Unity.Mathematics;

namespace Cities.World.Terrain
{
    public enum ResourceKind
    {
        Oil = 0,
        Ore = 1,
        FertileGround = 2,
        Forest = 3,
    }

    /// <summary>
    /// De natuurlijke grondstoffenlagen (E03-08): per plek de rijkdom van 0 tot 1 voor olie, erts, vruchtbare grond en bos. De lagen volgen
    /// het terrein: erts zit in heuvels en bergen, olie in het laagland, vruchtbare grond langs de rivier en bos op middelhoge grond.
    /// Zelfde seed, zelfde kaart.
    /// </summary>
    public static class ResourceLayers
    {
        /// <summary>Vanaf deze rijkdom telt een plek als een afzetting of een bos; ook de drempel van de overlay.</summary>
        public const float Deposit = 0.5f;

        public static float Richness(ResourceKind kind, float2 position, in TerrainShapeSettings settings)
        {
            var above = TerrainShape.Height(position, settings) - settings.BaseHeight;
            var blobs = Blobs(position, settings.Seed, (int)kind);
            switch (kind)
            {
                case ResourceKind.Oil:
                    // Olievelden liggen in het laagland, niet in de bergen en niet in het dal zelf.
                    return Peaks(blobs) * (1f - math.smoothstep(40f, 90f, above)) * math.smoothstep(0.1f, 0.3f, RiverFar(position, settings));
                case ResourceKind.Ore:
                    return Peaks(blobs) * math.smoothstep(25f, 140f, above);
                case ResourceKind.FertileGround:
                    // Hoe dichter bij de rivier en hoe vlakker, hoe vruchtbaarder; de ruis geeft stroken en vlekken.
                    return math.saturate(1.1f - (RiverFar(position, settings) * 1.2f) - math.smoothstep(25f, 70f, above) - (blobs * 0.15f));
                default:
                    return math.smoothstep(0.1f, 0.7f, blobs) * math.smoothstep(8f, 30f, above) * (1f - math.smoothstep(120f, 300f, above));
            }
        }

        /// <summary>Afstand tot de rivier als 0 (aan de oever) tot 1 (ver weg), op de schaal van het dal.</summary>
        private static float RiverFar(float2 position, in TerrainShapeSettings settings) =>
            math.saturate(TerrainShape.RiverDistance(position, settings) / (settings.ValleyWidth * 8f));

        /// <summary>Vlekkerige ruis van 0 tot 1; elke grondstof krijgt een eigen patroon.</summary>
        private static float Blobs(float2 position, int seed, int layer)
        {
            var p = (position / 900f) + new float2(layer * 37.1f, seed * 0.713f);
            var n = (noise.snoise(p) * 0.65f) + (noise.snoise(p * 2.3f) * 0.35f);
            return math.saturate((n * 0.5f) + 0.5f);
        }

        /// <summary>Haalt de afzetting uit de ruis: alleen de toppen blijven over.</summary>
        private static float Peaks(float blobs) => math.smoothstep(0.55f, 0.8f, blobs);
    }
}
