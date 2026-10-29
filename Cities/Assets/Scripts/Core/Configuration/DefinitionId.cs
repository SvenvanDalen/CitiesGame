using System.Text.RegularExpressions;

namespace Cities.Core.Configuration
{
    /// <summary>
    /// Regels voor definitie-ID's: kleine letters, cijfers en koppeltekens, in delen gescheiden door punten,
    /// met de soort als eerste deel (<c>vehicle.city-bus</c>, <c>building.residential-low-1</c>).
    /// ID's komen in opslagbestanden terecht en mogen daarom na uitbrengen niet meer veranderen.
    /// </summary>
    public static class DefinitionId
    {
        public const int MaxLength = 64;

        private static readonly Regex Pattern = new Regex(
            "^[a-z][a-z0-9]*(-[a-z0-9]+)*(\\.[a-z0-9]+(-[a-z0-9]+)*)+$",
            RegexOptions.CultureInvariant,
            System.TimeSpan.FromMilliseconds(50));

        public static bool IsValid(string id) =>
            !string.IsNullOrEmpty(id) && id.Length <= MaxLength && Pattern.IsMatch(id);

        /// <summary>Het eerste deel van het ID, bijvoorbeeld <c>vehicle</c>.</summary>
        public static string KindOf(string id)
        {
            if (!IsValid(id))
            {
                return string.Empty;
            }

            return id.Substring(0, id.IndexOf('.'));
        }
    }
}
