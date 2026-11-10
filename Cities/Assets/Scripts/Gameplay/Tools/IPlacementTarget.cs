using Unity.Mathematics;

namespace Cities.Gameplay.Tools
{
    /// <summary>Waar een <see cref="PlacementTool"/> objecten neerzet, en wat daar al staat.</summary>
    public interface IPlacementTarget
    {
        /// <summary>Waarom de footprint niet kan, of <c>null</c> als hij vrij is.</summary>
        string Check(in Footprint footprint);

        /// <param name="footprint">De cellen die het object inneemt.</param>
        /// <param name="position">Midden van de onderkant in de wereld.</param>
        void Place(in Footprint footprint, float3 position);
    }

    /// <summary>Een plaatsingsdoel dat een geplaatst object ook weer kan weghalen (E01-12), zodat plaatsen ongedaan te maken is.</summary>
    public interface IUndoablePlacementTarget : IPlacementTarget
    {
        /// <summary>Haalt het object weg dat met deze footprint is geplaatst en maakt de cellen weer vrij.</summary>
        void Unplace(in Footprint footprint, float3 position);
    }
}
