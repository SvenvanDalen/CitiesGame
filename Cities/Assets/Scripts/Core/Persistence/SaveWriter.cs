using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Cities.Core.Persistence
{
    /// <summary>
    /// Schrijft de inhoud van één sectie. Getallen zijn little-endian; arrays van eenvoudige structs worden als één
    /// blok bytes geschreven, zodat lagen van miljoenen cellen snel gaan.
    /// </summary>
    public sealed class SaveWriter
    {
        private readonly MemoryStream _stream;
        private readonly BinaryWriter _writer;

        internal SaveWriter(int capacity)
        {
            _stream = new MemoryStream(capacity);
            _writer = new BinaryWriter(_stream, Encoding.UTF8, leaveOpen: true);
        }

        /// <summary>Aantal geschreven bytes.</summary>
        public long Length => _stream.Length;

        public void Write(bool value) => _writer.Write(value);

        public void Write(byte value) => _writer.Write(value);

        public void Write(int value) => _writer.Write(value);

        public void Write(uint value) => _writer.Write(value);

        public void Write(long value) => _writer.Write(value);

        public void Write(float value) => _writer.Write(value);

        public void Write(double value) => _writer.Write(value);

        /// <summary>UTF-8 met de lengte ervoor; null wordt een lege tekst.</summary>
        public void Write(string value) => _writer.Write(value ?? string.Empty);

        /// <summary>Aantal elementen gevolgd door de ruwe bytes; lees terug met <see cref="SaveReader.ReadArray{T}(Span{T})"/>.</summary>
        public void WriteArray<T>(ReadOnlySpan<T> values) where T : unmanaged
        {
            _writer.Write(values.Length);
            _writer.Write(MemoryMarshal.AsBytes(values));
        }

        /// <summary>
        /// Als <see cref="WriteArray{T}"/>, maar met de bytes per positie gegroepeerd (eerst alle eerste bytes, dan alle
        /// tweede, ...). Bij lagen met geleidelijk veranderende waarden (hoogte, grondwaarde) maakt dat het bestand
        /// ongeveer een derde kleiner en het comprimeren twee keer zo snel. Lees terug met
        /// <see cref="SaveReader.ReadShuffled{T}"/>.
        /// </summary>
        public void WriteShuffled<T>(ReadOnlySpan<T> values) where T : unmanaged
        {
            var bytes = MemoryMarshal.AsBytes(values);
            _writer.Write(values.Length);
            if (values.Length == 0)
            {
                return;
            }

            var shuffled = new byte[bytes.Length];
            ByteShuffle.Shuffle(bytes, shuffled, bytes.Length / values.Length);
            _writer.Write(shuffled);
        }

        internal ArraySegment<byte> ToSegment()
        {
            _writer.Flush();
            return new ArraySegment<byte>(_stream.GetBuffer(), 0, (int)_stream.Length);
        }
    }
}
