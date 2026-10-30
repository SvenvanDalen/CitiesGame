using System;
using Cities.Core.Persistence;

namespace Cities.World.Water
{
    /// <summary>
    /// Sectie <c>WATR</c>: de grove waterdiepte en de stromen van de <see cref="WaterSimulation"/>. De fijne diepte in
    /// <c>WMAP</c> is daarvan afgeleid en wordt bij de volgende stap opnieuw berekend. Een save met een ander raster of
    /// met negatieve of oneindige waarden wordt geweigerd.
    /// </summary>
    public sealed class WaterSection : ISaveSection
    {
        public const string SectionId = "WATR";

        private readonly WaterSimulation _water;

        public WaterSection(WaterSimulation water) => _water = water ?? throw new ArgumentNullException(nameof(water));

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            writer.Write(_water.Width);
            writer.Write(_water.Depth);
            writer.WriteShuffled<float>(_water.Water.AsReadOnlySpan());
            writer.WriteShuffled<float>(_water.Flux.AsReadOnlySpan());
        }

        public Action Read(SaveReader reader, int version)
        {
            var width = reader.ReadInt32();
            var depth = reader.ReadInt32();
            if (width != _water.Width || depth != _water.Depth)
            {
                throw reader.Corrupt($"waterraster van {width}×{depth}, deze scene heeft {_water.Width}×{_water.Depth}");
            }

            var water = new float[_water.Water.Length];
            var flux = new float[_water.Flux.Length];
            reader.ReadShuffled<float>(water);
            reader.ReadShuffled<float>(flux);
            reader.ExpectEnd();
            Validate(reader, water, "waterdiepte");
            Validate(reader, flux, "stroom");

            return () => _water.Restore(water, flux);
        }

        private static void Validate(SaveReader reader, float[] values, string what)
        {
            foreach (var value in values)
            {
                if (!float.IsFinite(value) || value < 0f)
                {
                    throw reader.Corrupt($"ongeldige {what} {value}");
                }
            }
        }
    }
}
