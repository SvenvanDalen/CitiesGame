namespace Cities.Gameplay.Zoning
{
    /// <summary>De regels voor welke cellen gezoned mogen worden, los van het wegennet zodat ze apart te testen zijn.</summary>
    public static class ZoneRules
    {
        /// <summary>Hoeveel cellen diep een zone langs een weg mag liggen.</summary>
        public const int MaxDepthCells = 4;

        /// <summary>
        /// Waarom een cel niet gezoned mag worden, of <c>null</c>. <paramref name="edgeDistance"/> is de afstand in meters van
        /// het midden van de cel tot de rand van het dichtstbijzijnde wegdek (negatief als het midden op het wegdek ligt);
        /// <see cref="float.PositiveInfinity"/> zonder weg.
        /// </summary>
        public static string Reason(float edgeDistance, float cellSize, bool underwater)
        {
            if (underwater)
            {
                return "Onder water";
            }

            // Een kleine marge: een rij die precies langs de weg ligt heeft zijn midden een halve cel van de rand.
            if (edgeDistance < (cellSize * 0.5f) - 0.01f)
            {
                return "Op of te dicht bij de weg";
            }

            return edgeDistance > MaxDepthCells * cellSize ? "Te ver van een weg" : null;
        }
    }
}
