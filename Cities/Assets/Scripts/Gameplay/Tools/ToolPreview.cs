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
        /// <param name="info">Korte uitleg bij de preview (zoals de lengte van een weg), of <c>null</c>.</param>
        /// <param name="cost">Wat de actie kost in hele munteenheden; 0 als ze niets kost.</param>
        public ToolPreview(float3 position, float yawDegrees, float2 size, string invalidReason = null, string info = null, long cost = 0, float rangeMeters = 0f)
        {
            RangeMeters = rangeMeters;
            Info = info;
            Cost = cost;
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

        public string Info { get; }

        public long Cost { get; }

        /// <summary>Bereik in meters dat als cirkel om de plek getoond wordt (dekking van een dienst of centrale); 0 toont niets.</summary>
        public float RangeMeters { get; }

        /// <summary>Dezelfde preview met een bereikcirkel.</summary>
        public ToolPreview WithRange(float meters) => new ToolPreview(Position, YawDegrees, Size, InvalidReason, Info, Cost, meters);

        /// <summary>Dezelfde preview met een andere reden waarom het niet mag (bijvoorbeeld te weinig geld).</summary>
        public ToolPreview WithReason(string reason) => new ToolPreview(Position, YawDegrees, Size, reason, Info, Cost, RangeMeters);

        public bool IsValid => IsVisible && InvalidReason == null;
    }
}
