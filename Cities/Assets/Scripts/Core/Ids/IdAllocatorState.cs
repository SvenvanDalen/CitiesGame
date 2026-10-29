using Cities.Core.Persistence;

namespace Cities.Core.Ids
{
    /// <summary>Gelezen en gecontroleerde staat van een <see cref="IdAllocator{TKind}"/>, klaar om toe te passen.</summary>
    public sealed class IdAllocatorState
    {
        internal IdAllocatorState(uint[] generations, int[] free, int aliveCount, int retiredCount)
        {
            Generations = generations;
            Free = free;
            AliveCount = aliveCount;
            RetiredCount = retiredCount;
        }

        public uint[] Generations { get; }

        public int[] Free { get; }

        public int AliveCount { get; }

        public int RetiredCount { get; }

        /// <summary>Leest wat <c>IdAllocator.WriteTo</c> schreef en controleert het; verandert nog niets.</summary>
        public static IdAllocatorState Read(SaveReader reader, int maxSlots)
        {
            var generations = reader.ReadArray<uint>(maxSlots);
            var free = reader.ReadArray<int>(generations.Length);
            var isFree = new bool[generations.Length];
            foreach (var index in free)
            {
                if (index < 0 || index >= generations.Length || (generations[index] & 1u) == 1u || isFree[index])
                {
                    throw reader.Corrupt($"vrije plek {index} is ongeldig of bezet");
                }

                isFree[index] = true;
            }

            var alive = 0;
            var retired = 0;
            for (var i = 0; i < generations.Length; i++)
            {
                var generation = generations[i];
                if (generation == 0u)
                {
                    throw reader.Corrupt($"plek {i} heeft generatie 0");
                }

                if ((generation & 1u) == 1u)
                {
                    alive++;
                }
                else if (!isFree[i])
                {
                    ExpectRetired(reader, i, generation);
                    retired++;
                }
            }

            return new IdAllocatorState(generations, free, alive, retired);
        }

        /// <summary>Een vrije plek buiten de wachtrij mag alleen als de generatie is uitgeput.</summary>
        private static void ExpectRetired(SaveReader reader, int index, uint generation)
        {
            if (generation < uint.MaxValue - 1)
            {
                throw reader.Corrupt($"plek {index} is vrij maar staat niet in de wachtrij");
            }
        }
    }
}
