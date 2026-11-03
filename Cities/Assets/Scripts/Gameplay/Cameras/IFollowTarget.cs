using Unity.Mathematics;

namespace Cities.Gameplay.Cameras
{
    /// <summary>Iets wat de camera kan volgen, zoals een burger of voertuig. Het contract is bewust klein en los van Unity.</summary>
    public interface IFollowTarget
    {
        /// <summary>Valt weg als het doel verdwijnt (burger thuis, voertuig verwijderd); de camera stopt dan met volgen.</summary>
        bool IsValid { get; }

        /// <summary>Positie op de grond in meters.</summary>
        float3 Position { get; }

        /// <summary>Kijk- en rijrichting in graden, 0 = +Z (noord), met de klok mee.</summary>
        float Yaw { get; }

        /// <summary>Ooghoogte boven de grond in meters (burger ca. 1,6; bus ca. 2,5).</summary>
        float EyeHeight { get; }
    }
}
