using Cities.World.Grid;
using Unity.Mathematics;

namespace Cities.Gameplay.Tools
{
    /// <summary>Wat een <see cref="PlacementTool"/> van het wegennet weet om objecten aan de weg te snappen.</summary>
    public interface IRoadSnap
    {
        /// <summary>Het dichtstbijzijnde punt op een weg (op de hartlijn); <c>false</c> als er geen wegen zijn.</summary>
        bool TryNearestRoad(float3 point, out float3 roadPoint);

        /// <summary>Of de cel op of te dicht bij het wegdek ligt.</summary>
        bool IsOnRoad(CellCoord cell);
    }
}
