using System;

namespace Cities.Core.Persistence
{
    /// <summary>CRC-32 (IEEE 802.3, zoals zip en PNG) om beschadigde secties in een save te herkennen.</summary>
    public static class Crc32
    {
        private const uint Polynomial = 0xEDB88320u;

        private static readonly uint[] Table = BuildTable();

        public static uint Compute(ReadOnlySpan<byte> data) => Append(0u, data);

        /// <summary>Rekent verder vanaf een eerdere uitkomst, zodat grote data in stukken kan.</summary>
        public static uint Append(uint crc, ReadOnlySpan<byte> data)
        {
            var value = ~crc;
            foreach (var b in data)
            {
                value = Table[(value ^ b) & 0xFF] ^ (value >> 8);
            }

            return ~value;
        }

        private static uint[] BuildTable()
        {
            var table = new uint[256];
            for (var i = 0u; i < table.Length; i++)
            {
                var entry = i;
                for (var bit = 0; bit < 8; bit++)
                {
                    entry = (entry & 1u) != 0 ? Polynomial ^ (entry >> 1) : entry >> 1;
                }

                table[i] = entry;
            }

            return table;
        }
    }
}
