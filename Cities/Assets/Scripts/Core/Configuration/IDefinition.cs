using System.Collections.Generic;

namespace Cities.Core.Configuration
{
    /// <summary>
    /// Een stuk speldata (gebouwtype, voertuigtype, beleid) dat zonder code te wijzigen is.
    /// Wordt in de editor als ScriptableObject bewerkt (<see cref="GameDefinition"/>).
    /// </summary>
    public interface IDefinition
    {
        /// <summary>Stabiele sleutel, bijvoorbeeld <c>vehicle.city-bus</c>; zie <see cref="DefinitionId"/>.</summary>
        string Id { get; }

        /// <summary>Voegt een melding toe voor elke ongeldige waarde; voegt niets toe als alles klopt.</summary>
        void Validate(ICollection<string> errors);
    }
}
