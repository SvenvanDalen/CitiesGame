using System.IO;
using UnityEngine;

namespace Cities.Editor.Models
{
    /// <summary>
    /// De gedeelde materialenset van de gegroeide gebouwen (E05-08): baksteen in twee kleuren, stucwerk, betonpanelen, golfplaat,
    /// daken, glas en afwerking. Alle gebouwen delen deze materialen, zodat ze samen in één batch tekenen. De volgorde is die van
    /// <see cref="Slots"/>, dus ook van de submeshes van <see cref="BuildingBuilder"/>.
    /// </summary>
    internal static class BuildingMaterials
    {
        public const string Folder = "Assets/Art/Buildings/Shared";

        public static class Slots
        {
            public const int BrickRed = 0;
            public const int BrickYellow = 1;
            public const int Render = 2;
            public const int Concrete = 3;
            public const int Cladding = 4;
            public const int RoofTiles = 5;
            public const int RoofFlat = 6;
            public const int Trim = 7;
            public const int Glass = 8;
            public const int Door = 9;
            public const int Stone = 10;
            public const int Zinc = 11;
            public const int Paving = 12;
            public const int Plinth = 13;
            public const int AccentRed = 14;
            public const int AccentBlue = 15;
            public const int AccentGreen = 16;
            public const int Asphalt = 17;
            public const int Curtain = 18;
            public const int Hedge = 19;
            public const int Count = 20;
        }

        public static Material[] Create()
        {
            var textures = $"{Folder}/Textures";
            Directory.CreateDirectory(textures);
            var brickRed = ModelMaterials.SaveTexture($"{textures}/BrickRed.png", ModelMaterials.Bricks(new Color(0.5f, 0.22f, 0.15f), new Color(0.72f, 0.69f, 0.64f), 11));
            var brickYellow = ModelMaterials.SaveTexture($"{textures}/BrickYellow.png", ModelMaterials.Bricks(new Color(0.76f, 0.63f, 0.37f), new Color(0.74f, 0.72f, 0.67f), 17));
            var plinth = ModelMaterials.SaveTexture($"{textures}/PlinthBrick.png", ModelMaterials.Bricks(new Color(0.2f, 0.19f, 0.18f), new Color(0.46f, 0.45f, 0.43f), 23));
            var roof = ModelMaterials.SaveTexture($"{textures}/RoofTiles.png", ModelMaterials.RoofTiles(new Color(0.2f, 0.21f, 0.22f), 37));
            var paving = ModelMaterials.SaveTexture($"{textures}/Paving.png", ModelMaterials.Paving(new Color(0.6f, 0.59f, 0.56f), 41));
            var hedge = ModelMaterials.SaveTexture($"{textures}/Hedge.png", ModelMaterials.Foliage(new Color(0.16f, 0.3f, 0.12f), 53));
            var render = ModelMaterials.SaveTexture($"{textures}/Render.png", StuccoTexture(new Color(0.9f, 0.88f, 0.82f), 61));
            var concrete = ModelMaterials.SaveTexture($"{textures}/ConcretePanels.png", ConcretePanels(new Color(0.62f, 0.62f, 0.6f), 67));
            var cladding = ModelMaterials.SaveTexture($"{textures}/Cladding.png", CladdingTexture(new Color(0.58f, 0.6f, 0.62f), 71));
            var gravel = ModelMaterials.SaveTexture($"{textures}/Gravel.png", Gravel(new Color(0.34f, 0.34f, 0.33f), 73));
            var asphalt = ModelMaterials.SaveTexture($"{textures}/Asphalt.png", Gravel(new Color(0.18f, 0.18f, 0.19f), 79));
            var one = Vector2.one;
            return new[]
            {
                ModelMaterials.Lit(Folder, "BrickRed", brickRed, Color.white, 0.12f, 0f, new Vector2(1f / 0.88f, 1f)),
                ModelMaterials.Lit(Folder, "BrickYellow", brickYellow, Color.white, 0.12f, 0f, new Vector2(1f / 0.88f, 1f)),
                ModelMaterials.Lit(Folder, "Render", render, Color.white, 0.1f, 0f, new Vector2(0.5f, 0.5f)),
                ModelMaterials.Lit(Folder, "Concrete", concrete, Color.white, 0.2f, 0f, new Vector2(1f / 3f, 1f / 3f)),
                ModelMaterials.Lit(Folder, "Cladding", cladding, Color.white, 0.4f, 0.5f, new Vector2(1f / 0.64f, 0.25f)),
                ModelMaterials.Lit(Folder, "RoofTiles", roof, Color.white, 0.3f, 0f, new Vector2(1f / 0.9f, 1f / 1.02f)),
                ModelMaterials.Lit(Folder, "RoofFlat", gravel, Color.white, 0.05f, 0f, new Vector2(0.5f, 0.5f)),
                ModelMaterials.Lit(Folder, "Trim", null, new Color(0.93f, 0.93f, 0.9f), 0.45f, 0f, one),
                ModelMaterials.Lit(Folder, "Glass", null, new Color(0.11f, 0.15f, 0.18f), 0.95f, 0.2f, one),
                ModelMaterials.Lit(Folder, "Door", null, new Color(0.1f, 0.14f, 0.2f), 0.5f, 0f, one),
                ModelMaterials.Lit(Folder, "Stone", null, new Color(0.56f, 0.56f, 0.55f), 0.25f, 0f, one),
                ModelMaterials.Lit(Folder, "Zinc", null, new Color(0.47f, 0.49f, 0.5f), 0.55f, 0.75f, one),
                ModelMaterials.Lit(Folder, "Paving", paving, Color.white, 0.18f, 0f, new Vector2(1f / 0.9f, 1f / 0.9f)),
                ModelMaterials.Lit(Folder, "Plinth", plinth, Color.white, 0.15f, 0f, new Vector2(1f / 0.88f, 1f)),
                ModelMaterials.Lit(Folder, "AccentRed", null, new Color(0.7f, 0.1f, 0.1f), 0.4f, 0f, one),
                ModelMaterials.Lit(Folder, "AccentBlue", null, new Color(0.1f, 0.25f, 0.55f), 0.4f, 0f, one),
                ModelMaterials.Lit(Folder, "AccentGreen", null, new Color(0.1f, 0.4f, 0.2f), 0.4f, 0f, one),
                ModelMaterials.Lit(Folder, "Asphalt", asphalt, Color.white, 0.08f, 0f, new Vector2(0.5f, 0.5f)),
                ModelMaterials.Lit(Folder, "Curtain", null, new Color(0.18f, 0.3f, 0.4f), 0.97f, 0.3f, one),
                ModelMaterials.Lit(Folder, "Hedge", hedge, Color.white, 0.05f, 0f, new Vector2(0.5f, 0.5f)),
            };
        }

        /// <summary>Spuitpleister met fijne korrel en een paar vlekken.</summary>
        private static Color[] StuccoTexture(Color tone, int seed)
        {
            var size = ModelMaterials.Size;
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var stain = Mathf.PerlinNoise((x * 0.012f) + seed, (y * 0.012f) + seed);
                    pixels[(y * size) + x] = ModelMaterials.Shade(tone, 0.9f + (stain * 0.12f) + (ModelMaterials.Noise(x, y, seed) * 0.035f));
                }
            }

            return pixels;
        }

        /// <summary>Prefab-betonpanelen van 3 × 3 m met een voeg en lichte vlekken.</summary>
        private static Color[] ConcretePanels(Color tone, int seed)
        {
            var size = ModelMaterials.Size;
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var joint = x < 3 || y < 3;
                    var grain = ModelMaterials.Noise(x, y, seed) * 0.045f;
                    var stain = Mathf.PerlinNoise((x * 0.02f) + seed, (y * 0.02f) + seed) * 0.12f;
                    pixels[(y * size) + x] = ModelMaterials.Shade(tone, joint ? 0.55f : 0.96f + grain - stain);
                }
            }

            return pixels;
        }

        /// <summary>Verticale golfplaat: 8 golven over 64 cm, met lichte strepen van oxidatie.</summary>
        private static Color[] CladdingTexture(Color tone, int seed)
        {
            var size = ModelMaterials.Size;
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var wave = 0.78f + (0.22f * Mathf.Cos(x * 8f / size * Mathf.PI * 2f));
                    var streak = Mathf.PerlinNoise((x * 0.15f) + seed, y * 0.01f) * 0.15f;
                    pixels[(y * size) + x] = ModelMaterials.Shade(tone, wave + streak - 0.05f);
                }
            }

            return pixels;
        }

        /// <summary>Grind of asfalt: grove korrel zonder patroon.</summary>
        private static Color[] Gravel(Color tone, int seed)
        {
            var size = ModelMaterials.Size;
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var coarse = Mathf.PerlinNoise((x * 0.08f) + seed, (y * 0.08f) + seed);
                    pixels[(y * size) + x] = ModelMaterials.Shade(tone, 0.75f + (coarse * 0.35f) + (ModelMaterials.Noise(x, y, seed) * 0.12f));
                }
            }

            return pixels;
        }
    }
}
