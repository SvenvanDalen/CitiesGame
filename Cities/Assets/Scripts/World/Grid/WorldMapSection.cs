using System;
using Cities.Core.Persistence;

namespace Cities.World.Grid
{
    /// <summary>
    /// Sectie <c>WMAP</c>: afmetingen en alle lagen van de <see cref="WorldMap"/> (hoogte, waterdiepte, zones,
    /// grondvervuiling, grondwaarde en vanaf versie 2 de grondbedekking). Een save met een andere kaartgrootte wordt
    /// geweigerd.
    /// </summary>
    public sealed class WorldMapSection : ISaveSection
    {
        public const string SectionId = "WMAP";

        private readonly WorldMap _map;

        public WorldMapSection(WorldMap map) => _map = map ?? throw new ArgumentNullException(nameof(map));

        public string Id => SectionId;

        public int Version => 2;

        public void Write(SaveWriter writer)
        {
            var dimensions = _map.Dimensions;
            writer.Write(dimensions.TilesX);
            writer.Write(dimensions.TilesZ);
            writer.Write(dimensions.CellsPerTile);
            writer.Write(dimensions.CellSize);
            writer.WriteShuffled<float>(_map.Height.Values.AsReadOnlySpan());
            writer.WriteShuffled<float>(_map.WaterDepth.Values.AsReadOnlySpan());
            writer.WriteShuffled<ZoneCell>(_map.Zones.Values.AsReadOnlySpan());
            writer.WriteShuffled<float>(_map.GroundPollution.Values.AsReadOnlySpan());
            writer.WriteShuffled<float>(_map.LandValue.Values.AsReadOnlySpan());
            writer.WriteShuffled<GroundCover>(_map.GroundCover.Values.AsReadOnlySpan());
        }

        public Action Read(SaveReader reader, int version)
        {
            var saved = new WorldDimensions(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadSingle());
            if (saved != _map.Dimensions)
            {
                throw reader.Corrupt($"kaart van {saved}, deze scene heeft {_map.Dimensions}");
            }

            // Eerst in losse arrays lezen: pas als alles klopt, wordt de kaart overschreven.
            var height = Read<float>(reader, _map.Height.Values.Length);
            var water = Read<float>(reader, _map.WaterDepth.Values.Length);
            var zones = Read<ZoneCell>(reader, _map.Zones.Values.Length);
            var pollution = Read<float>(reader, _map.GroundPollution.Values.Length);
            var landValue = Read<float>(reader, _map.LandValue.Values.Length);

            // Versie 1 kende geen grondbedekking: alles natuurlijk.
            var cover = version >= 2 ? Read<GroundCover>(reader, _map.GroundCover.Values.Length) : new GroundCover[_map.GroundCover.Values.Length];
            reader.ExpectEnd();
            Validate(reader, zones, cover);

            return () =>
            {
                _map.Height.Values.CopyFrom(height);
                _map.WaterDepth.Values.CopyFrom(water);
                _map.Zones.Values.CopyFrom(zones);
                _map.GroundPollution.Values.CopyFrom(pollution);
                _map.LandValue.Values.CopyFrom(landValue);
                _map.GroundCover.Values.CopyFrom(cover);
                _map.NotifyHeightChanged(_map.AllCorners);
                _map.NotifyGroundCoverChanged(_map.AllCells);
            };
        }

        private static T[] Read<T>(SaveReader reader, int length) where T : unmanaged
        {
            var values = new T[length];
            reader.ReadShuffled<T>(values);
            return values;
        }

        private static void Validate(SaveReader reader, ZoneCell[] zones, GroundCover[] cover)
        {
            foreach (var value in cover)
            {
                if ((byte)value > (byte)GroundCover.Built)
                {
                    throw reader.Corrupt($"onbekende grondbedekking {(byte)value}");
                }
            }

            foreach (var zone in zones)
            {
                if ((byte)zone.Type > (byte)ZoneType.Office || (byte)zone.Density > (byte)ZoneDensity.High)
                {
                    throw reader.Corrupt($"onbekende zone {(byte)zone.Type}/{(byte)zone.Density}");
                }
            }
        }
    }
}
