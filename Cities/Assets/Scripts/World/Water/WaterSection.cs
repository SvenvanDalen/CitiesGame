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

        public int Version => 2;

        public void Write(SaveWriter writer)
        {
            writer.Write(_water.Width);
            writer.Write(_water.Depth);
            writer.WriteShuffled<float>(_water.Water.AsReadOnlySpan());
            writer.WriteShuffled<float>(_water.Flux.AsReadOnlySpan());
            writer.Write(_water.Sources.Count);
            foreach (var source in _water.Sources.Items)
            {
                writer.Write(source.X);
                writer.Write(source.Z);
                writer.Write(source.Discharge);
            }
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
            var sources = version >= 2 ? ReadSources(reader) : Array.Empty<WaterSource>();
            reader.ExpectEnd();
            Validate(reader, water, "waterdiepte");
            Validate(reader, flux, "stroom");

            return () => _water.Restore(water, flux, sources);
        }

        private static WaterSource[] ReadSources(SaveReader reader)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > WaterSources.MaxCount)
            {
                throw reader.Corrupt($"{count} bronnen");
            }

            var sources = new WaterSource[count];
            for (var i = 0; i < count; i++)
            {
                var source = new WaterSource(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                if (!float.IsFinite(source.X) || !float.IsFinite(source.Z) || !float.IsFinite(source.Discharge))
                {
                    throw reader.Corrupt($"bron {i} is geen getal");
                }

                sources[i] = source;
            }

            return sources;
        }

        private static void Validate(SaveReader reader, float[] values, string what)
        {
            var bad = Array.FindIndex(values, value => !float.IsFinite(value) || value < 0f);
            if (bad >= 0)
            {
                throw reader.Corrupt($"ongeldige {what} {values[bad]}");
            }
        }
    }
}
