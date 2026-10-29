using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cities.Core.Configuration
{
    /// <summary>
    /// Lijst van alle definitie-assets in het project. De editor houdt deze lijst automatisch bij
    /// (<c>Cities.Editor.Configuration.DefinitionCatalogBuilder</c>); in het spel wordt er een
    /// <see cref="DefinitionDatabase"/> van gemaakt.
    /// </summary>
    [CreateAssetMenu(fileName = "DefinitionCatalog", menuName = "Cities/Definition catalog")]
    public sealed class DefinitionCatalog : ScriptableObject
    {
        [SerializeField] private List<GameDefinition> _definitions = new List<GameDefinition>();

        public IReadOnlyList<GameDefinition> Definitions => _definitions;

        /// <summary>Vervangt de lijst, gesorteerd op ID; alleen voor de editor-tooling.</summary>
        public void SetDefinitions(IEnumerable<GameDefinition> definitions)
        {
            _definitions = definitions.Where(d => d != null).OrderBy(d => d.Id, StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// Bouwt de database. Met <paramref name="overrides"/> (ID → JSON) wordt per definitie een kopie gemaakt waarop
        /// de JSON wordt toegepast; de assets zelf blijven ongewijzigd. Problemen met overrides komen in
        /// <paramref name="overrideErrors"/>, problemen met waarden in <see cref="DefinitionDatabase.Errors"/>.
        /// </summary>
        public DefinitionDatabase CreateDatabase(IReadOnlyDictionary<string, string> overrides = null, ICollection<string> overrideErrors = null)
        {
            if (overrides == null || overrides.Count == 0)
            {
                return new DefinitionDatabase(_definitions);
            }

            var errors = overrideErrors ?? new List<string>();
            var used = new HashSet<string>(StringComparer.Ordinal);
            var definitions = new List<IDefinition>(_definitions.Count);
            foreach (var definition in _definitions)
            {
                if (definition == null || !overrides.TryGetValue(definition.Id, out var json))
                {
                    definitions.Add(definition);
                    continue;
                }

                used.Add(definition.Id);
                definitions.Add(ApplyOverride(definition, json, errors));
            }

            foreach (var id in overrides.Keys.Where(id => !used.Contains(id)).OrderBy(id => id, StringComparer.Ordinal))
            {
                errors.Add($"Override '{id}': er bestaat geen definitie met dit ID.");
            }

            return new DefinitionDatabase(definitions);
        }

        private static GameDefinition ApplyOverride(GameDefinition definition, string json, ICollection<string> errors)
        {
            var copy = Instantiate(definition);
            copy.name = definition.name;
            try
            {
                JsonUtility.FromJsonOverwrite(json, copy);
            }
            catch (ArgumentException exception)
            {
                errors.Add($"Override '{definition.Id}': ongeldige JSON ({exception.Message}).");
                if (Application.isPlaying)
                {
                    Destroy(copy);
                }
                else
                {
                    DestroyImmediate(copy);
                }

                return definition;
            }

            if (copy.Id != definition.Id)
            {
                errors.Add($"Override '{definition.Id}': het ID kan niet via een override worden gewijzigd.");
                copy.RestoreId(definition.Id);
            }

            return copy;
        }
    }
}
