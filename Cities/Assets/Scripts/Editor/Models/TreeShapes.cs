using System.Collections.Generic;
using UnityEngine;
using TreeMesh = Cities.Editor.Models.TreeBuilder.TreeMesh;
using ShapeRandom = Cities.Editor.Models.TreeBuilder.ShapeRandom;

namespace Cities.Editor.Models
{
    /// <summary>
    /// Loofboom van zo'n 17 m: een stam met wortelaanzet die doorloopt als leidtak, zes gesteltakken die naar buiten en
    /// omhoog buigen, twijgen daarop en bladtrossen aan de buitenkant van de kroon. Het blad krijgt normalen vanuit het
    /// midden van de kroon, zodat de kroon als geheel belicht wordt in plaats van als losse kaarten.
    /// </summary>
    internal static class BroadleafShape
    {
        private const int Limbs = 6;

        public static void Build(TreeMesh mesh, bool detail)
        {
            var random = new ShapeRandom(7);
            var trunk = Trunk(random);
            mesh.Tube(Every(trunk.Points, detail ? 1 : 2), Every(trunk.Radii, detail ? 1 : 2), detail ? 10 : 6, 1.6f);

            var clusters = new List<(Vector3 Center, float Size)>();
            for (var i = 0; i < Limbs; i++)
            {
                var height = Mathf.Lerp(4.4f, 9.5f, i / (float)(Limbs - 1)) + random.Range(-0.4f, 0.4f);
                var azimuth = (i * 2.4f) + random.Range(-0.3f, 0.3f);
                var elevation = random.Range(35f, 58f) * Mathf.Deg2Rad;
                var outward = new Vector3(Mathf.Cos(azimuth) * Mathf.Cos(elevation), Mathf.Sin(elevation), Mathf.Sin(azimuth) * Mathf.Cos(elevation));
                var length = random.Range(5f, 6.8f) * (1f - (0.3f * (height - 4.4f) / 5.1f));
                var limb = Branch(TrunkAt(trunk, height), outward, length, TrunkRadius(height) * 0.6f, 0.035f, 6, 0.16f);
                mesh.Tube(detail ? limb.Points : Every(limb.Points, 2), detail ? limb.Radii : Every(limb.Radii, 2), detail ? 6 : 4, 1.2f);
                clusters.Add((limb.Points[^1], 2.4f));
                Twigs(mesh, random, limb.Points, outward, clusters, detail);
            }

            for (var c = 0; c < 3; c++)
            {
                clusters.Add((trunk.Points[^1] + (random.OnSphere() * 1.2f), 2.4f));
            }

            Foliage(mesh, random, clusters, detail);
        }

        /// <summary>Vier twijgen langs een gesteltak, elk met zes bladtrossen.</summary>
        private static void Twigs(TreeMesh mesh, ShapeRandom random, List<Vector3> limb, Vector3 outward, List<(Vector3 Center, float Size)> clusters, bool detail)
        {
            for (var t = 0; t < 4; t++)
            {
                var along = Mathf.Lerp(0.35f, 0.92f, t / 3f) + random.Range(-0.05f, 0.05f);
                var direction = (outward + (random.OnSphere() * 0.9f) + (Vector3.up * 0.35f)).normalized;
                var twig = Branch(Along(limb, along), direction, random.Range(2f, 3.3f), 0.06f, 0.012f, 3, 0.1f);
                if (detail)
                {
                    mesh.Tube(twig.Points, twig.Radii, 3, 0.8f);
                }

                for (var c = 0; c < 6; c++)
                {
                    clusters.Add((Along(twig.Points, 0.2f + (c * 0.16f)) + (random.OnSphere() * 0.6f), random.Range(1.9f, 2.6f)));
                }
            }
        }

        private static void Foliage(TreeMesh mesh, ShapeRandom random, List<(Vector3 Center, float Size)> clusters, bool detail)
        {
            var crown = Vector3.zero;
            foreach (var (center, _) in clusters)
            {
                crown += center / clusters.Count;
            }

            // Detail: drie gekruiste kaarten per tros. Eenvoudig: elke vierde tros, twee keer zo groot.
            for (var i = 0; i < clusters.Count; i += detail ? 1 : 4)
            {
                var (center, size) = clusters[i];
                var outward = (center - crown).normalized;
                var cards = detail ? 3 : 2;
                for (var k = 0; k < cards; k++)
                {
                    var facing = random.OnSphere();
                    var width = Vector3.Cross(facing, random.OnSphere()).normalized;
                    var height = Vector3.Cross(facing, width);
                    var scale = detail ? size : size * 2f;
                    var normal = Vector3.Lerp(facing, outward, 0.8f).normalized;
                    var tile = new Rect((k + i) % 2 * 0.5f, (i / 2) % 2 * 0.5f, 0.5f, 0.5f);
                    mesh.Card(1, center, width * scale, height * scale, normal, tile);
                }
            }
        }

        private static (List<Vector3> Points, List<float> Radii) Trunk(ShapeRandom random)
        {
            var points = new List<Vector3>();
            var radii = new List<float>();
            var lean = new Vector3(random.Range(-0.25f, 0.25f), 0f, random.Range(-0.25f, 0.25f));
            for (var i = 0; i <= 12; i++)
            {
                var y = 15f * Mathf.Pow(i / 12f, 1.15f);
                var sway = new Vector3(Mathf.Sin(y * 0.6f) * 0.12f, 0f, Mathf.Cos(y * 0.45f) * 0.1f);
                points.Add(new Vector3(0f, y, 0f) + (lean * (y / 15f)) + (sway * (y / 15f)));
                radii.Add(TrunkRadius(y));
            }

            return (points, radii);
        }

        /// <summary>Stam van 40 cm doorsnede boven de wortelaanzet, dunner naar de top.</summary>
        private static float TrunkRadius(float height) => (0.2f * Mathf.Pow(Mathf.Max(0f, 1f - (height / 16f)), 0.8f)) + 0.03f + (0.22f * Mathf.Exp(-height * 3f));

        private static Vector3 TrunkAt((List<Vector3> Points, List<float> Radii) trunk, float height) =>
            Along(trunk.Points, Mathf.Pow(height / 15f, 1f / 1.15f));

        /// <summary>Tak die gaandeweg omhoog buigt, van <paramref name="startRadius"/> naar <paramref name="endRadius"/>.</summary>
        private static (List<Vector3> Points, List<float> Radii) Branch(Vector3 origin, Vector3 direction, float length, float startRadius, float endRadius, int segments, float rise)
        {
            var points = new List<Vector3> { origin };
            var radii = new List<float> { startRadius };
            var position = origin;
            for (var i = 1; i <= segments; i++)
            {
                direction = (direction + (Vector3.up * rise)).normalized;
                position += direction * (length / segments);
                points.Add(position);
                radii.Add(Mathf.Lerp(startRadius, endRadius, i / (float)segments));
            }

            return (points, radii);
        }

        private static Vector3 Along(List<Vector3> points, float t)
        {
            var f = Mathf.Clamp01(t) * (points.Count - 1);
            var i = Mathf.Min((int)f, points.Count - 2);
            return Vector3.Lerp(points[i], points[i + 1], f - i);
        }

        private static List<T> Every<T>(List<T> items, int step)
        {
            var result = new List<T>();
            for (var i = 0; i < items.Count; i += step)
            {
                result.Add(items[i]);
            }

            if ((items.Count - 1) % step != 0)
            {
                result.Add(items[^1]);
            }

            return result;
        }
    }

    /// <summary>
    /// Fijnspar van 22 m: een rechte, taps toelopende stam met kransen van vijf takken om de 50 cm. Onderin hangen de
    /// takken door en zijn ze tot 3,8 m lang; naar de top worden ze korter en wijzen ze omhoog. Elke tak is een liggende
    /// kaart met een staande erbij, zodat de kroon van opzij en van boven even vol is.
    /// </summary>
    internal static class ConiferShape
    {
        private const float Height = 22f;
        private const float CrownBase = 1.8f;

        public static void Build(TreeMesh mesh, bool detail)
        {
            var random = new ShapeRandom(13);
            var points = new List<Vector3>();
            var radii = new List<float>();
            for (var i = 0; i <= (detail ? 10 : 4); i++)
            {
                var y = Height * i / (detail ? 10f : 4f);
                points.Add(new Vector3(0f, y, 0f));
                radii.Add((0.32f * (1f - (y / Height))) + 0.02f + (0.18f * Mathf.Exp(-y * 3f)));
            }

            mesh.Tube(points, radii, detail ? 8 : 5, 1.4f);

            var spacing = detail ? 0.5f : 0.75f;
            var perWhorl = 5;
            var whorl = 0;
            for (var y = CrownBase; y < Height - 0.3f; y += spacing, whorl++)
            {
                var t = (y - CrownBase) / (Height - CrownBase);
                for (var k = 0; k < perWhorl; k++)
                {
                    var azimuth = (whorl * 0.9f) + (k * Mathf.PI * 2f / perWhorl) + random.Range(-0.25f, 0.25f);
                    var length = (0.35f + (3.4f * Mathf.Pow(1f - t, 1.05f))) * random.Range(0.85f, 1.1f) * (detail ? 1f : 1.1f);
                    var droop = Mathf.Lerp(-0.4f, 0.35f, t) + random.Range(-0.08f, 0.08f);
                    Branch(mesh, new Vector3(0f, y, 0f), new Vector3(Mathf.Cos(azimuth), droop, Mathf.Sin(azimuth)).normalized, length, detail);
                }
            }

            // De top: een paar korte, rechtopstaande takjes.
            for (var k = 0; k < 2; k++)
            {
                var facing = Quaternion.Euler(0f, k * 90f, 0f) * Vector3.right;
                mesh.Quad(1, new Vector3(0f, Height - 1.4f, 0f) - (facing * 0.25f), new Vector3(0f, Height - 1.4f, 0f) + (facing * 0.25f), new Vector3(0f, Height + 0.2f, 0f) + (facing * 0.1f), new Vector3(0f, Height + 0.2f, 0f) - (facing * 0.1f), Vector3.up, new Rect(0f, 0f, 1f, 1f));
            }
        }

        /// <summary>
        /// Een tak als kaart van de stam naar buiten: smal bij de stam en breder aan het eind. De textuur heeft de tak
        /// langs u (stam links, punt rechts), dus de kaart loopt van u = 0 naar u = 1.
        /// </summary>
        private static void Branch(TreeMesh mesh, Vector3 origin, Vector3 direction, float length, bool detail)
        {
            var side = Vector3.Cross(Vector3.up, direction).normalized;
            var tip = origin + (direction * length);
            var width = (0.55f + (0.45f * length)) * (detail ? 1f : 1.8f);
            var normal = (new Vector3(direction.x, 0f, direction.z).normalized * 0.75f) + (Vector3.up * 0.65f);
            var uv = new Rect(0f, 0f, 1f, 1f);
            mesh.Quad(1, origin - (side * width * 0.2f), tip - (side * width * 0.5f), tip + (side * width * 0.5f), origin + (side * width * 0.2f), normal.normalized, uv);
            var lift = Vector3.Cross(direction, side).normalized;
            var thickness = detail ? 0.4f : 0.6f;
            mesh.Quad(1, origin - (lift * width * thickness * 0.25f), tip - (lift * width * thickness), tip + (lift * width * thickness), origin + (lift * width * thickness * 0.25f), normal.normalized, uv);
        }
    }
}
