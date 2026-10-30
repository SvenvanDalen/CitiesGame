using Unity.Mathematics;

namespace Cities.Gameplay.Tools
{
    /// <summary>Wat de actieve tool zou doen op de plek van de muis: de ghost en of dat mag.</summary>
    public readonly struct ToolPreview
    {
        public static readonly ToolPreview Hidden = default;

        /// <param name="position">Midden van de onderkant in de wereld.</param>
        /// <param name="yawDegrees">Draaiing om de Y-as.</param>
        /// <param name="size">Breedte (lokaal X) en diepte (lokaal Z) in meters.</param>
        /// <param name="invalidReason">Waarom het niet mag, of <c>null</c> als het mag.</param>
        public ToolPreview(float3 position, float yawDegrees, float2 size, string invalidReason = null)
        {
            IsVisible = true;
            Position = position;
            YawDegrees = yawDegrees;
            Size = size;
            InvalidReason = invalidReason;
        }

        public bool IsVisible { get; }

        public float3 Position { get; }

        public float YawDegrees { get; }

        public float2 Size { get; }

        public string InvalidReason { get; }

        public bool IsValid => IsVisible && InvalidReason == null;
    }
}
