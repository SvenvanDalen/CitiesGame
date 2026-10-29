using System.Collections.Generic;
using UnityEngine;

namespace Cities.Core.Configuration
{
    /// <summary>
    /// Basis voor alle definities die in de editor als asset worden bewerkt. De waarden zelf staan in een
    /// serialiseerbare spec-struct in de afgeleide klasse, zodat validatie en berekeningen zonder Unity te testen zijn.
    /// </summary>
    public abstract class GameDefinition : ScriptableObject, IDefinition
    {
        [SerializeField] private string _id = string.Empty;
        [SerializeField] private string _displayName = string.Empty;
        [SerializeField, TextArea(2, 6)] private string _description = string.Empty;
        [SerializeField] private Sprite _icon;

        public string Id => _id;

        public string DisplayName => _displayName;

        public string Description => _description;

        public Sprite Icon => _icon;

        /// <summary>Verwacht eerste deel van het ID, bijvoorbeeld <c>vehicle</c>.</summary>
        protected abstract string Kind { get; }

        public void Validate(ICollection<string> errors)
        {
            if (DefinitionId.KindOf(_id) != Kind)
            {
                errors.Add($"ID moet beginnen met '{Kind}.'.");
            }

            if (string.IsNullOrWhiteSpace(_displayName))
            {
                errors.Add("Weergavenaam ontbreekt.");
            }

            ValidateSpec(errors);
        }

        /// <summary>Wordt na het laden van JSON-overrides aangeroepen om het oorspronkelijke ID terug te zetten.</summary>
        internal void RestoreId(string id) => _id = id;

        protected abstract void ValidateSpec(ICollection<string> errors);
    }
}
