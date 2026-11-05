using System;
using System.Collections.Generic;
using Cities.Core.Persistence;
using Cities.Gameplay.Services;
using Cities.Gameplay.Zoning;
using Cities.Simulation.Industry;
using Unity.Mathematics;

namespace Cities.Gameplay.Persistence
{
    public readonly struct SavedLine
    {
        public SavedLine(string name, int[] stopIndices, bool isLoop, int vehicles, float headway)
        {
            Name = name ?? string.Empty;
            StopIndices = stopIndices ?? Array.Empty<int>();
            IsLoop = isLoop;
            Vehicles = vehicles;
            Headway = headway;
        }

        public string Name { get; }

        /// <summary>Plekken in <see cref="CityState.TransitStops"/>.</summary>
        public int[] StopIndices { get; }

        public bool IsLoop { get; }

        public int Vehicles { get; }

        public float Headway { get; }
    }

    public readonly struct SavedService
    {
        public SavedService(CityService kind, float2 position)
        {
            Kind = kind;
            Position = position;
        }

        public CityService Kind { get; }

        public float2 Position { get; }
    }

    public readonly struct SavedSite
    {
        public SavedSite(IndustryKind kind, int level, float richness)
        {
            Kind = kind;
            Level = level;
            Richness = richness;
        }

        public IndustryKind Kind { get; }

        public int Level { get; }

        public float Richness { get; }
    }

    public readonly struct SavedAttraction
    {
        public SavedAttraction(string name, int touristsPerDay, long ticketPrice, long upkeepPerWeek)
        {
            Name = name ?? string.Empty;
            TouristsPerDay = touristsPerDay;
            TicketPrice = ticketPrice;
            UpkeepPerWeek = upkeepPerWeek;
        }

        public string Name { get; }

        public int TouristsPerDay { get; }

        public long TicketPrice { get; }

        public long UpkeepPerWeek { get; }
    }

    /// <summary>
    /// Alles wat de spelmodellen van de hoofdscène (E26-03 t/m E26-12) bijhouden en niet al in een eigen opslagsectie zit
    /// (E26-14): saldo, inwoners, gegroeide gebouwen, openbaar vervoer, diensten, winningen en toerisme.
    /// </summary>
    public sealed class CityState
    {
        public long Balance { get; set; }

        public int Population { get; set; }

        public List<BuiltBuilding> Buildings { get; } = new List<BuiltBuilding>();

        public List<float2> TransitStops { get; } = new List<float2>();

        public List<SavedLine> Lines { get; } = new List<SavedLine>();

        public List<SavedService> Services { get; } = new List<SavedService>();

        public List<SavedSite> Sites { get; } = new List<SavedSite>();

        public List<SavedAttraction> Attractions { get; } = new List<SavedAttraction>();

        public int Beds { get; set; }
    }

    /// <summary>Opslagsectie <c>CITY</c> voor de <see cref="CityState"/>; het eigenlijke ophalen en toepassen doet de host.</summary>
    public sealed class CityStateSection : ISaveSection
    {
        public const string SectionId = "CITY";
        public const int MaxItems = 1_000_000;

        private readonly Func<CityState> _capture;
        private readonly Action<CityState> _apply;

        public CityStateSection(Func<CityState> capture, Action<CityState> apply)
        {
            _capture = capture ?? throw new ArgumentNullException(nameof(capture));
            _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            var state = _capture();
            writer.Write(state.Balance);
            writer.Write(state.Population);
            writer.Write(state.Beds);

            writer.Write(state.Buildings.Count);
            foreach (var b in state.Buildings)
            {
                writer.Write(b.DefinitionId);
                writer.Write(b.OriginX);
                writer.Write(b.OriginZ);
                writer.Write(b.Width);
                writer.Write(b.Depth);
                writer.Write(b.QuarterTurns);
                writer.Write(b.X);
                writer.Write(b.Y);
                writer.Write(b.Z);
            }

            writer.Write(state.TransitStops.Count);
            foreach (var stop in state.TransitStops)
            {
                writer.Write(stop.x);
                writer.Write(stop.y);
            }

            writer.Write(state.Lines.Count);
            foreach (var line in state.Lines)
            {
                writer.Write(line.Name);
                writer.Write(line.IsLoop);
                writer.Write(line.Vehicles);
                writer.Write(line.Headway);
                writer.Write(line.StopIndices.Length);
                foreach (var index in line.StopIndices)
                {
                    writer.Write(index);
                }
            }

            writer.Write(state.Services.Count);
            foreach (var service in state.Services)
            {
                writer.Write((byte)service.Kind);
                writer.Write(service.Position.x);
                writer.Write(service.Position.y);
            }

            writer.Write(state.Sites.Count);
            foreach (var site in state.Sites)
            {
                writer.Write((byte)site.Kind);
                writer.Write(site.Level);
                writer.Write(site.Richness);
            }

            writer.Write(state.Attractions.Count);
            foreach (var a in state.Attractions)
            {
                writer.Write(a.Name);
                writer.Write(a.TouristsPerDay);
                writer.Write(a.TicketPrice);
                writer.Write(a.UpkeepPerWeek);
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var state = new CityState
            {
                Balance = reader.ReadInt64(),
                Population = reader.ReadInt32(),
                Beds = reader.ReadInt32(),
            };

            var count = Count(reader, "gebouwen");
            for (var i = 0; i < count; i++)
            {
                state.Buildings.Add(new BuiltBuilding(
                    reader.ReadString(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(),
                    reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
            }

            count = Count(reader, "haltes");
            for (var i = 0; i < count; i++)
            {
                state.TransitStops.Add(new float2(reader.ReadSingle(), reader.ReadSingle()));
            }

            count = Count(reader, "lijnen");
            for (var i = 0; i < count; i++)
            {
                var name = reader.ReadString();
                var loop = reader.ReadBool();
                var vehicles = reader.ReadInt32();
                var headway = reader.ReadSingle();
                var stopCount = Count(reader, "lijnhaltes");
                var indices = new int[stopCount];
                for (var s = 0; s < stopCount; s++)
                {
                    indices[s] = reader.ReadInt32();
                    if (indices[s] < 0 || indices[s] >= state.TransitStops.Count)
                    {
                        throw reader.Corrupt($"lijn verwijst naar onbekende halte {indices[s]}");
                    }
                }

                state.Lines.Add(new SavedLine(name, indices, loop, vehicles, headway));
            }

            count = Count(reader, "diensten");
            for (var i = 0; i < count; i++)
            {
                var kind = reader.ReadByte();
                if (kind >= CityServices.KindCount)
                {
                    throw reader.Corrupt($"onbekende dienst {kind}");
                }

                state.Services.Add(new SavedService((CityService)kind, new float2(reader.ReadSingle(), reader.ReadSingle())));
            }

            count = Count(reader, "winningen");
            for (var i = 0; i < count; i++)
            {
                var kind = reader.ReadByte();
                if (!Enum.IsDefined(typeof(IndustryKind), (int)kind))
                {
                    throw reader.Corrupt($"onbekende winning {kind}");
                }

                state.Sites.Add(new SavedSite((IndustryKind)kind, reader.ReadInt32(), reader.ReadSingle()));
            }

            count = Count(reader, "trekpleisters");
            for (var i = 0; i < count; i++)
            {
                state.Attractions.Add(new SavedAttraction(reader.ReadString(), reader.ReadInt32(), reader.ReadInt64(), reader.ReadInt64()));
            }

            reader.ExpectEnd();
            return () => _apply(state);
        }

        private static int Count(SaveReader reader, string what)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > MaxItems)
            {
                throw reader.Corrupt($"onmogelijk aantal {what}: {count}");
            }

            return count;
        }
    }
}
