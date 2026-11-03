using System;
using System.Collections.Generic;
using System.Linq;

namespace Cities.World.Districts
{
    /// <summary>Een district: nummer (1 tot 255), naam en kleur. Nummer 0 is geen district, dus de hele stad.</summary>
    public readonly struct DistrictInfo : IEquatable<DistrictInfo>
    {
        public DistrictInfo(int id, string name, int rgb)
        {
            Id = id;
            Name = name;
            Rgb = rgb;
        }

        public int Id { get; }

        public string Name { get; }

        /// <summary>Kleur als <c>0xRRGGBB</c>.</summary>
        public int Rgb { get; }

        public bool Equals(DistrictInfo other) => Id == other.Id && Name == other.Name && Rgb == other.Rgb;

        public override bool Equals(object obj) => obj is DistrictInfo other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Id, Name, Rgb);
    }

    /// <summary>
    /// De districten van de stad (E13-01): naam en kleur per nummer. Het nummer is hetzelfde als dat van
    /// <c>PolicyBook</c> (0 is stadsbreed). Een nieuw district krijgt altijd het laagste vrije nummer, zodat een reeks
    /// commando's dezelfde nummers geeft.
    /// </summary>
    public sealed class DistrictBook
    {
        public const int WholeCity = 0;
        public const int MaxDistricts = 255;
        public const int MaxNameLength = 40;
        public const string WholeCityName = "Hele stad";

        private static readonly int[] Palette =
        {
            0xE6194B, 0x3CB44B, 0xFFE119, 0x4363D8, 0xF58231, 0x911EB4,
            0x42D4F4, 0xF032E6, 0xBFEF45, 0xFABED4, 0x469990, 0xDCBEFF,
        };

        private readonly SortedDictionary<int, DistrictInfo> _districts = new SortedDictionary<int, DistrictInfo>();

        /// <summary>Loopt op bij elke wijziging; weergaven kunnen hiermee zien of ze moeten verversen.</summary>
        public long Revision { get; private set; }

        public int Count => _districts.Count;

        /// <summary>Alle districten op nummer.</summary>
        public IEnumerable<DistrictInfo> Districts => _districts.Values;

        public bool Contains(int id) => _districts.ContainsKey(id);

        public bool TryGet(int id, out DistrictInfo info) => _districts.TryGetValue(id, out info);

        /// <summary>De naam van een district; 0 geeft <see cref="WholeCityName"/>.</summary>
        public string NameOf(int id) => id == WholeCity ? WholeCityName : Get(id).Name;

        /// <summary>Maakt een district met het laagste vrije nummer. Zonder kleur kiest het een uit het palet.</summary>
        public int Create(string name, int? rgb = null)
        {
            if (_districts.Count >= MaxDistricts)
            {
                throw new InvalidOperationException($"Er kunnen niet meer dan {MaxDistricts} districten zijn.");
            }

            var cleaned = ValidateName(name, 0);
            var id = 1;
            while (_districts.ContainsKey(id))
            {
                id++;
            }

            var color = rgb ?? Palette[(id - 1) % Palette.Length];
            ValidateColor(color);
            _districts[id] = new DistrictInfo(id, cleaned, color);
            Revision++;
            return id;
        }

        public void Rename(int id, string name)
        {
            var info = Get(id);
            _districts[id] = new DistrictInfo(id, ValidateName(name, id), info.Rgb);
            Revision++;
        }

        public void Recolor(int id, int rgb)
        {
            var info = Get(id);
            ValidateColor(rgb);
            _districts[id] = new DistrictInfo(id, info.Name, rgb);
            Revision++;
        }

        /// <summary>Verwijdert een district uit de lijst. Gebruik <see cref="DistrictMap.RemoveDistrict"/> om ook de kaart te wissen.</summary>
        public bool Remove(int id)
        {
            var removed = _districts.Remove(id);
            if (removed)
            {
                Revision++;
            }

            return removed;
        }

        /// <summary>Controleert een lijst districten (uit een save) zonder iets te veranderen; gooit <see cref="ArgumentException"/> bij een fout.</summary>
        public static void Check(IReadOnlyCollection<DistrictInfo> districts)
        {
            if (districts is null)
            {
                throw new ArgumentNullException(nameof(districts));
            }

            if (districts.Count > MaxDistricts)
            {
                throw new ArgumentException($"{districts.Count} districten is te veel.");
            }

            var ids = new HashSet<int>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in districts)
            {
                if (d.Id < 1 || d.Id > MaxDistricts || !ids.Add(d.Id))
                {
                    throw new ArgumentException($"Ongeldig of dubbel districtnummer {d.Id}.");
                }

                if (!IsValidName(d.Name) || !names.Add(d.Name))
                {
                    throw new ArgumentException($"Ongeldige of dubbele districtnaam '{d.Name}'.");
                }

                ValidateColor(d.Rgb);
            }
        }

        /// <summary>Vervangt de lijst door <paramref name="districts"/>; roep eerst <see cref="Check"/> aan.</summary>
        public void Restore(IEnumerable<DistrictInfo> districts)
        {
            _districts.Clear();
            foreach (var d in districts)
            {
                _districts[d.Id] = d;
            }

            Revision++;
        }

        private static bool IsValidName(string name) =>
            !string.IsNullOrWhiteSpace(name) && name == name.Trim() && name.Length <= MaxNameLength;

        private static void ValidateColor(int rgb)
        {
            if (rgb < 0 || rgb > 0xFFFFFF)
            {
                throw new ArgumentException($"Kleur {rgb} ligt buiten 0x000000 tot 0xFFFFFF.", nameof(rgb));
            }
        }

        private DistrictInfo Get(int id) =>
            _districts.TryGetValue(id, out var info) ? info : throw new ArgumentException($"Onbekend district {id}.", nameof(id));

        private string ValidateName(string name, int ownId)
        {
            var cleaned = name?.Trim();
            if (!IsValidName(cleaned))
            {
                throw new ArgumentException($"Een districtnaam is verplicht en hoogstens {MaxNameLength} tekens.", nameof(name));
            }

            if (_districts.Values.Any(d => d.Id != ownId && string.Equals(d.Name, cleaned, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException($"Er is al een district '{cleaned}'.", nameof(name));
            }

            return cleaned;
        }
    }
}
