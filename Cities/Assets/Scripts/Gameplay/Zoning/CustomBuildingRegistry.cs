using System.Collections.Generic;
using Cities.World.Buildings;

namespace Cities.Gameplay.Zoning
{
    /// <summary>
    /// Eigen gebouwen die tijdens het spelen zijn geladen (E21-08). De groeiregels nemen ze mee naast de definities uit de catalogus;
    /// laden gebeurt in <c>CustomBuildingsBehaviour</c> vóórdat de bouwer start.
    /// </summary>
    public static class CustomBuildingRegistry
    {
        private static readonly List<BuildingDefinition> Loaded = new List<BuildingDefinition>();

        public static IReadOnlyList<BuildingDefinition> Definitions => Loaded;

        public static void Replace(IEnumerable<BuildingDefinition> definitions)
        {
            Loaded.Clear();
            Loaded.AddRange(definitions);
        }
    }
}
