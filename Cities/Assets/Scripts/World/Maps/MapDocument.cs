using System;
using System.Collections.Generic;
using System.IO;
using Cities.Core.Persistence;
using Cities.World.Grid;
using Cities.World.Terrain;

namespace Cities.World.Maps
{
    public enum MapEdge : byte
    {
        South = 0,
        East = 1,
        North = 2,
        West = 3,
    }

    public enum ConnectionKind : byte
    {
        Road = 0,
        Highway = 1,
        Rail = 2,
        Waterway = 3,
        Air = 4,
    }

    /// <summary>Een verbinding met de buitenwereld op een kaartrand; <see cref="Offset"/> is de cel langs die rand.</summary>
    public readonly struct MapConnection : IEquatable<MapConnection>
    {
        public MapConnection(MapEdge edge, int offset, ConnectionKind kind)
        {
            Edge = edge;
            Offset = offset;
            Kind = kind;
        }

        public MapEdge Edge { get; }

        public int Offset { get; }

        public ConnectionKind Kind { get; }

        public bool Equals(MapConnection other) => Edge == other.Edge && Offset == other.Offset && Kind == other.Kind;

        public override bool Equals(object obj) => obj is MapConnection other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Edge, Offset, Kind);
    }

    /// <summary>Een afzetting of bos op de kaart: middelpunt en straal in cellen en de rijkdom van 0 tot 1.</summary>
    public readonly struct MapResource : IEquatable<MapResource>
    {
        public MapResource(ResourceKind kind, int x, int z, int radius, float richness)
        {
            Kind = kind;
            X = x;
            Z = z;
            Radius = radius;
            Richness = richness;
        }

        public ResourceKind Kind { get; }

        public int X { get; }

        public int Z { get; }

        public int Radius { get; }

        public float Richness { get; }

        public bool Covers(int x, int z)
        {
            var dx = x - X;
            var dz = z - Z;
            return (dx * dx) + (dz * dz) <= Radius * Radius;
        }

        public bool Equals(MapResource other) =>
            Kind == other.Kind && X == other.X && Z == other.Z && Radius == other.Radius && Richness.CompareTo(other.Richness) == 0;

        public override bool Equals(object obj) => obj is MapResource other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Kind, X, Z, Radius, Richness);
    }

    /// <summary>
    /// Een kaart zoals de kaarteditor (E21-07) hem bewerkt en bewaart: hoogte en water per hoekpunt, grondstoffen en de
    /// verbindingen met de buitenwereld. De bewerkingen zijn gewone methodes, zodat een editor-venster, een in-game tool en
    /// tests dezelfde code gebruiken. Opslaan gebruikt het savebestand (secties met compressie en controlegetallen) met de
    /// extensie <see cref="Extension"/>.
    /// </summary>
    public sealed class MapDocument
    {
        public const string Extension = ".ctymap";

        /// <summary>Minimale afstand in cellen tussen twee verbindingen op dezelfde rand.</summary>
        public const int MinConnectionSpacing = 4;

        public const int MaxResources = 4096;

        private const string DataSection = "MDAT";
        private const string HeightSection = "MHGT";
        private const string WaterSection = "MWTR";
        private const string ResourceSection = "MRES";
        private const string ConnectionSection = "MCON";

        private readonly float[] _heights;
        private readonly float[] _water;
        private readonly List<MapResource> _resources = new List<MapResource>();
        private readonly List<MapConnection> _connections = new List<MapConnection>();

        public MapDocument(WorldDimensions dimensions, string name, float baseHeight = 0f)
        {
            Dimensions = dimensions;
            Name = name ?? string.Empty;
            CornersX = dimensions.CellsX + 1;
            CornersZ = dimensions.CellsZ + 1;
            _heights = new float[CornersX * CornersZ];
            _water = new float[_heights.Length];
            if (baseHeight != 0f)
            {
                Array.Fill(_heights, baseHeight);
            }
        }

        public WorldDimensions Dimensions { get; }

        public string Name { get; set; }

        public int CornersX { get; }

        public int CornersZ { get; }

        public IReadOnlyList<MapResource> Resources => _resources;

        public IReadOnlyList<MapConnection> Connections => _connections;

        public float HeightAt(int x, int z) => _heights[CornerIndex(x, z)];

        public float WaterDepthAt(int x, int z) => _water[CornerIndex(x, z)];

        /// <summary>Alle hoogtes rij voor rij (index = z × <see cref="CornersX"/> + x), voor het overzetten naar de wereldkaart.</summary>
        public ReadOnlySpan<float> Heights => _heights;

        public ReadOnlySpan<float> WaterDepths => _water;

        public void SetHeight(int x, int z, float height) => _heights[CornerIndex(x, z)] = Clamp(height);

        public void SetWaterDepth(int x, int z, float depth) => _water[CornerIndex(x, z)] = Math.Max(0f, depth);

        /// <summary>Past het terreinpenseel toe op deze kaart; <paramref name="x"/> en <paramref name="z"/> in wereldmeters.</summary>
        public void Paint(TerraformMode mode, float x, float z, float radius, float step, float target = 0f)
        {
            if (!(radius > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "De straal moet positief zijn.");
            }

            var spacing = Dimensions.CellSize;
            var minX = Math.Max(0, (int)Math.Floor((x - radius) / spacing));
            var maxX = Math.Min(CornersX - 1, (int)Math.Ceiling((x + radius) / spacing));
            var minZ = Math.Max(0, (int)Math.Floor((z - radius) / spacing));
            var maxZ = Math.Min(CornersZ - 1, (int)Math.Ceiling((z + radius) / spacing));

            // Gladmaken leest de buren van vóór de streek, zodat de volgorde van de punten niet uitmaakt.
            var snapshot = mode == TerraformMode.Smooth ? (float[])_heights.Clone() : _heights;
            for (var cz = minZ; cz <= maxZ; cz++)
            {
                for (var cx = minX; cx <= maxX; cx++)
                {
                    var dx = (cx * spacing) - x;
                    var dz = (cz * spacing) - z;
                    var distance = (float)Math.Sqrt((dx * dx) + (dz * dz));
                    if (distance > radius)
                    {
                        continue;
                    }

                    var weight = TerrainBrush.Falloff(distance, radius);
                    var index = (cz * CornersX) + cx;
                    var height = _heights[index];
                    switch (mode)
                    {
                        case TerraformMode.Raise:
                            height += step * weight;
                            break;
                        case TerraformMode.Lower:
                            height -= step * weight;
                            break;
                        case TerraformMode.Smooth:
                            height = MoveTowards(height, Average(snapshot, cx, cz), step * weight);
                            break;
                        default:
                            height = MoveTowards(height, target, step * weight);
                            break;
                    }

                    _heights[index] = Clamp(height);
                }
            }
        }

        /// <summary>Vult het water tot <paramref name="level"/> (meter) binnen de straal, waar het terrein lager ligt.</summary>
        public void FillWater(float x, float z, float radius, float level)
        {
            ForCornersInCircle(x, z, radius, index => _water[index] = Math.Max(_water[index], level - _heights[index]));
        }

        public void ClearWater(float x, float z, float radius)
        {
            ForCornersInCircle(x, z, radius, index => _water[index] = 0f);
        }

        public bool PlaceResource(MapResource resource, out string reason)
        {
            if (resource.X < 0 || resource.Z < 0 || resource.X >= Dimensions.CellsX || resource.Z >= Dimensions.CellsZ)
            {
                reason = "Buiten de kaart";
                return false;
            }

            if (resource.Radius < 1 || resource.Radius > Dimensions.CellsPerTile)
            {
                reason = $"De straal is 1 tot {Dimensions.CellsPerTile} cellen";
                return false;
            }

            if (!(resource.Richness > 0f) || resource.Richness > 1f)
            {
                reason = "De rijkdom is groter dan 0 en hoogstens 1";
                return false;
            }

            if (_resources.Count >= MaxResources)
            {
                reason = "Te veel grondstoffen op de kaart";
                return false;
            }

            _resources.Add(resource);
            reason = null;
            return true;
        }

        /// <summary>Haalt alle grondstoffen weg die de cel dekken; geeft het aantal.</summary>
        public int RemoveResourcesAt(int x, int z) => _resources.RemoveAll(resource => resource.Covers(x, z));

        public bool AddConnection(MapConnection connection, out string reason)
        {
            var length = EdgeLength(connection.Edge);
            if (connection.Offset < 0 || connection.Offset >= length)
            {
                reason = "Buiten de kaartrand";
                return false;
            }

            foreach (var other in _connections)
            {
                if (other.Edge == connection.Edge && Math.Abs(other.Offset - connection.Offset) < MinConnectionSpacing)
                {
                    reason = $"Te dicht bij een andere verbinding (minimaal {MinConnectionSpacing} cellen)";
                    return false;
                }
            }

            _connections.Add(connection);
            reason = null;
            return true;
        }

        public bool RemoveConnection(MapConnection connection) => _connections.Remove(connection);

        /// <summary>Wereldpositie (x, z in meter) van een verbinding, op de rand van de kaart.</summary>
        public (float X, float Z) PositionOf(MapConnection connection)
        {
            var along = (connection.Offset + 0.5f) * Dimensions.CellSize;
            switch (connection.Edge)
            {
                case MapEdge.South:
                    return (along, 0f);
                case MapEdge.North:
                    return (along, Dimensions.SizeZ);
                case MapEdge.West:
                    return (0f, along);
                default:
                    return (Dimensions.SizeX, along);
            }
        }

        /// <summary>Controleert of de kaart speelbaar is; problemen komen in <paramref name="errors"/>.</summary>
        public bool Validate(ICollection<string> errors)
        {
            var before = errors.Count;
            if (string.IsNullOrWhiteSpace(Name))
            {
                errors.Add("De kaart heeft geen naam.");
            }

            var hasLand = false;
            foreach (var connection in _connections)
            {
                if (connection.Kind == ConnectionKind.Road || connection.Kind == ConnectionKind.Highway)
                {
                    hasLand = true;
                }

                var (x, z) = PositionOf(connection);
                var cornerX = Math.Min((int)Math.Round(x / Dimensions.CellSize), CornersX - 1);
                var cornerZ = Math.Min((int)Math.Round(z / Dimensions.CellSize), CornersZ - 1);
                var wet = _water[(cornerZ * CornersX) + cornerX] > 0.05f;
                if (wet != (connection.Kind == ConnectionKind.Waterway) && connection.Kind != ConnectionKind.Air)
                {
                    errors.Add($"Verbinding {connection.Kind} op {connection.Edge} {connection.Offset} ligt {(wet ? "in het water" : "op het droge")}.");
                }
            }

            if (!hasLand)
            {
                errors.Add("De kaart heeft minstens één weg of snelweg naar buiten nodig.");
            }

            return errors.Count == before;
        }

        public void Save(Stream output, DateTime savedAtUtc)
        {
            var metadata = new SaveMetadata { CityName = Name, GameVersion = "map", SavedAtUtc = savedAtUtc };
            SaveFile.Write(output, metadata, new ISaveSection[]
            {
                new Section(DataSection, writer => WriteData(writer), null),
                new Section(HeightSection, writer => writer.WriteShuffled<float>(_heights), null),
                new Section(WaterSection, writer => writer.WriteShuffled<float>(_water), null),
                new Section(ResourceSection, WriteResources, null),
                new Section(ConnectionSection, WriteConnections, null),
            });
        }

        public static MapDocument Load(Stream input)
        {
            WorldDimensions dimensions = default;
            var name = string.Empty;
            var heights = Array.Empty<float>();
            var water = Array.Empty<float>();
            var resources = new List<MapResource>();
            var connections = new List<MapConnection>();
            var read = new SectionCounter();

            SaveFile.Read(input, new ISaveSection[]
            {
                new Section(DataSection, null, reader =>
                {
                    dimensions = new WorldDimensions(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadSingle());
                    name = reader.ReadString();
                    reader.ExpectEnd();
                    return () => read.Mark();
                }),
                new Section(HeightSection, null, reader =>
                {
                    var values = ReadLayer(reader, dimensions);
                    return () =>
                    {
                        heights = values;
                        read.Mark();
                    };
                }),
                new Section(WaterSection, null, reader =>
                {
                    var values = ReadLayer(reader, dimensions);
                    return () =>
                    {
                        water = values;
                        read.Mark();
                    };
                }),
                new Section(ResourceSection, null, reader =>
                {
                    var list = ReadResources(reader);
                    return () =>
                    {
                        resources = list;
                        read.Mark();
                    };
                }),
                new Section(ConnectionSection, null, reader =>
                {
                    var list = ReadConnections(reader);
                    return () =>
                    {
                        connections = list;
                        read.Mark();
                    };
                }),
            });

            if (!read.IsComplete(5))
            {
                throw new SaveFormatException("De kaart mist een onderdeel.");
            }

            var document = new MapDocument(dimensions, name);
            heights.CopyTo(document._heights, 0);
            water.CopyTo(document._water, 0);
            document._resources.AddRange(resources);
            document._connections.AddRange(connections);
            return document;
        }

        private void WriteData(SaveWriter writer)
        {
            writer.Write(Dimensions.TilesX);
            writer.Write(Dimensions.TilesZ);
            writer.Write(Dimensions.CellsPerTile);
            writer.Write(Dimensions.CellSize);
            writer.Write(Name);
        }

        private void WriteResources(SaveWriter writer)
        {
            writer.Write(_resources.Count);
            foreach (var resource in _resources)
            {
                writer.Write((byte)resource.Kind);
                writer.Write(resource.X);
                writer.Write(resource.Z);
                writer.Write(resource.Radius);
                writer.Write(resource.Richness);
            }
        }

        private void WriteConnections(SaveWriter writer)
        {
            writer.Write(_connections.Count);
            foreach (var connection in _connections)
            {
                writer.Write((byte)connection.Edge);
                writer.Write(connection.Offset);
                writer.Write((byte)connection.Kind);
            }
        }

        /// <summary>Leest een laag; de lengte moet bij de afmetingen uit de kaartkop passen.</summary>
        private static float[] ReadLayer(SaveReader reader, WorldDimensions dimensions)
        {
            if (dimensions.TileCount == 0)
            {
                throw reader.Corrupt("de kaartkop komt na de laag");
            }

            var values = new float[(dimensions.CellsX + 1) * (dimensions.CellsZ + 1)];
            reader.ReadShuffled<float>(values);
            reader.ExpectEnd();
            return values;
        }

        private static List<MapResource> ReadResources(SaveReader reader)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > MaxResources)
            {
                throw reader.Corrupt($"{count} grondstoffen");
            }

            var list = new List<MapResource>(count);
            for (var i = 0; i < count; i++)
            {
                list.Add(new MapResource((ResourceKind)reader.ReadByte(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadSingle()));
            }

            reader.ExpectEnd();
            return list;
        }

        private static List<MapConnection> ReadConnections(SaveReader reader)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > 1024)
            {
                throw reader.Corrupt($"{count} verbindingen");
            }

            var list = new List<MapConnection>(count);
            for (var i = 0; i < count; i++)
            {
                list.Add(new MapConnection((MapEdge)reader.ReadByte(), reader.ReadInt32(), (ConnectionKind)reader.ReadByte()));
            }

            reader.ExpectEnd();
            return list;
        }

        private int EdgeLength(MapEdge edge) => edge == MapEdge.South || edge == MapEdge.North ? Dimensions.CellsX : Dimensions.CellsZ;

        private int CornerIndex(int x, int z)
        {
            if (x < 0 || z < 0 || x >= CornersX || z >= CornersZ)
            {
                throw new ArgumentOutOfRangeException(nameof(x), $"Hoekpunt ({x}, {z}) ligt buiten de kaart.");
            }

            return (z * CornersX) + x;
        }

        private void ForCornersInCircle(float x, float z, float radius, Action<int> apply)
        {
            if (!(radius > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "De straal moet positief zijn.");
            }

            var spacing = Dimensions.CellSize;
            for (var cz = Math.Max(0, (int)Math.Floor((z - radius) / spacing)); cz <= Math.Min(CornersZ - 1, (int)Math.Ceiling((z + radius) / spacing)); cz++)
            {
                for (var cx = Math.Max(0, (int)Math.Floor((x - radius) / spacing)); cx <= Math.Min(CornersX - 1, (int)Math.Ceiling((x + radius) / spacing)); cx++)
                {
                    var dx = (cx * spacing) - x;
                    var dz = (cz * spacing) - z;
                    if ((dx * dx) + (dz * dz) <= radius * radius)
                    {
                        apply((cz * CornersX) + cx);
                    }
                }
            }
        }

        private float Average(float[] values, int x, int z)
        {
            var sum = 0f;
            var count = 0;
            for (var dz = -1; dz <= 1; dz++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var nx = x + dx;
                    var nz = z + dz;
                    if (nx >= 0 && nz >= 0 && nx < CornersX && nz < CornersZ)
                    {
                        sum += values[(nz * CornersX) + nx];
                        count++;
                    }
                }
            }

            return sum / count;
        }

        private static float MoveTowards(float current, float target, float maxDelta) =>
            Math.Abs(target - current) <= maxDelta ? target : current + (Math.Sign(target - current) * maxDelta);

        private static float Clamp(float height) => Math.Max(TerrainBrush.MinHeight, Math.Min(TerrainBrush.MaxHeight, height));

        /// <summary>Sectie met losse schrijf- en leesfuncties, zodat het opslaan en laden van een kaart op één plek staat.</summary>
        private sealed class Section : ISaveSection
        {
            private readonly Action<SaveWriter> _write;
            private readonly Func<SaveReader, Action> _read;

            public Section(string id, Action<SaveWriter> write, Func<SaveReader, Action> read)
            {
                Id = id;
                _write = write;
                _read = read;
            }

            public string Id { get; }

            public int Version => 1;

            public void Write(SaveWriter writer) => _write(writer);

            public Action Read(SaveReader reader, int version) => _read(reader);
        }

        private sealed class SectionCounter
        {
            private int _count;

            public void Mark() => _count++;

            public bool IsComplete(int expected) => _count == expected;
        }
    }
}
