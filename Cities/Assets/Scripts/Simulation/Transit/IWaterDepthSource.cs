using Unity.Mathematics;

namespace Cities.Simulation.Transit
{
    /// <summary>
    /// De waterdiepte op een punt van de wereld in meter (E09-08); 0 of minder is land. Houdt het waterverkeer los van de wereldkaart:
    /// de wereld (<c>WorldMap.WaterDepth</c>) levert de diepte, tests leveren een eenvoudige functie.
    /// shortcut: er bestaat nog geen adapter op <c>WorldMap</c>; die volgt zodra de scheepvaart in de scene wordt aangesloten.
    /// </summary>
    public interface IWaterDepthSource
    {
        float DepthAt(float2 position);
    }
}
