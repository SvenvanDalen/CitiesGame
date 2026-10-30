using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cities.Editor.Models
{
    /// <summary>
    /// Maakt de texturen en URP-materialen voor gebouwmodellen. De texturen zijn procedureel (vaste seed), zodat
    /// opnieuw bouwen dezelfde bestanden geeft. Eén texture-eenheid is één meter, net als de UV's van <see cref="ModelMesh"/>.
    /// </summary>
    internal static class ModelMaterials
    {
        private const int Size = 512;

        /// <summary>Materialen in de volgorde van de submeshes van <see cref="DetachedHouseBuilder"/>.</summary>
        public static Material[] CreateHouseMaterials(string folder)
        {
            var textures = $"{folder}/Textures";
            Directory.CreateDirectory(textures);

            // Waalformaat-baksteen in halfsteensverband: 4 stenen van 22 cm breed, 16 lagen van 6,25 cm hoog.
            var brick = SaveTexture($"{textures}/Brick.png", Bricks(new Color(0.55f, 0.25f, 0.16f), new Color(0.74f, 0.71f, 0.66f), 11));
            var plinth = SaveTexture($"{textures}/PlinthBrick.png", Bricks(new Color(0.2f, 0.19f, 0.18f), new Color(0.46f, 0.45f, 0.43f), 23));
            var roof = SaveTexture($"{textures}/RoofTiles.png", RoofTiles(new Color(0.2f, 0.21f, 0.22f), 37));
            var paving = SaveTexture($"{textures}/Paving.png", Paving(new Color(0.6f, 0.59f, 0.56f), 41));
            var hedge = SaveTexture($"{textures}/Hedge.png", Foliage(new Color(0.16f, 0.3f, 0.12f), 53));

            return new[]
            {
                Lit(folder, "Brick", brick, Color.white, 0.12f, 0f, new Vector2(1f / 0.88f, 1f)),
                Lit(folder, "Plinth", plinth, Color.white, 0.15f, 0f, new Vector2(1f / 0.88f, 1f)),
                Lit(folder, "RoofTiles", roof, Color.white, 0.3f, 0f, new Vector2(1f / 0.9f, 1f / 1.02f)),
                Lit(folder, "Trim", null, new Color(0.93f, 0.93f, 0.9f), 0.45f, 0f, Vector2.one),
                Lit(folder, "Glass", null, new Color(0.11f, 0.15f, 0.18f), 0.95f, 0.2f, Vector2.one),
                Lit(folder, "FrontDoor", null, new Color(0.08f, 0.2f, 0.15f), 0.5f, 0f, Vector2.one),
                Lit(folder, "Stone", null, new Color(0.56f, 0.56f, 0.55f), 0.25f, 0f, Vector2.one),
                Lit(folder, "Zinc", null, new Color(0.47f, 0.49f, 0.5f), 0.55f, 0.75f, Vector2.one),
                Lit(folder, "Paving", paving, Color.white, 0.18f, 0f, new Vector2(1f / 0.9f, 1f / 0.9f)),
                Lit(folder, "Hedge", hedge, Color.white, 0.05f, 0f, new Vector2(0.5f, 0.5f)),
            };
        }

        private static Material Lit(string folder, string name, Texture2D texture, Color color, float smoothness, float metallic, Vector2 tiling)
        {
            var path = $"{folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", tiling);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D SaveTexture(string path, Color[] pixels)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 8;
            importer.sRGBTexture = true;
            importer.maxTextureSize = Size;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Color[] Bricks(Color brick, Color mortar, int seed)
        {
            const int courses = 16;
            const int perCourse = 4;
            const int courseHeight = Size / courses;
            const int brickLength = Size / perCourse;
            const int joint = 4;
            var pixels = new Color[Size * Size];
            for (var y = 0; y < Size; y++)
            {
                var row = y / courseHeight;
                var inRow = y % courseHeight;
                var offset = row % 2 == 0 ? 0 : brickLength / 2;
                for (var x = 0; x < Size; x++)
                {
                    var shifted = (x + offset) % Size;
                    var index = shifted / brickLength;
                    var inBrick = shifted % brickLength;
                    var grain = Noise(x, y, seed) * 0.08f;
                    if (inRow < joint || inBrick < joint)
                    {
                        pixels[(y * Size) + x] = Shade(mortar, 1f + grain);
                        continue;
                    }

                    // Elke steen een eigen tint; de bovenste pixelrij ligt in de schaduw van de voeg.
                    var shadow = inRow == joint ? 0.12f : 0f;
                    pixels[(y * Size) + x] = Shade(brick, Variation(index % perCourse, row, seed, 0.82f, 0.36f) + grain - shadow);
                }
            }

            return pixels;
        }

        /// <summary>Betonnen dakpannen met holle golf, drie op een rij, met schaduw van de pan erboven.</summary>
        private static Color[] RoofTiles(Color tile, int seed)
        {
            const int rows = 3;
            const int perRow = 3;
            var pixels = new Color[Size * Size];
            for (var y = 0; y < Size; y++)
            {
                var row = y * rows / Size;
                var along = (float)(y * rows % Size) / Size;
                for (var x = 0; x < Size; x++)
                {
                    var index = x * perRow / Size;
                    var across = (float)(x * perRow % Size) / Size;
                    var wave = 0.85f + (0.15f * Mathf.Cos(across * Mathf.PI * 2f));
                    var overlap = along > 0.9f ? 0.55f : 1f - (along * 0.18f);
                    var edge = across < 0.015f ? 0.6f : 1f;
                    pixels[(y * Size) + x] = Shade(tile, Variation(index, row, seed, 0.88f, 0.22f) * wave * overlap * edge * (1f + (Noise(x, y, seed) * 0.06f)));
                }
            }

            return pixels;
        }

        /// <summary>Betontegels van 30 × 30 cm met smalle voegen.</summary>
        private static Color[] Paving(Color stone, int seed)
        {
            const int tiles = 3;
            var pixels = new Color[Size * Size];
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var joint = x * tiles % Size < 3 * tiles || y * tiles % Size < 3 * tiles;
                    var shade = joint ? 0.55f : Variation(x * tiles / Size, y * tiles / Size, seed, 0.9f, 0.16f);
                    pixels[(y * Size) + x] = Shade(stone, shade * (1f + (Noise(x, y, seed) * 0.1f)));
                }
            }

            return pixels;
        }

        /// <summary>Bladermassa: ruis op twee schalen, met donkere gaten tussen de bladeren.</summary>
        private static Color[] Foliage(Color leaf, int seed)
        {
            var pixels = new Color[Size * Size];
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var coarse = Mathf.PerlinNoise((x * 0.035f) + seed, y * 0.035f);
                    var fine = Mathf.PerlinNoise((x * 0.2f) + seed, (y * 0.2f) + seed);
                    var shade = 0.55f + (coarse * 0.5f) + ((fine - 0.5f) * 0.5f);
                    var hue = new Color(leaf.r * (0.9f + (fine * 0.3f)), leaf.g, leaf.b * (0.8f + (coarse * 0.3f)));
                    pixels[(y * Size) + x] = Shade(hue, fine < 0.28f ? shade * 0.45f : shade);
                }
            }

            return pixels;
        }

        private static Color Shade(Color color, float factor) =>
            new Color(Mathf.Clamp01(color.r * factor), Mathf.Clamp01(color.g * factor), Mathf.Clamp01(color.b * factor), 1f);

        /// <summary>Vaste tint per steen of tegel: tussen <paramref name="min"/> en <paramref name="min"/> + <paramref name="range"/>.</summary>
        private static float Variation(int column, int row, int seed, float min, float range) =>
            min + ((Noise(column, row, seed + 7919) + 1f) * 0.5f * range);

        /// <summary>Vaste ruis tussen -1 en 1 per pixel.</summary>
        private static float Noise(int x, int y, int seed)
        {
            unchecked
            {
                var h = (uint)((x * 374761393) + (y * 668265263) + (seed * 982451653));
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return ((h & 0xFFFF) / 32767.5f) - 1f;
            }
        }
    }
}
