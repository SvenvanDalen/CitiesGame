using System;

namespace Cities.Core.Persistence
{
    /// <summary>
    /// Zet de bytes van elementen om naar vlakken: byte 0 van alle elementen, dan byte 1, enzovoort. Bij floats van een
    /// glad verlopende laag staan de exponent- en hoge mantissebytes dan naast elkaar, wat Deflate veel beter comprimeert.
    /// </summary>
    public static class ByteShuffle
    {
        public static void Shuffle(ReadOnlySpan<byte> source, Span<byte> destination, int elementSize)
        {
            var count = Validate(source, destination, elementSize);
            for (var plane = 0; plane < elementSize; plane++)
            {
                var output = destination.Slice(plane * count, count);
                for (var i = 0; i < count; i++)
                {
                    output[i] = source[(i * elementSize) + plane];
                }
            }
        }

        public static void Unshuffle(ReadOnlySpan<byte> source, Span<byte> destination, int elementSize)
        {
            var count = Validate(source, destination, elementSize);
            for (var plane = 0; plane < elementSize; plane++)
            {
                var input = source.Slice(plane * count, count);
                for (var i = 0; i < count; i++)
                {
                    destination[(i * elementSize) + plane] = input[i];
                }
            }
        }

        private static int Validate(ReadOnlySpan<byte> source, Span<byte> destination, int elementSize)
        {
            if (elementSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(elementSize), elementSize, "Elementgrootte moet minstens 1 zijn.");
            }

            if (source.Length != destination.Length || source.Length % elementSize != 0)
            {
                throw new ArgumentException("Bron en doel moeten even lang zijn en een veelvoud van de elementgrootte.", nameof(destination));
            }

            return source.Length / elementSize;
        }
    }
}
