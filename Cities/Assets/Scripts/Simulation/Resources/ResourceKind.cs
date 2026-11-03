namespace Cities.Simulation.Resources
{
    /// <summary>De delfstoffen en landbouwproducten die een gebouw op een resource-laag kan winnen (E14-01).</summary>
    public enum ResourceKind
    {
        Oil = 0,
        Ore = 1,
        Wood = 2,
        Crops = 3,
    }

    /// <summary>Eigenschappen per soort: olie en erts raken op, hout groeit langzaam terug en gewassen hangen van het seizoen af.</summary>
    public static class ResourceKinds
    {
        public const int Count = 4;

        /// <summary>Aandeel van de capaciteit dat een bos per week terugkomt.</summary>
        public const float WoodRegrowthPerWeek = 0.01f;

        public static bool IsValid(ResourceKind kind) => (int)kind >= 0 && (int)kind < Count;

        /// <summary>Olie en erts komen niet terug.</summary>
        public static bool IsFinite(ResourceKind kind) => kind == ResourceKind.Oil || kind == ResourceKind.Ore;

        /// <summary>De naam van het goed in het <c>GoodsNetwork</c> dat deze grondstof voedt.</summary>
        public static string Good(ResourceKind kind)
        {
            switch (kind)
            {
                case ResourceKind.Oil: return "olie";
                case ResourceKind.Ore: return "erts";
                case ResourceKind.Wood: return "hout";
                default: return "gewassen";
            }
        }
    }
}
