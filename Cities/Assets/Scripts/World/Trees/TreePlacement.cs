using Unity.Mathematics;

namespace Cities.World.Trees
{
    public enum TreeSpecies : byte
    {
        /// <summary>Loofboom (zoals beuk of linde).</summary>
        Broadleaf,

        /// <summary>Naaldboom (zoals fijnspar).</summary>
        Conifer,
    }

    /// <summary>Eén boom: plek in wereldmeters (x, z), soort, schaal en draaiing.</summary>
    public readonly struct TreeInstance
    {
        public TreeInstance(float2 position, TreeSpecies species, float scale, float yawDegrees)
        {
            Position = position;
            Species = species;
            Scale = scale;
            YawDegrees = yawDegrees;
        }

        public float2 Position { get; }

        public TreeSpecies Species { get; }

        public float Scale { get; }

        public float YawDegrees { get; }
    }

    /// <summary>
    /// Zet de bosdichtheid van een cel om in bomen. Alles volgt uit de celcoördinaat, zodat dezelfde dichtheid altijd
    /// dezelfde bomen geeft en alleen de dichtheidskaart opgeslagen hoeft te worden.
    /// </summary>
    public static class TreePlacement
    {
        /// <summary>Bij volle dichtheid twee bomen per cel van 8 m: zo'n 300 per hectare, als in een volgroeid bos.</summary>
        public const int MaxPerCell = 2;

        /// <summary>Aantal bomen in de cel; tussenliggende dichtheden worden per cel naar boven of beneden afgerond.</summary>
        public static int Count(int x, int z, byte density)
        {
            var expected = density * MaxPerCell / 255f;
            var whole = (int)expected;
            return whole + (Random01(x, z, 0) < expected - whole ? 1 : 0);
        }

        /// <summary>Boom <paramref name="index"/> van de cel; elke boom krijgt een eigen helft, zodat stammen niet overlappen.</summary>
        /// <param name="coniferShare">Kans op een naaldboom, zie <see cref="ConiferShare"/>.</param>
        public static TreeInstance Tree(int x, int z, int index, float cellSize, float coniferShare)
        {
            var jitter = new float2(Random01(x, z, 1 + (index * 4)), Random01(x, z, 2 + (index * 4)));
            var local = new float2((index + 0.15f + (jitter.x * 0.7f)) / MaxPerCell, 0.1f + (jitter.y * 0.8f));
            var species = Random01(x, z, 3 + (index * 4)) < coniferShare ? TreeSpecies.Conifer : TreeSpecies.Broadleaf;
            var scale = 0.75f + (0.5f * Random01(x, z, 4 + (index * 4)));
            var yaw = 360f * Random01(x, z, 100 + index);
            return new TreeInstance((new float2(x, z) + local) * cellSize, species, scale, yaw);
        }

        /// <summary>Op de lage grond vooral loofbos; hoger op de hellingen steeds meer naaldbomen.</summary>
        public static float ConiferShare(float height) => 0.15f + (0.8f * math.smoothstep(70f, 200f, height));

        /// <summary>Murmur3-finalizer over de celcoördinaat: buurcellen geven onafhankelijke getallen, zonder rijen of strepen.</summary>
        private static float Random01(int x, int z, int salt)
        {
            unchecked
            {
                var h = ((uint)x * 0x8DA6B343u) ^ ((uint)z * 0xD8163841u) ^ ((uint)salt * 0xCB1AB31Fu);
                h ^= h >> 16;
                h *= 0x85EBCA6Bu;
                h ^= h >> 13;
                h *= 0xC2B2AE35u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }
    }
}
