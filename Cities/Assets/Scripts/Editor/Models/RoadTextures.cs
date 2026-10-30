using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cities.Editor.Models
{
    /// <summary>
    /// Procedurele texturen voor het wegdek, met vaste seeds. Eén texture beslaat de breedte van een lane (u) en
    /// <c>RoadMeshBuilder.TextureMeters</c> meter in de lengte (v) en herhaalt naadloos in de lengte.
    /// </summary>
    internal static class RoadTextures
    {
        public const int AsphaltWidth = 256;
        public const int AsphaltHeight = 512;
        public const int PavingTilesAcross = 6;
        public const int PavingTilesAlong = 24;
        public const int PavingTilePixels = 32;

        /// <summary>Asfalt met korrel, vlekken en iets lichtere sporen waar de banden lopen; de helderheid ook als hoogte voor de normal map.</summary>
        public static (Color[] Color, float[] Height) Asphalt(Color tint, int seed)
        {
            var color = new Color[AsphaltWidth * AsphaltHeight];
            var height = new float[color.Length];
            for (var y = 0; y < AsphaltHeight; y++)
            {
                for (var x = 0; x < AsphaltWidth; x++)
                {
                    var u = x / (float)AsphaltWidth;
                    var v = y / (float)AsphaltHeight;
                    var patches = TreeTextures.Fbm(u * 3f, v * 6f, 4, 3, 6, seed);
                    var grain = TreeTextures.Hash01(x, y, AsphaltWidth, seed + 7, AsphaltHeight);
                    var coarse = TreeTextures.Fbm(u * 24f, v * 48f, 3, 24, 48, seed + 3);
                    var stone = grain > 0.93f ? 0.12f : 0f;
                    var wheel = Mathf.Exp(-Mathf.Pow((u - 0.27f) / 0.1f, 2f)) + Mathf.Exp(-Mathf.Pow((u - 0.73f) / 0.1f, 2f));
                    var value = 0.78f + ((patches - 0.5f) * 0.28f) + ((coarse - 0.5f) * 0.2f) + ((grain - 0.5f) * 0.1f) + stone + (wheel * 0.05f);
                    var i = (y * AsphaltWidth) + x;
                    color[i] = new Color(tint.r * value, tint.g * value, tint.b * value, 1f);
                    height[i] = (coarse * 0.6f) + (grain * 0.4f) + (stone * 2f);
                }
            }

            return (color, height);
        }

        /// <summary>Betontegels van 33 cm met voegen, per tegel een eigen tint en lichte korrel.</summary>
        public static (Color[] Color, float[] Height) Paving(int seed)
        {
            var width = PavingTilesAcross * PavingTilePixels;
            var depth = PavingTilesAlong * PavingTilePixels;
            var color = new Color[width * depth];
            var height = new float[color.Length];
            for (var y = 0; y < depth; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var tx = x / PavingTilePixels;
                    var ty = y / PavingTilePixels;
                    var inX = x % PavingTilePixels;
                    var inY = y % PavingTilePixels;
                    var joint = inX < 1 || inY < 1 || inX >= PavingTilePixels - 1 || inY >= PavingTilePixels - 1;
                    var tile = TreeTextures.Hash01(tx, ty, PavingTilesAcross, seed, PavingTilesAlong);
                    var noise = TreeTextures.Fbm(x / (float)width * 12f, y / (float)depth * 48f, 3, 12, 48, seed + 5);
                    var grain = TreeTextures.Hash01(x, y, width, seed + 9, depth);
                    var value = 0.62f + ((tile - 0.5f) * 0.12f) + ((noise - 0.5f) * 0.12f) + ((grain - 0.5f) * 0.05f);
                    var i = (y * width) + x;
                    value = joint ? 0.32f : value;
                    color[i] = new Color(value, value * 0.99f, value * 0.96f, 1f);
                    height[i] = joint ? 0f : 0.7f + (noise * 0.3f);
                }
            }

            return (color, height);
        }

        public static Color[] Concrete(int width, int height, int seed)
        {
            var pixels = new Color[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var noise = TreeTextures.Fbm(x / (float)width * 4f, y / (float)height * 16f, 4, 4, 16, seed);
                    var grain = TreeTextures.Hash01(x, y, width, seed + 1, height);
                    var value = 0.58f + ((noise - 0.5f) * 0.2f) + ((grain - 0.5f) * 0.06f);
                    pixels[(y * width) + x] = new Color(value, value, value * 0.97f, 1f);
                }
            }

            return pixels;
        }

        /// <summary>Verf als wit met alfa; versleten plekken vallen weg. <paramref name="dashFraction"/> is het deel van de lengte dat verf heeft (1 = doorgetrokken).</summary>
        public static Color[] Paint(int width, int height, float dashFraction, int seed)
        {
            var pixels = new Color[width * height];
            for (var y = 0; y < height; y++)
            {
                var along = y / (float)height;
                var painted = along < dashFraction;
                for (var x = 0; x < width; x++)
                {
                    var wear = TreeTextures.Fbm(x / (float)width * 2f, along * 24f, 4, 2, 24, seed);
                    var grain = TreeTextures.Hash01(x, y, width, seed + 4, height);
                    var alpha = painted && wear + (grain * 0.12f) < 0.88f ? 1f : 0f;
                    var shade = 0.92f - (grain * 0.08f);
                    pixels[(y * width) + x] = new Color(shade, shade, shade * 0.98f, alpha);
                }
            }

            return pixels;
        }

        /// <summary>Normal map uit een hoogteveld dat in beide richtingen herhaalt.</summary>
        public static Color[] Normals(float[] heights, int width, int depth, float strength)
        {
            var pixels = new Color[heights.Length];
            for (var y = 0; y < depth; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var dx = heights[(y * width) + ((x + 1) % width)] - heights[(y * width) + ((x + width - 1) % width)];
                    var dy = heights[(((y + 1) % depth) * width) + x] - heights[(((y + depth - 1) % depth) * width) + x];
                    var normal = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                    pixels[(y * width) + x] = new Color((normal.x * 0.5f) + 0.5f, (normal.y * 0.5f) + 0.5f, (normal.z * 0.5f) + 0.5f, 1f);
                }
            }

            return pixels;
        }

        public static Texture2D Save(string path, int width, int height, Color[] pixels, TextureKind kind)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, kind == TextureKind.Normal);
            texture.SetPixels(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = kind == TextureKind.Normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.wrapModeU = kind == TextureKind.Paint ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.wrapModeV = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 8;
            importer.alphaIsTransparency = kind == TextureKind.Paint;
            importer.mipMapsPreserveCoverage = kind == TextureKind.Paint;
            importer.alphaTestReferenceValue = 0.5f;
            importer.maxTextureSize = Mathf.Max(width, height);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }

    internal enum TextureKind
    {
        Color,
        Normal,
        Paint,
    }
}
