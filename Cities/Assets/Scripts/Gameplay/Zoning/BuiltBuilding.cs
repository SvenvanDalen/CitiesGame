using Cities.Gameplay.Tools;
using Unity.Mathematics;

namespace Cities.Gameplay.Zoning
{
    /// <summary>Een gegroeid gebouw zoals het wordt opgeslagen (E26-14): welke definitie, welke cellen en waar in de wereld.</summary>
    public readonly struct BuiltBuilding
    {
        public BuiltBuilding(string definitionId, Footprint footprint, float3 position)
            : this(definitionId, footprint.Origin.X, footprint.Origin.Z, footprint.Width, footprint.Depth, footprint.QuarterTurns, position.x, position.y, position.z)
        {
        }

        public BuiltBuilding(string definitionId, int originX, int originZ, int width, int depth, int quarterTurns, float x, float y, float z)
        {
            DefinitionId = definitionId ?? string.Empty;
            OriginX = originX;
            OriginZ = originZ;
            Width = width;
            Depth = depth;
            QuarterTurns = quarterTurns;
            X = x;
            Y = y;
            Z = z;
        }

        public string DefinitionId { get; }

        public int OriginX { get; }

        public int OriginZ { get; }

        public int Width { get; }

        public int Depth { get; }

        public int QuarterTurns { get; }

        public float X { get; }

        public float Y { get; }

        public float Z { get; }
    }
}
