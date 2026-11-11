using Unity.Mathematics;

namespace Cities.Gameplay.Expansion
{
    /// <summary>Rekent de bladkleur van een seizoen uit: groen in de zomer, oranje in de herfst, kaal bruin in de winter.</summary>
    public static class SeasonTint
    {
        public static readonly float3 Autumn = new float3(0.85f, 0.45f, 0.1f);

        public static readonly float3 Bare = new float3(0.35f, 0.3f, 0.25f);

        /// <summary>De kleur uit de basiskleur, de volheid van het blad (0 kaal, 1 vol) en de herfstkleur (0 groen, 1 oranje).</summary>
        public static float3 Leaf(float3 summer, float foliage, float autumn)
        {
            var colored = math.lerp(summer, Autumn, math.saturate(autumn));
            return math.lerp(Bare, colored, math.saturate(foliage));
        }
    }
}
