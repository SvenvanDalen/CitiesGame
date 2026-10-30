using Cities.Core.Spatial;

namespace Cities.World.Picking
{
    /// <summary>Gebouwen als gedraaide dozen; massief, dus wat erachter ligt is niet aan te wijzen.</summary>
    public sealed class BoxPickLayer : SpatialPickLayer<PickBox>
    {
        public BoxPickLayer(GridGeometry geometry, int initialCapacity = 1024, PickKind kind = PickKind.Building)
            : base(kind, geometry, initialCapacity, 0f, 0f)
        {
        }

        public override bool IsSolid => true;
    }

    /// <summary>
    /// Wegsegmenten (als capsules langs de spline) of knooppunten. Ver weg groeit de pickstraal, zodat een smalle weg
    /// aan te wijzen blijft.
    /// </summary>
    public sealed class CapsulePickLayer : SpatialPickLayer<PickCapsule>
    {
        public CapsulePickLayer(PickKind kind, GridGeometry geometry, int initialCapacity = 1024, float margin = 0.003f, float maxMargin = 4f)
            : base(kind, geometry, initialCapacity, margin, maxMargin)
        {
        }

        public override bool IsSolid => false;
    }

    /// <summary>Burgers of voertuigen als staande capsules; het verkeerssysteem werkt hun posities elke tick bij.</summary>
    public sealed class AgentPickLayer : SpatialPickLayer<PickAgent>
    {
        public AgentPickLayer(PickKind kind, GridGeometry geometry, int initialCapacity = 4096, float margin = 0.004f, float maxMargin = 3f)
            : base(kind, geometry, initialCapacity, margin, maxMargin)
        {
        }

        public override bool IsSolid => false;
    }
}
