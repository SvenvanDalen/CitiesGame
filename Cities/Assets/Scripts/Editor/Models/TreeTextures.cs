using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cities.Editor.Models
{
    /// <summary>
    /// Procedurele texturen voor bomen (vaste seed): bast die rondom en in de lengte naadloos herhaalt, een atlas met
    /// vier bladtrossen en een naaldtak. Blad en naalden hebben alfa voor alpha-clipping; de kleur loopt door onder de
    /// transparante delen, zodat de randen in de mipmaps niet donker worden.
    /// </summary>
    internal static class TreeTextures
    {
        public const int Size = 512;

        /// <summary>Gladde grijze beukenbast met lenticellen en wat korstmos.</summary>
        public static Color[] BroadleafBark()
        {
            var pixels = new Color[Size * Size];
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var u = x / (float)Size;
                    var v = y / (float)Size;
                    var tone = Fbm(u * 4f, v * 4f, 4, 4, 4, 11);
                    var streak = Fbm(u * 24f, v * 3f, 3, 24, 3, 12);
                    var lichen = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 0.72f, Fbm(u * 6f, v * 6f, 4, 6, 6, 13)));
                    var lenticel = Mathf.InverseLerp(0.8f, 0.9f, Fbm(u * 40f, v * 160f, 2, 40, 160, 14));
                    var color = Color.Lerp(new Color(0.42f, 0.41f, 0.38f), new Color(0.55f, 0.54f, 0.5f), tone);
                    color *= 0.88f + (0.18f * streak);
                    color = Color.Lerp(color, new Color(0.44f, 0.5f, 0.32f), lichen * 0.6f);
                    color = Color.Lerp(color, new Color(0.25f, 0.24f, 0.22f), lenticel * 0.7f);
                    pixels[(y * Size) + x] = Opaque(color);
                }
            }

            return pixels;
        }

        /// <summary>Roodbruine sparrenbast in schilfers met donkere voegen.</summary>
        public static Color[] ConiferBark()
        {
            var pixels = new Color[Size * Size];
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var u = x / (float)Size;
                    var v = y / (float)Size;
                    var (edge, plate) = Cells(u * 10f, v * 5f, 10, 5, 21);
                    var tone = Fbm(u * 8f, v * 8f, 4, 8, 8, 22);
                    var color = Color.Lerp(new Color(0.27f, 0.17f, 0.12f), new Color(0.42f, 0.27f, 0.18f), (0.55f * plate) + (0.45f * tone));
                    color *= Mathf.Lerp(0.45f, 1f, Mathf.SmoothStep(0f, 1f, edge * 6f));
                    pixels[(y * Size) + x] = Opaque(color);
                }
            }

            return pixels;
        }

        /// <summary>Atlas van 2 × 2 bladtrossen: een twijg met eironde bladeren in wisselende tinten groen.</summary>
        public static Color[] LeafAtlas()
        {
            var canvas = new Canvas(Size, Size, new Color(0.2f, 0.32f, 0.11f, 0f));
            var random = new HashRandom(31);
            for (var tile = 0; tile < 4; tile++)
            {
                var origin = new Vector2((tile % 2) * 0.5f, (tile / 2) * 0.5f) * Size;
                const float tileSize = Size * 0.5f;
                var stem = origin + new Vector2(tileSize * 0.5f, tileSize * 0.04f);
                var tip = origin + new Vector2(tileSize * random.Range(0.4f, 0.6f), tileSize * 0.92f);
                canvas.Line(stem, tip, 2.2f, new Color(0.3f, 0.22f, 0.14f, 1f));
                for (var leaf = 0; leaf < 34; leaf++)
                {
                    var along = Vector2.Lerp(stem, tip, random.Range(0.1f, 1f));
                    var angle = random.Range(0f, Mathf.PI * 2f);
                    var reach = random.Range(10f, tileSize * 0.3f);
                    var center = along + (new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reach);
                    center = Vector2.Max(origin + (Vector2.one * 14f), Vector2.Min(origin + (Vector2.one * (tileSize - 14f)), center));
                    var length = random.Range(16f, 26f);
                    var green = new Color(random.Range(0.14f, 0.3f), random.Range(0.3f, 0.46f), random.Range(0.07f, 0.15f), 1f);
                    canvas.Line(along, center, 1f, new Color(0.27f, 0.25f, 0.13f, 1f));
                    canvas.Leaf(center, angle, length, length * 0.55f, green);
                }
            }

            return canvas.Pixels;
        }

        /// <summary>Een sparrentak van links (bij de stam) naar rechts, met zijtakjes vol naalden; breedte 512 × 256.</summary>
        public static Color[] ConiferBranch()
        {
            var canvas = new Canvas(Size, Size / 2, new Color(0.1f, 0.2f, 0.1f, 0f));
            var random = new HashRandom(41);
            var start = new Vector2(4f, Size / 4f);
            var end = new Vector2(Size - 8f, (Size / 4f) + 6f);
            canvas.Line(start, end, 3f, new Color(0.3f, 0.21f, 0.14f, 1f));
            for (var i = 0; i < 22; i++)
            {
                var t = 0.06f + (i * 0.042f);
                var from = Vector2.Lerp(start, end, t);
                var side = i % 2 == 0 ? 1f : -1f;
                var reach = Mathf.Lerp(Size * 0.2f, Size * 0.07f, t) * random.Range(0.8f, 1.1f);
                var to = from + new Vector2(reach * 0.7f, side * reach);
                canvas.Line(from, to, 1.4f, new Color(0.28f, 0.2f, 0.12f, 1f));
                Needles(canvas, random, from, to);
            }

            Needles(canvas, random, start + ((end - start) * 0.05f), end);
            return canvas.Pixels;
        }

        public static Texture2D Save(string path, int width, int height, Color[] pixels, bool alphaTested)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = alphaTested ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = alphaTested;
            importer.mipMapsPreserveCoverage = alphaTested;
            importer.alphaTestReferenceValue = 0.5f;
            importer.maxTextureSize = Mathf.Max(width, height);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void Needles(Canvas canvas, HashRandom random, Vector2 from, Vector2 to)
        {
            var length = Vector2.Distance(from, to);
            var direction = (to - from) / Mathf.Max(length, 1f);
            var normal = new Vector2(-direction.y, direction.x);
            for (var d = 0f; d < length; d += 2.2f)
            {
                var root = from + (direction * d);
                var needle = Mathf.Lerp(11f, 6f, d / length) * random.Range(0.8f, 1.2f);
                var green = new Color(random.Range(0.06f, 0.13f), random.Range(0.17f, 0.27f), random.Range(0.08f, 0.13f), 1f);
                canvas.Line(root, root + (((normal * 0.9f) + (direction * 0.5f)) * needle), 0.8f, green);
                canvas.Line(root, root + (((-normal * 0.9f) + (direction * 0.5f)) * needle), 0.8f, green);
            }
        }

        private static Color Opaque(Color color)
        {
            color.a = 1f;
            return color;
        }

        /// <summary>Fractal ruis die per as met de opgegeven periode herhaalt (op de basisschaal).</summary>
        private static float Fbm(float x, float y, int octaves, int periodX, int periodY, int seed)
        {
            var sum = 0f;
            var amplitude = 0.5f;
            var total = 0f;
            for (var o = 0; o < octaves; o++)
            {
                var scale = 1 << o;
                sum += amplitude * ValueNoise(x * scale, y * scale, periodX * scale, periodY * scale, seed + o);
                total += amplitude;
                amplitude *= 0.5f;
            }

            return sum / total;
        }

        private static float ValueNoise(float x, float y, int periodX, int periodY, int seed)
        {
            var x0 = Mathf.FloorToInt(x);
            var y0 = Mathf.FloorToInt(y);
            var fx = x - x0;
            var fy = y - y0;
            var sx = fx * fx * (3f - (2f * fx));
            var sy = fy * fy * (3f - (2f * fy));
            var a = Hash01(x0, y0, periodX, seed, periodY);
            var b = Hash01(x0 + 1, y0, periodX, seed, periodY);
            var c = Hash01(x0, y0 + 1, periodX, seed, periodY);
            var d = Hash01(x0 + 1, y0 + 1, periodX, seed, periodY);
            return Mathf.Lerp(Mathf.Lerp(a, b, sx), Mathf.Lerp(c, d, sx), sy);
        }

        /// <summary>Worley-cellen: afstand tot de rand van de cel en een vaste waarde per cel, naadloos per periode.</summary>
        private static (float Edge, float Value) Cells(float x, float y, int periodX, int periodY, int seed)
        {
            var cx = Mathf.FloorToInt(x);
            var cy = Mathf.FloorToInt(y);
            var nearest = float.MaxValue;
            var second = float.MaxValue;
            var value = 0f;
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var gx = cx + dx;
                    var gy = cy + dy;
                    var px = gx + Hash01(gx, gy, periodX, seed, periodY);
                    var py = gy + Hash01(gx, gy, periodX, seed + 1, periodY);
                    var distance = ((x - px) * (x - px)) + ((y - py) * (y - py));
                    if (distance < nearest)
                    {
                        second = nearest;
                        nearest = distance;
                        value = Hash01(gx, gy, periodX, seed + 2, periodY);
                    }
                    else if (distance < second)
                    {
                        second = distance;
                    }
                }
            }

            return (Mathf.Sqrt(second) - Mathf.Sqrt(nearest), value);
        }

        private static float Hash01(int x, int y, int period, int seed, int periodY = 0)
        {
            var px = ((x % period) + period) % period;
            var py = periodY > 0 ? ((y % periodY) + periodY) % periodY : ((y % period) + period) % period;
            unchecked
            {
                var h = (uint)((px * 374761393) + (py * 668265263) + (seed * 1442695041));
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        /// <summary>Herhaalbare reeks getallen tussen 0 en 1.</summary>
        private sealed class HashRandom
        {
            private uint _state;

            public HashRandom(uint seed) => _state = seed * 747796405u;

            public float Range(float min, float max)
            {
                unchecked
                {
                    _state = (_state * 747796405u) + 2891336453u;
                    var word = ((_state >> (int)((_state >> 28) + 4u)) ^ _state) * 277803737u;
                    word ^= word >> 22;
                    return min + ((max - min) * ((word & 0xFFFFFF) / 16777216f));
                }
            }
        }

        /// <summary>Tekenvlak met afgeronde lijnen en bladvormen, met anti-aliasing via de afstand tot de rand.</summary>
        private sealed class Canvas
        {
            private readonly int _width;
            private readonly int _height;

            public Canvas(int width, int height, Color background)
            {
                _width = width;
                _height = height;
                Pixels = new Color[width * height];
                System.Array.Fill(Pixels, background);
            }

            public Color[] Pixels { get; }

            public void Line(Vector2 a, Vector2 b, float halfWidth, Color color)
            {
                Shape(Vector2.Min(a, b) - (Vector2.one * (halfWidth + 1f)), Vector2.Max(a, b) + (Vector2.one * (halfWidth + 1f)), color, p =>
                {
                    var ab = b - a;
                    var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
                    return Vector2.Distance(p, a + (ab * t)) - halfWidth;
                });
            }

            /// <summary>Blad met spitse punt en een iets donkerdere middennerf.</summary>
            public void Leaf(Vector2 center, float angle, float length, float width, Color color)
            {
                var axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var side = new Vector2(-axis.y, axis.x);
                var reach = Vector2.one * (length + 2f);
                Shape(center - reach, center + reach, color, p =>
                {
                    var d = p - center;
                    var along = Vector2.Dot(d, axis) / length;
                    var across = Vector2.Dot(d, side) / width;

                    // Eirond met de breedste kant bij de steel en een spitse punt.
                    var profile = Mathf.Sqrt(Mathf.Max(0f, 1f - (along * along))) * (1f - (0.35f * Mathf.Max(0f, along)));
                    return (Mathf.Abs(across) - profile) * width;
                });
                Line(center - (axis * length * 0.9f), center + (axis * length * 0.8f), 0.6f, color * 0.75f);
            }

            private void Shape(Vector2 min, Vector2 max, Color color, System.Func<Vector2, float> distance)
            {
                for (var y = Mathf.Max(0, (int)min.y); y <= Mathf.Min(_height - 1, (int)max.y); y++)
                {
                    for (var x = Mathf.Max(0, (int)min.x); x <= Mathf.Min(_width - 1, (int)max.x); x++)
                    {
                        var coverage = Mathf.Clamp01(0.5f - distance(new Vector2(x + 0.5f, y + 0.5f)));
                        if (coverage <= 0f)
                        {
                            continue;
                        }

                        var index = (y * _width) + x;
                        var under = Pixels[index];
                        var mixed = Color.Lerp(under, color, coverage);
                        mixed.a = Mathf.Max(under.a, coverage * color.a);
                        Pixels[index] = mixed;
                    }
                }
            }
        }
    }
}
