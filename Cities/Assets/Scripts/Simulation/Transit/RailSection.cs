using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;
using Unity.Mathematics;

namespace Cities.Simulation.Transit
{
    /// <summary>
    /// Sectie <c>RAIL</c>: het spoornet met knopen, sporen, stations, wissels, overwegen en treinen. De haltes en lijnen van de
    /// passagiersstations staan in sectie <c>TRNS</c> (<see cref="TransitSection"/>); hier staat welke halte bij welk station hoort.
    /// De stand van de bomen wordt na het laden uit de treinen afgeleid en dus niet bewaard.
    /// </summary>
    public sealed class RailSection : ISaveSection
    {
        public const string SectionId = "RAIL";

        private const int MaxItems = 1_000_000;

        private readonly RailNetwork _rail;

        public RailSection(RailNetwork rail)
        {
            _rail = rail ?? throw new ArgumentNullException(nameof(rail));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            writer.Write(_rail.NextNodeId);
            writer.Write(_rail.NextTrackId);
            writer.Write(_rail.NextCrossingId);
            writer.Write(_rail.NextTrainId);
            WriteAll(writer, _rail.Nodes.ToArray(), n =>
            {
                writer.Write(n.Key);
                writer.Write(n.Value.x);
                writer.Write(n.Value.y);
            });
            WriteAll(writer, _rail.Tracks.ToArray(), t =>
            {
                writer.Write(t.Id);
                writer.Write(t.A);
                writer.Write(t.B);
                writer.Write(t.Length);
            });
            WriteAll(writer, _rail.Stations.ToArray(), s =>
            {
                writer.Write(s.Node);
                writer.Write((byte)s.Kind);
                writer.Write(s.Stop);
                writer.Write(s.PlatformLength);
            });
            WriteAll(writer, _rail.Switches.ToArray(), s =>
            {
                writer.Write(s.Key);
                writer.Write(s.Value);
            });
            WriteAll(writer, _rail.Crossings.ToArray(), c =>
            {
                writer.Write(c.Id);
                writer.Write(c.Track);
                writer.Write(c.Offset);
                writer.Write(c.Road);
            });
            WriteAll(writer, _rail.Trains.ToArray(), t =>
            {
                writer.Write(t.Id);
                writer.Write((byte)t.Kind);
                writer.Write(t.Cars);
                writer.Write(t.Position);
                WriteAll(writer, t.Route.ToArray(), writer.Write);
            });
        }

        public Action Read(SaveReader reader, int version)
        {
            var snapshot = new RailSnapshot
            {
                NextNode = reader.ReadInt32(),
                NextTrack = reader.ReadInt32(),
                NextCrossing = reader.ReadInt32(),
                NextTrain = reader.ReadInt32(),
            };
            snapshot.Nodes = ReadAll(reader, "knopen", () => new KeyValuePair<int, float2>(reader.ReadInt32(), new float2(reader.ReadSingle(), reader.ReadSingle())));
            snapshot.Tracks = ReadAll(reader, "sporen", () => Build(reader, () => new RailTrack(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadSingle())));
            snapshot.Stations = ReadAll(reader, "stations", () => Build(reader, () => new RailStation(reader.ReadInt32(), ReadKind(reader), reader.ReadInt32(), reader.ReadSingle())));
            snapshot.Switches = ReadAll(reader, "wissels", () => new KeyValuePair<int, int>(reader.ReadInt32(), reader.ReadInt32()));
            snapshot.Crossings = ReadAll(reader, "overwegen", () => new LevelCrossing(reader.ReadInt32(), reader.ReadInt32(), reader.ReadSingle(), reader.ReadInt32()));
            snapshot.Trains = ReadAll(reader, "treinen", () => Build(reader, () => ReadTrain(reader)));
            reader.ExpectEnd();
            try
            {
                RailNetwork.Check(snapshot);
            }
            catch (ArgumentException e)
            {
                throw reader.Corrupt(e.Message);
            }

            return () => _rail.Restore(snapshot);
        }

        private static TrainRun ReadTrain(SaveReader reader)
        {
            var id = reader.ReadInt32();
            var kind = ReadKind(reader);
            var cars = reader.ReadInt32();
            var position = reader.ReadSingle();
            var route = ReadAll(reader, "knopen in een route", reader.ReadInt32);
            return new TrainRun(id, kind, cars, route, position);
        }

        private static TrainKind ReadKind(SaveReader reader)
        {
            var value = reader.ReadByte();
            if (!Enum.IsDefined(typeof(TrainKind), (int)value))
            {
                throw reader.Corrupt($"onbekende soort trein: {value}");
            }

            return (TrainKind)value;
        }

        private static T Build<T>(SaveReader reader, Func<T> create)
        {
            try
            {
                return create();
            }
            catch (ArgumentException e)
            {
                throw reader.Corrupt(e.Message);
            }
        }

        private static void WriteAll<T>(SaveWriter writer, T[] items, Action<T> write)
        {
            writer.Write(items.Length);
            foreach (var item in items)
            {
                write(item);
            }
        }

        private static T[] ReadAll<T>(SaveReader reader, string what, Func<T> read)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > MaxItems)
            {
                throw reader.Corrupt($"onmogelijk aantal {what}: {count}");
            }

            var items = new T[count];
            for (var i = 0; i < count; i++)
            {
                items[i] = read();
            }

            return items;
        }
    }
}
