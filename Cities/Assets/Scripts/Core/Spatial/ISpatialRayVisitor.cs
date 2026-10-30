namespace Cities.Core.Spatial
{
    /// <summary>Ontvangt de objecten langs een straal uit <see cref="SpatialGrid{T}.Raycast{TVisitor}"/>.</summary>
    public interface ISpatialRayVisitor<T> where T : unmanaged
    {
        /// <summary>Afstand (langs de straal in het grondvlak) waarna geen beter object meer te vinden is.</summary>
        float StopDistance { get; }

        void Visit(in T value, Aabb2 bounds);
    }
}
