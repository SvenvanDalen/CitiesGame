using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;

namespace Cities.Simulation.Outside
{
    /// <summary>Sectie <c>OUTC</c>: de buitenverbindingen aan de kaartrand (E14-04).</summary>
    public sealed class OutsideSection : ISaveSection
    {
        public const string SectionId = "OUTC";

        private readonly OutsideConnections _connections;

        public OutsideSection(OutsideConnections connections)
        {
            _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            writer.Write(_connections.MapSize);
            writer.Write(_connections.NextId);
            writer.Write(_connections.Connections.Count);
            foreach (var c in _connections.Connections)
            {
                writer.Write(c.Id);
                writer.Write((byte)c.Kind);
                writer.Write((byte)c.Edge);
                writer.Write(c.Offset);
                writer.Write(c.Level);
                writer.Write(c.IsOpen);
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var mapSize = reader.ReadSingle();
            var nextId = reader.ReadInt32();
            var count = reader.ReadInt32();
            if (!(Math.Abs(mapSize - _connections.MapSize) < 0.01f))
            {
                throw reader.Corrupt("andere kaartmaat");
            }

            if (count < 0 || count > OutsideConnections.MaxConnections)
            {
                throw reader.Corrupt($"onmogelijk aantal buitenverbindingen: {count}");
            }

            var loaded = new List<OutsideConnection>();
            for (var i = 0; i < count; i++)
            {
                loaded.Add(ReadConnection(reader, mapSize));
            }

            reader.ExpectEnd();
            if (loaded.Any(c => c.Id < 1 || c.Id >= nextId) || loaded.Select(c => c.Id).Distinct().Count() != count)
            {
                throw reader.Corrupt("volgnummer te laag of dubbel ID");
            }

            if (Enum.GetValues(typeof(OutsideKind)).Cast<OutsideKind>().Any(k => loaded.Count(c => c.Kind == k) > OutsideConnections.MaxPerKind))
            {
                throw reader.Corrupt("te veel verbindingen van één soort");
            }

            return () => _connections.Restore(loaded, nextId);
        }

        private static OutsideConnection ReadConnection(SaveReader reader, float mapSize)
        {
            var id = reader.ReadInt32();
            var kind = (OutsideKind)reader.ReadByte();
            var edge = (MapEdge)reader.ReadByte();
            var offset = reader.ReadSingle();
            var level = reader.ReadInt32();
            var open = reader.ReadBool();
            if (!Enum.IsDefined(typeof(OutsideKind), kind) || !Enum.IsDefined(typeof(MapEdge), edge)
                || !(offset >= 0f && offset <= mapSize) || level < 1 || level > OutsideConnection.MaxLevel)
            {
                throw reader.Corrupt("ongeldige buitenverbinding");
            }

            return new OutsideConnection(id, kind, edge, offset, level, open);
        }
    }
}
