using System;
using System.Collections.Generic;
using Cities.Core.Randomness;
using Unity.Mathematics;

namespace Cities.World.Fauna
{
    /// <summary>
    /// Bepaalt welke groepen er in een vak van de kaart wonen. Het vak en de wereldseed leggen de groepen vast; de
    /// habitat beslist of ze er mogen staan. Zo is de fauna overal hetzelfde bij dezelfde kaart zonder iets op te slaan.
    /// </summary>
    public static class FaunaGroups
    {
        /// <summary>Zijde van een vak in meters (32 cellen).</summary>
        public const float PatchSize = 256f;

        /// <summary>Een school heeft minstens zoveel water nodig om in te zwemmen.</summary>
        public const float MinimumFishDepth = 2.5f;

        private const float BirdChance = 0.45f;
        private const float DeerChance = 0.5f;
        private const int FishAttempts = 3;
        private const int DeerAttempts = 2;

        /// <summary>Hoe ver een groep hoogstens van haar anker beweegt, voor het afstandsfilter.</summary>
        public static float Reach(in FaunaGroup group) => group.Radius + 20f;

        /// <summary>Gelijkmatig getal in [0, 1) uit een seed en een zout.</summary>
        public static float Unit(ulong seed, int salt) => (SeedHash.Combine(seed, (ulong)salt) >> 40) / 16777216f;

        /// <summary>Voegt de groepen van vak (<paramref name="patchX"/>, <paramref name="patchZ"/>) toe aan <paramref name="result"/>.</summary>
        public static void Spawn(FaunaKind kind, int patchX, int patchZ, ulong worldSeed, IFaunaHabitat habitat, List<FaunaGroup> result)
        {
            if (habitat == null)
            {
                throw new ArgumentNullException(nameof(habitat));
            }

            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var attempts = 1;
            if (kind == FaunaKind.Fish)
            {
                attempts = FishAttempts;
            }
            else if (kind == FaunaKind.Deer)
            {
                attempts = DeerAttempts;
            }

            for (var attempt = 0; attempt < attempts; attempt++)
            {
                var seed = SeedHash.Combine(
                    SeedHash.Combine(worldSeed, (ulong)kind),
                    unchecked((ulong)(uint)patchX),
                    unchecked(((ulong)(uint)patchZ << 3) + (ulong)attempt));
                var anchor = new float2(patchX + Unit(seed, 1), patchZ + Unit(seed, 2)) * PatchSize;
                if (habitat.InBounds(anchor))
                {
                    TryAdd(kind, seed, anchor, habitat, result);
                }
            }
        }

        private static void TryAdd(FaunaKind kind, ulong seed, float2 anchor, IFaunaHabitat habitat, List<FaunaGroup> result)
        {
            var depth = habitat.WaterDepth(anchor);
            var count = 0;
            switch (kind)
            {
                case FaunaKind.Birds when Unit(seed, 3) < BirdChance:
                    // Meeuwen blijven bij het water, kraaien cirkelen boven het land.
                    count = 5 + (int)(Unit(seed, 4) * 8f);
                    result.Add(new FaunaGroup(kind, seed, anchor, count, new float2(50f + (Unit(seed, 5) * 80f), 8f + (Unit(seed, 6) * 3f)), 35f + (Unit(seed, 7) * 50f), depth > 0.5f));
                    break;
                case FaunaKind.Fish when depth >= MinimumFishDepth:
                    count = 5 + (int)(Unit(seed, 4) * 10f);
                    result.Add(new FaunaGroup(kind, seed, anchor, count, new float2(5f + (Unit(seed, 5) * 12f), 0.4f + (Unit(seed, 6) * 0.5f)), 0f, true));
                    break;
                case FaunaKind.Deer when depth <= 0f && Unit(seed, 3) < DeerChance && habitat.IsOpenLand(anchor):
                    count = 3 + (int)(Unit(seed, 4) * 6f);
                    result.Add(new FaunaGroup(kind, seed, anchor, count, new float2(10f + (Unit(seed, 5) * 14f), 0.15f + (Unit(seed, 6) * 0.15f)), 0f, false));
                    break;
            }
        }
    }
}
