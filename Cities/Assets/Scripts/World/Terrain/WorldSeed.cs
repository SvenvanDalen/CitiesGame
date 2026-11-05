namespace Cities.World.Terrain
{
    /// <summary>Leidt de seeds van terrein en bossen af van de ene wereldseed (E26-01): dezelfde wereldseed geeft dezelfde kaart.</summary>
    public static class WorldSeed
    {
        public static int Terrain(ulong worldSeed) => (int)(Mix(worldSeed, 0x9E3779B97F4A7C15UL) & 0x7FFFFFFF);

        public static uint Forest(ulong worldSeed) => (uint)(Mix(worldSeed, 0xC2B2AE3D27D4EB4FUL) >> 32);

        private static ulong Mix(ulong value, ulong salt)
        {
            unchecked
            {
                var x = value + salt;
                x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
                x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
                return x ^ (x >> 31);
            }
        }
    }
}
