using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cities.Editor.Terrain
{
    /// <summary>
    /// Maakt de texturen voor de terreinshader: gras, zand, rots, akker en bebouwde grond, elk 1024² en naadloos
    /// herhalend. Albedo (met hoogte in alfa voor zachte, natuurlijke overgangen) en normalen komen elk in één strook
    /// van vijf vakken, die Unity als <see cref="Texture2DArray"/> importeert. Procedureel met vaste seeds, dus opnieuw
    /// bouwen geeft dezelfde bestanden.
    /// </summary>
    internal static class TerrainTextureBuilder
    {
        public const string Folder = "Assets/Art/Terrain";
        public const int Size = 1024;
        public const int LayerCount = 5;

        /// <summary>Breedte in meters die één tegel van elke laag dekt; de shader gebruikt dezelfde maten.</summary>
        public static readonly float[] LayerMeters = { 4f, 5f, 10f, 8f, 4f };

        private static readonly float[] NormalStrength = { 2.2f, 1.6f, 4.5f, 3.2f, 2.6f };

        [MenuItem("Cities/Terrein/Texturen bouwen")]
        public static void BuildAssets()
        {
            Directory.CreateDirectory(Folder);
            var albedo = new Color[Size * LayerCount * Size];
            var normals = new Color[Size * LayerCount * Size];
            var height = new float[Size * Size];
            for (var layer = 0; layer < LayerCount; layer++)
            {
                for (var y = 0; y < Size; y++)
                {
                    for (var x = 0; x < Size; x++)
                    {
                        var color = Sample(layer, (float)x / Size, (float)y / Size, x, y, out var h);
                        height[(y * Size) + x] = h;
                        albedo[(y * Size * LayerCount) + (layer * Size) + x] = new Color(color.r, color.g, color.b, h);
                    }
                }

                WriteNormals(height, layer, normals);
            }

            Save($"{Folder}/TerrainAlbedo.png", albedo, true);
            Save($"{Folder}/TerrainNormals.png", normals, false);
        }

        private static Color Sample(int layer, float u, float v, int px, int py, out float height)
        {
            switch (layer)
            {
                case 0:
                    return Grass(u, v, px, py, out height);
                case 1:
                    return Sand(u, v, px, py, out height);
                case 2:
                    return Rock(u, v, px, py, out height);
                case 3:
                    return Field(u, v, px, py, out height);
                default:
                    return Built(u, v, px, py, out height);
            }
        }

        /// <summary>Gras van 4 m: polletjes, sprieten en droge plekken.</summary>
        private static Color Grass(float u, float v, int px, int py, out float height)
        {
            var clumps = TileNoise.Fbm(u, v, 6, 4, 11);
            var blades = TileNoise.Fbm(u, v, 128, 2, 12);
            var dry = Mathf.SmoothStep(0.55f, 0.75f, TileNoise.Fbm(u, v, 3, 3, 13));
            var green = new Color(0.25f, 0.36f, 0.13f);
            var straw = new Color(0.47f, 0.44f, 0.24f);
            var color = Color.Lerp(green, straw, dry * 0.6f) * (0.72f + (0.5f * clumps) + (0.18f * (blades - 0.5f)));
            color += new Color(0.03f, 0.04f, 0f) * (TileNoise.Hash(px, py, 14) - 0.5f);
            height = Mathf.Clamp01((0.55f * clumps) + (0.45f * blades));
            return color;
        }

        /// <summary>Zand van 5 m: fijne korrel, windribbels en vochtige plekken.</summary>
        private static Color Sand(float u, float v, int px, int py, out float height)
        {
            var warp = TileNoise.Fbm(u, v, 4, 3, 21);
            var ripple = 0.5f + (0.5f * Mathf.Sin(((v * 14f) + (warp * 1.6f) + (u * 2f)) * Mathf.PI * 2f));
            var damp = Mathf.SmoothStep(0.5f, 0.8f, TileNoise.Fbm(u, v, 3, 3, 22));
            var grain = TileNoise.Hash(px, py, 23) - 0.5f;
            var color = new Color(0.77f, 0.68f, 0.5f) * (0.9f + (0.12f * ripple) + (0.12f * grain) - (0.18f * damp));
            height = Mathf.Clamp01((0.6f * ripple) + (0.25f * warp) + (0.15f * (grain + 0.5f)));
            return color;
        }

        /// <summary>Rots van 10 m: gelaagde banken, scheuren en korstmos.</summary>
        private static Color Rock(float u, float v, int px, int py, out float height)
        {
            var warp = TileNoise.Fbm(u, v, 4, 4, 31);
            var strata = 0.5f + (0.5f * Mathf.Sin(((v * 9f) + (warp * 2.2f)) * Mathf.PI * 2f));
            var ridges = TileNoise.Ridged(u, v, 5, 5, 32);
            var cracks = Mathf.SmoothStep(0.86f, 0.98f, TileNoise.Ridged(u, v, 12, 2, 33));
            var lichen = Mathf.SmoothStep(0.62f, 0.8f, TileNoise.Fbm(u, v, 8, 3, 34));
            var stone = Color.Lerp(new Color(0.42f, 0.4f, 0.37f), new Color(0.55f, 0.51f, 0.45f), strata * 0.6f + (ridges * 0.4f));
            var color = Color.Lerp(stone, new Color(0.5f, 0.52f, 0.36f), lichen * 0.45f) * (1f - (0.55f * cracks));
            color += Color.gray * ((TileNoise.Hash(px, py, 35) - 0.5f) * 0.06f);
            height = Mathf.Clamp01((0.6f * ridges) + (0.25f * strata) - (0.4f * cracks) + 0.15f);
            return color;
        }

        /// <summary>Geploegde akker van 8 m: tien voren, kluiten en stro.</summary>
        private static Color Field(float u, float v, int px, int py, out float height)
        {
            var furrow = 0.5f + (0.5f * Mathf.Cos(((v * 10f) + (0.08f * TileNoise.Fbm(u, v, 4, 2, 41))) * Mathf.PI * 2f));
            var clods = TileNoise.Fbm(u, v, 48, 3, 42);
            var straw = TileNoise.Hash(px / 3, py, 43) > 0.985f ? 1f : 0f;
            var soil = Color.Lerp(new Color(0.22f, 0.16f, 0.11f), new Color(0.38f, 0.29f, 0.2f), (furrow * 0.65f) + (clods * 0.35f));
            var color = Color.Lerp(soil, new Color(0.62f, 0.55f, 0.36f), straw * 0.7f);
            height = Mathf.Clamp01((0.7f * furrow) + (0.3f * clods));
            return color;
        }

        /// <summary>Bebouwde grond van 4 m: aangestampte aarde met grind.</summary>
        private static Color Built(float u, float v, int px, int py, out float height)
        {
            var stone = TileNoise.Stones(u, v, 40, 51, out var stoneShade);
            var dirt = TileNoise.Fbm(u, v, 5, 4, 52);
            var earth = Color.Lerp(new Color(0.44f, 0.4f, 0.34f), new Color(0.56f, 0.52f, 0.45f), dirt);
            var pebble = Color.Lerp(new Color(0.48f, 0.47f, 0.45f), new Color(0.7f, 0.68f, 0.64f), stoneShade);
            var color = Color.Lerp(earth, pebble, stone) * (0.94f + (0.12f * (TileNoise.Hash(px, py, 53) - 0.5f)));
            height = Mathf.Clamp01((0.7f * stone) + (0.3f * dirt));
            return color;
        }

        /// <summary>Normalen uit de hoogte met centrale verschillen; de randen lopen door (naadloos).</summary>
        private static void WriteNormals(float[] height, int layer, Color[] normals)
        {
            var strength = NormalStrength[layer];
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var dx = height[(y * Size) + ((x + 1) % Size)] - height[(y * Size) + ((x + Size - 1) % Size)];
                    var dy = height[((y + 1) % Size * Size) + x] - height[((y + Size - 1) % Size * Size) + x];
                    var n = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                    normals[(y * Size * LayerCount) + (layer * Size) + x] = new Color((n.x * 0.5f) + 0.5f, (n.y * 0.5f) + 0.5f, (n.z * 0.5f) + 0.5f, 1f);
                }
            }
        }

        private static void Save(string path, Color[] pixels, bool srgb)
        {
            var texture = new Texture2D(Size * LayerCount, Size, TextureFormat.RGBA32, false, !srgb);
            texture.SetPixels(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureShape = TextureImporterShape.Texture2DArray;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.flipbookColumns = LayerCount;
            settings.flipbookRows = 1;
            importer.SetTextureSettings(settings);
            importer.sRGBTexture = srgb;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.maxTextureSize = Size * LayerCount;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
    }

    /// <summary>Naadloze ruis op het eenheidsvierkant: het rooster herhaalt met de opgegeven periode.</summary>
    internal static class TileNoise
    {
        /// <summary>Gradiëntruis van 0 tot 1 met <paramref name="octaves"/> lagen, beginnend bij periode <paramref name="period"/>.</summary>
        public static float Fbm(float u, float v, int period, int octaves, int seed)
        {
            var sum = 0f;
            var amplitude = 0.5f;
            var norm = 0f;
            for (var i = 0; i < octaves; i++)
            {
                sum += Gradient(u * period, v * period, period, seed + (i * 101)) * amplitude;
                norm += amplitude;
                amplitude *= 0.5f;
                period *= 2;
            }

            return Mathf.Clamp01(0.5f + (0.5f * sum / norm / 0.7f));
        }

        /// <summary>Kammen van 0 tot 1, scherp waar de ruis door nul gaat.</summary>
        public static float Ridged(float u, float v, int period, int octaves, int seed)
        {
            var sum = 0f;
            var amplitude = 0.5f;
            var norm = 0f;
            for (var i = 0; i < octaves; i++)
            {
                var ridge = 1f - Math.Abs(Gradient(u * period, v * period, period, seed + (i * 101)) / 0.7f);
                sum += ridge * ridge * amplitude;
                norm += amplitude;
                amplitude *= 0.5f;
                period *= 2;
            }

            return Mathf.Clamp01(sum / norm);
        }

        /// <summary>Losse steentjes op een raster van <paramref name="cells"/>²: 1 op een steen, 0 ertussen.</summary>
        public static float Stones(float u, float v, int cells, int seed, out float shade)
        {
            var gx = u * cells;
            var gy = v * cells;
            var cx = (int)Math.Floor(gx);
            var cy = (int)Math.Floor(gy);
            var best = 0f;
            shade = 0.5f;
            for (var oy = -1; oy <= 1; oy++)
            {
                for (var ox = -1; ox <= 1; ox++)
                {
                    var x = Wrap(cx + ox, cells);
                    var y = Wrap(cy + oy, cells);
                    var centerX = cx + ox + Hash(x, y, seed);
                    var centerY = cy + oy + Hash(x, y, seed + 1);
                    var radius = 0.25f + (0.3f * Hash(x, y, seed + 2));
                    var d = Mathf.Sqrt(((gx - centerX) * (gx - centerX)) + ((gy - centerY) * (gy - centerY))) / radius;
                    var bump = Mathf.Clamp01(1f - (d * d));
                    if (bump > best)
                    {
                        best = bump;
                        shade = Hash(x, y, seed + 3);
                    }
                }
            }

            return Mathf.SmoothStep(0f, 1f, best);
        }

        /// <summary>Vaste waarde van 0 tot 1 per roosterpunt.</summary>
        public static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                var h = (uint)((x * 374761393) + (y * 668265263) + (seed * 982451653));
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        private static float Gradient(float x, float y, int period, int seed)
        {
            var x0 = (int)Math.Floor(x);
            var y0 = (int)Math.Floor(y);
            var fx = x - x0;
            var fy = y - y0;
            var sx = fx * fx * fx * ((fx * ((fx * 6f) - 15f)) + 10f);
            var sy = fy * fy * fy * ((fy * ((fy * 6f) - 15f)) + 10f);
            var n00 = Dot(x0, y0, fx, fy, period, seed);
            var n10 = Dot(x0 + 1, y0, fx - 1f, fy, period, seed);
            var n01 = Dot(x0, y0 + 1, fx, fy - 1f, period, seed);
            var n11 = Dot(x0 + 1, y0 + 1, fx - 1f, fy - 1f, period, seed);
            return Mathf.Lerp(Mathf.Lerp(n00, n10, sx), Mathf.Lerp(n01, n11, sx), sy);
        }

        private static float Dot(int ix, int iy, float dx, float dy, int period, int seed)
        {
            var angle = Hash(Wrap(ix, period), Wrap(iy, period), seed) * Mathf.PI * 2f;
            return (Mathf.Cos(angle) * dx) + (Mathf.Sin(angle) * dy);
        }

        private static int Wrap(int value, int period) => ((value % period) + period) % period;
    }
}
