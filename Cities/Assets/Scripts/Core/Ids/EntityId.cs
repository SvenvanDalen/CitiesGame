using System;

namespace Cities.Core.Ids
{
    /// <summary>
    /// Stabiele verwijzing naar een object: een plek (<see cref="Index"/>) plus een generatienummer.
    /// Komt een plek na verwijderen opnieuw in gebruik, dan stijgt de generatie, zodat oude verwijzingen
    /// herkenbaar ongeldig worden in plaats van stilletjes naar het nieuwe object te wijzen.
    /// </summary>
    /// <typeparam name="TKind">Soort object, zodat bijvoorbeeld een gebouw-ID niet als voertuig-ID te gebruiken is.</typeparam>
    /// <remarks>Generatie 0 betekent "geen object"; <c>default</c> is dus altijd ongeldig.</remarks>
    public readonly struct EntityId<TKind> : IEquatable<EntityId<TKind>>, IComparable<EntityId<TKind>>
        where TKind : struct, IEntityKind
    {
        public static readonly EntityId<TKind> None = default;

        public EntityId(int index, uint generation)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index kan niet negatief zijn.");
            }

            Index = index;
            Generation = generation;
        }

        public int Index { get; }

        public uint Generation { get; }

        public bool IsValid => Generation != 0;

        /// <summary>Index en generatie in één getal, voor opslaan en als sleutel in hashmaps.</summary>
        public ulong Packed => ((ulong)Generation << 32) | (uint)Index;

        public static EntityId<TKind> FromPacked(ulong packed)
        {
            var index = (int)(uint)packed;
            return index < 0 ? throw new ArgumentOutOfRangeException(nameof(packed), packed, "Ongeldige index.")
                : new EntityId<TKind>(index, (uint)(packed >> 32));
        }

        public bool Equals(EntityId<TKind> other) => Index == other.Index && Generation == other.Generation;

        public override bool Equals(object obj) => obj is EntityId<TKind> other && Equals(other);

        public override int GetHashCode() => Packed.GetHashCode();

        /// <summary>Ordent op index en daarna generatie; geeft een deterministische volgorde voor verwerking en opslag.</summary>
        public int CompareTo(EntityId<TKind> other)
        {
            var byIndex = Index.CompareTo(other.Index);
            return byIndex != 0 ? byIndex : Generation.CompareTo(other.Generation);
        }

        public override string ToString() =>
            IsValid ? $"{typeof(TKind).Name.Replace("Kind", string.Empty)}#{Index}v{Generation}" : "None";

        public static bool operator ==(EntityId<TKind> left, EntityId<TKind> right) => left.Equals(right);

        public static bool operator !=(EntityId<TKind> left, EntityId<TKind> right) => !left.Equals(right);

        public static bool operator <(EntityId<TKind> left, EntityId<TKind> right) => left.CompareTo(right) < 0;

        public static bool operator >(EntityId<TKind> left, EntityId<TKind> right) => left.CompareTo(right) > 0;

        public static bool operator <=(EntityId<TKind> left, EntityId<TKind> right) => left.CompareTo(right) <= 0;

        public static bool operator >=(EntityId<TKind> left, EntityId<TKind> right) => left.CompareTo(right) >= 0;
    }
}
