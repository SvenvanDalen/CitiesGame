using Unity.Mathematics;

namespace Cities.World.Fauna
{
    /// <summary>
    /// Wat de fauna van de wereld moet weten. Een interface, zodat de bewegingsregels zonder kaart getest kunnen worden;
    /// <see cref="WorldMapHabitat"/> leest de echte kaart.
    /// </summary>
    public interface IFaunaHabitat
    {
        /// <summary>Of de plek op de kaart ligt.</summary>
        bool InBounds(float2 position);

        /// <summary>Terreinhoogte in meters.</summary>
        float GroundHeight(float2 position);

        /// <summary>Waterdiepte in meters boven het terrein, 0 op droog land.</summary>
        float WaterDepth(float2 position);

        /// <summary>Droog, onbebouwd, geen bos en niet te steil: hier grazen herten.</summary>
        bool IsOpenLand(float2 position);
    }
}
