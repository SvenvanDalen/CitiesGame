using System;
using Cities.Core.Ids;
using Unity.Mathematics;

namespace Cities.World.Picking
{
    /// <summary>
    /// Wat er geraakt kan worden. De waarde is ook de prioriteit: liggen meerdere zichtbare objecten onder de cursor,
    /// dan wint de hoogste (een burger op de stoep gaat voor de weg, de weg voor het gebouw erachter).
    /// </summary>
    public enum PickKind : byte
    {
        None = 0,
        Terrain = 1,
        Building = 2,
        Segment = 3,
        Node = 4,
        Vehicle = 5,
        Citizen = 6,
    }

    /// <summary>Welke soorten een tool wil kunnen aanwijzen.</summary>
    [Flags]
    public enum PickKinds
    {
        None = 0,
        Terrain = 1 << (int)PickKind.Terrain,
        Building = 1 << (int)PickKind.Building,
        Segment = 1 << (int)PickKind.Segment,
        Node = 1 << (int)PickKind.Node,
        Vehicle = 1 << (int)PickKind.Vehicle,
        Citizen = 1 << (int)PickKind.Citizen,
        Network = Segment | Node,
        Agents = Vehicle | Citizen,
        All = Terrain | Building | Network | Agents,
    }

    public static class PickKindsExtensions
    {
        public static PickKinds Of(PickKind kind) => kind == PickKind.None ? PickKinds.None : (PickKinds)(1 << (int)kind);

        public static bool Includes(this PickKinds mask, PickKind kind) => kind != PickKind.None && (mask & Of(kind)) != 0;
    }

    /// <summary>Straal vanuit de camera in wereldcoördinaten.</summary>
    public readonly struct PickRay
    {
        public PickRay(float3 origin, float3 direction, float length = 50_000f)
        {
            var lengthSq = math.lengthsq(direction);
            if (!(lengthSq > 1e-12f) || float.IsInfinity(lengthSq))
            {
                throw new ArgumentException("De richting van een pickstraal mag niet nul of ongeldig zijn.", nameof(direction));
            }

            if (!(length > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(length), length, "De lengte moet groter dan 0 zijn.");
            }

            Origin = origin;
            Direction = direction * math.rsqrt(lengthSq);
            Length = length;
        }

        public float3 Origin { get; }

        /// <summary>Genormaliseerd.</summary>
        public float3 Direction { get; }

        public float Length { get; }

        public float3 Point(float distance) => Origin + (Direction * distance);
    }

    /// <summary>Eén treffer: soort, object en het punt op het object.</summary>
    public readonly struct PickHit
    {
        public static readonly PickHit None = default;

        public PickHit(PickKind kind, ulong id, float3 point, float distance)
        {
            Kind = kind;
            Id = id;
            Point = point;
            Distance = distance;
        }

        public PickKind Kind { get; }

        /// <summary><see cref="EntityId{TKind}.Packed"/> van het object; 0 voor terrein.</summary>
        public ulong Id { get; }

        public float3 Point { get; }

        /// <summary>Afstand langs de straal.</summary>
        public float Distance { get; }

        public bool IsHit => Kind != PickKind.None;

        public EntityId<TKind> As<TKind>() where TKind : struct, IEntityKind => EntityId<TKind>.FromPacked(Id);

        public override string ToString() => IsHit ? $"{Kind} {Id} op {Distance:0.0} m" : "Niets";
    }

    /// <summary>Uitkomst van <see cref="PickingService.Pick"/>: het gekozen object en het terreinpunt onder de cursor.</summary>
    public readonly struct PickResult
    {
        public PickResult(PickHit hit, PickHit ground)
        {
            Hit = hit;
            Ground = ground;
        }

        /// <summary>Het object met de hoogste prioriteit dat zichtbaar onder de cursor ligt.</summary>
        public PickHit Hit { get; }

        /// <summary>Het terrein (of water) onder de cursor, ook als er een object is geraakt; tools snappen hiermee.</summary>
        public PickHit Ground { get; }

        public bool HasGround => Ground.IsHit;
    }

    /// <summary>Eén soort pickbare objecten.</summary>
    public interface IPickLayer
    {
        PickKind Kind { get; }

        /// <summary>Of objecten in deze laag wat erachter ligt verbergen (terrein, gebouwen); wegen en agents niet.</summary>
        bool IsSolid { get; }

        /// <summary>De beste treffer binnen <paramref name="maxDistance"/>.</summary>
        bool Raycast(in PickRay ray, float maxDistance, out PickHit hit);
    }
}
