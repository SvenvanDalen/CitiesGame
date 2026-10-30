namespace Cities.Core.Ids
{
    /// <summary>Markeert een soort object dat een eigen ID-reeks heeft; zie <see cref="EntityId{TKind}"/>.</summary>
    public interface IEntityKind
    {
    }

    /// <summary>Gebouwen, zowel gegroeid als handmatig geplaatst.</summary>
    public struct BuildingKind : IEntityKind
    {
    }

    /// <summary>Knooppunten van het wegennet (kruispunten, eindpunten, splitsingen).</summary>
    public struct NodeKind : IEntityKind
    {
    }

    /// <summary>Wegsegmenten tussen twee knooppunten.</summary>
    public struct SegmentKind : IEntityKind
    {
    }

    /// <summary>Rijstroken, fietspaden en stoepen op een wegsegment.</summary>
    public struct LaneKind : IEntityKind
    {
    }

    public struct CitizenKind : IEntityKind
    {
    }

    public struct VehicleKind : IEntityKind
    {
    }
}
