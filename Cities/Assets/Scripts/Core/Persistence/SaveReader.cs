using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

namespace Cities.Core.Persistence
{
    /// <summary>
    /// Leest de inhoud van één sectie. Lezen voorbij het einde geeft een <see cref="SaveFormatException"/> in plaats van
    /// vreemde waarden, zodat een afgekapte of verkeerde sectie altijd opvalt.
    /// </summary>
    public sealed class SaveReader
    {
        private readonly byte[] _data;
        private readonly string _section;
        private int _position;

        internal SaveReader(byte[] data, string section)
        {
            _data = data;
            _section = section;
        }

        public int Remaining => _data.Length - _position;

        public bool ReadBool() => Take(1)[0] != 0;

        public byte ReadByte() => Take(1)[0];

        public int ReadInt32() => BinaryPrimitives.ReadInt32LittleEndian(Take(4));

        public uint ReadUInt32() => BinaryPrimitives.ReadUInt32LittleEndian(Take(4));

        public long ReadInt64() => BinaryPrimitives.ReadInt64LittleEndian(Take(8));

        public float ReadSingle() => BitConverter.Int32BitsToSingle(ReadInt32());

        public double ReadDouble() => BitConverter.Int64BitsToDouble(ReadInt64());

        /// <summary>Tekst zoals <see cref="SaveWriter.Write(string)"/> hem schrijft (lengte als 7-bits variabel getal).</summary>
        public string ReadString()
        {
            var length = Read7BitLength();
            return Encoding.UTF8.GetString(Take(length));
        }

        /// <summary>Leest een array met precies de lengte van <paramref name="destination"/>.</summary>
        public void ReadArray<T>(Span<T> destination) where T : unmanaged
        {
            var count = ReadInt32();
            if (count != destination.Length)
            {
                throw Corrupt($"array met {count} elementen waar {destination.Length} verwacht werden");
            }

            var bytes = MemoryMarshal.AsBytes(destination);
            Take(bytes.Length).CopyTo(bytes);
        }

        /// <summary>Leest een array met de opgeslagen lengte, met een bovengrens tegen onzinnige groottes.</summary>
        public T[] ReadArray<T>(int maxLength) where T : unmanaged
        {
            var count = ReadInt32();
            if (count < 0 || count > maxLength)
            {
                throw Corrupt($"array met {count} elementen (maximaal {maxLength})");
            }

            var values = new T[count];
            var bytes = MemoryMarshal.AsBytes(values.AsSpan());
            Take(bytes.Length).CopyTo(bytes);
            return values;
        }

        /// <summary>Leest wat <see cref="SaveWriter.WriteShuffled{T}"/> schreef, met precies de lengte van <paramref name="destination"/>.</summary>
        public void ReadShuffled<T>(Span<T> destination) where T : unmanaged
        {
            var count = ReadInt32();
            if (count != destination.Length)
            {
                throw Corrupt($"array met {count} elementen waar {destination.Length} verwacht werden");
            }

            if (count == 0)
            {
                return;
            }

            var bytes = MemoryMarshal.AsBytes(destination);
            ByteShuffle.Unshuffle(Take(bytes.Length), bytes, bytes.Length / count);
        }

        /// <summary>Geeft een fout als de sectie meer bytes bevat dan gelezen; dat wijst op een verkeerde versie.</summary>
        public void ExpectEnd()
        {
            if (Remaining != 0)
            {
                throw Corrupt($"{Remaining} ongelezen bytes");
            }
        }

        /// <summary>Fout voor ongeldige inhoud, met de naam van de sectie erbij.</summary>
        public SaveFormatException Corrupt(string reason) => new SaveFormatException($"Sectie {_section} is ongeldig: {reason}.");

        private int Read7BitLength()
        {
            var result = 0;
            for (var shift = 0; shift < 35; shift += 7)
            {
                var b = ReadByte();
                result |= (b & 0x7F) << shift;
                if ((b & 0x80) == 0)
                {
                    if (result < 0)
                    {
                        break;
                    }

                    return result;
                }
            }

            throw Corrupt("ongeldige tekstlengte");
        }

        private ReadOnlySpan<byte> Take(int count)
        {
            if (count < 0 || count > Remaining)
            {
                throw Corrupt($"{count} bytes nodig, {Remaining} over");
            }

            var span = new ReadOnlySpan<byte>(_data, _position, count);
            _position += count;
            return span;
        }
    }
}
