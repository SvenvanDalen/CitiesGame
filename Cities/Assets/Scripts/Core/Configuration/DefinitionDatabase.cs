using System;
using System.Collections.Generic;
using System.Linq;

namespace Cities.Core.Configuration
{
    /// <summary>
    /// Alle definities van een spelsessie, opzoekbaar op type en ID. Controleert bij het opbouwen op dubbele en ongeldige
    /// ID's en op ongeldige waarden, en verzamelt die als meldingen in plaats van halverwege te stoppen.
    /// </summary>
    /// <remarks>
    /// Per type is de volgorde op ID gesorteerd (ordinaal). Daardoor is <see cref="IndexOf{T}"/> stabiel zolang de set
    /// definities gelijk blijft, en bruikbaar als index in tabellen voor Burst-jobs.
    /// </remarks>
    public sealed class DefinitionDatabase
    {
        private readonly Dictionary<string, IDefinition> _byId = new Dictionary<string, IDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<Type, object> _byType = new Dictionary<Type, object>();
        private readonly List<string> _errors = new List<string>();

        public DefinitionDatabase(IEnumerable<IDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            foreach (var definition in definitions)
            {
                Add(definition);
            }
        }

        public bool IsValid => _errors.Count == 0;

        /// <summary>Alle gevonden problemen, met het ID ervoor.</summary>
        public IReadOnlyList<string> Errors => _errors;

        public int Count => _byId.Count;

        public T Get<T>(string id) where T : class, IDefinition
        {
            if (TryGet<T>(id, out var definition))
            {
                return definition;
            }

            throw new KeyNotFoundException(_byId.TryGetValue(id ?? string.Empty, out var other)
                ? $"Definitie '{id}' is een {other.GetType().Name}, geen {typeof(T).Name}."
                : $"Geen definitie met ID '{id}'.");
        }

        public bool TryGet<T>(string id, out T definition) where T : class, IDefinition
        {
            definition = id != null && _byId.TryGetValue(id, out var found) ? found as T : null;
            return definition != null;
        }

        /// <summary>Alle definities van type <typeparamref name="T"/> (inclusief subtypes), gesorteerd op ID.</summary>
        public IReadOnlyList<T> All<T>() where T : class, IDefinition
        {
            if (_byType.TryGetValue(typeof(T), out var cached))
            {
                return (IReadOnlyList<T>)cached;
            }

            var list = _byId.Values.OfType<T>().OrderBy(d => d.Id, StringComparer.Ordinal).ToList();
            _byType[typeof(T)] = list;
            return list;
        }

        /// <summary>Positie van een definitie binnen <see cref="All{T}"/>, of -1 als die er niet is.</summary>
        public int IndexOf<T>(string id) where T : class, IDefinition
        {
            if (!TryGet<T>(id, out var definition))
            {
                return -1;
            }

            var all = All<T>();
            for (var i = 0; i < all.Count; i++)
            {
                if (ReferenceEquals(all[i], definition))
                {
                    return i;
                }
            }

            return -1;
        }

        private void Add(IDefinition definition)
        {
            if (definition == null)
            {
                _errors.Add("Lege verwijzing in de lijst met definities.");
                return;
            }

            var id = definition.Id;
            if (!DefinitionId.IsValid(id))
            {
                _errors.Add($"'{id}' ({definition.GetType().Name}): ongeldig ID; gebruik bijvoorbeeld 'vehicle.city-bus'.");
                return;
            }

            if (_byId.TryGetValue(id, out var existing))
            {
                _errors.Add($"'{id}': dubbel ID ({existing.GetType().Name} en {definition.GetType().Name}).");
                return;
            }

            _byId.Add(id, definition);
            var problems = new List<string>();
            definition.Validate(problems);
            foreach (var problem in problems)
            {
                _errors.Add($"'{id}': {problem}");
            }
        }
    }
}
