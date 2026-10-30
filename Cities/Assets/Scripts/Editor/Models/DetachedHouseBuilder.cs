using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cities.Editor.Models
{
    /// <summary>
    /// Bouwt een vrijstaand Nederlands woonhuis op een kavel van 2 × 2 cellen (16 × 16 m): twee bouwlagen in
    /// baksteen op een donkere plint, een zadeldak van 45° met antraciet pannen, witte kozijnen met glas en
    /// dorpels, voordeur met stoep, dakgoten met regenpijpen, een schoorsteen, tuinpad en haag. De voorkant (straat)
    /// ligt aan lokaal -Z; de oorsprong is het midden van de kavel op maaiveld.
    /// </summary>
    internal static class DetachedHouseBuilder
    {
        public const string Folder = "Assets/Art/Buildings/DetachedHouse";

        private const int Brick = 0;
        private const int Plinth = 1;
        private const int Roof = 2;
        private const int Trim = 3;
        private const int Glass = 4;
        private const int Door = 5;
        private const int Stone = 6;
        private const int Zinc = 7;
        private const int Paving = 8;
        private const int Hedge = 9;
        private const int MaterialCount = 10;

        private const float X0 = -4.5f;
        private const float X1 = 4.5f;
        private const float Z0 = -5.5f;
        private const float Z1 = 3.0f;
        private const float PlinthTop = 0.45f;
        private const float Eaves = 5.9f;
        private const float FirstFloor = 3.1f;
        private const float FrameWidth = 0.07f;
        private const float FrameDepth = 0.07f;

        private static float ZMid => (Z0 + Z1) * 0.5f;

        private static float Ridge => Eaves + ((Z1 - Z0) * 0.5f);

        [MenuItem("Cities/Modellen/Vrijstaand huis bouwen")]
        public static void BuildAsset()
        {
            Directory.CreateDirectory(Folder);
            var mesh = BuildMesh();
            var meshPath = $"{Folder}/DetachedHouseMesh.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                mesh = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(mesh, meshPath);
            }

            var materials = ModelMaterials.CreateHouseMaterials(Folder);
            var root = new GameObject("DetachedHouse");
            try
            {
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                var meshRenderer = root.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterials = materials;
                PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/DetachedHouse.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
        }

        public static Mesh BuildMesh()
        {
            var m = new ModelMesh(MaterialCount);
            Walls(m);
            RoofAndChimney(m);
            Gutters(m);
            Openings(m);
            Garden(m);
            return m.Build("DetachedHouseMesh");
        }

        private static void Walls(ModelMesh m)
        {
            // Plint iets voor de gevel en onder het maaiveld, zodat hij op licht hellend terrein niet zweeft.
            m.Box(Plinth, new Vector3(X0 - 0.03f, -0.4f, Z0 - 0.03f), new Vector3(X1 + 0.03f, PlinthTop, Z1 + 0.03f));
            m.Box(Brick, new Vector3(X0, PlinthTop, Z0), new Vector3(X1, Eaves, Z1));

            // Puntgevels aan de zijkanten.
            foreach (var x in new[] { X0, X1 })
            {
                var outward = new Vector3(Mathf.Sign(x), 0f, 0f);
                m.Triangle(Brick, new Vector3(x, Eaves, Z0), new Vector3(x, Eaves, Z1), new Vector3(x, Ridge, ZMid), outward);
            }
        }

        private static void RoofAndChimney(ModelMesh m)
        {
            const float overhang = 0.45f;
            const float verge = 0.3f;
            const float thickness = 0.2f;
            var slope = (Ridge - Eaves) / (ZMid - Z0);
            var lift = thickness * Mathf.Sqrt(1f + (slope * slope));
            var eaveY = Eaves - (overhang * slope);

            foreach (var side in new[] { -1f, 1f })
            {
                var eaveZ = side < 0f ? Z0 - overhang : Z1 + overhang;
                var xa = X0 - verge;
                var xb = X1 + verge;
                // Onderkant van eind tot nok, daarboven de pannen; de nok sluit op de andere helft aan.
                m.Hexahedron(Roof, new[]
                {
                    new Vector3(xa, eaveY, eaveZ), new Vector3(xb, eaveY, eaveZ), new Vector3(xb, eaveY + lift, eaveZ), new Vector3(xa, eaveY + lift, eaveZ),
                    new Vector3(xa, Ridge, ZMid), new Vector3(xb, Ridge, ZMid), new Vector3(xb, Ridge + lift, ZMid), new Vector3(xa, Ridge + lift, ZMid),
                });

                // Witte boeidelen langs de goot en de gevels.
                m.Box(Trim, new Vector3(xa, eaveY - 0.18f, Mathf.Min(eaveZ, eaveZ + (side * 0.03f))), new Vector3(xb, eaveY + lift, Mathf.Max(eaveZ, eaveZ + (side * 0.03f))));
            }

            foreach (var x in new[] { X0 - verge - 0.03f, X1 + verge })
            {
                foreach (var side in new[] { -1f, 1f })
                {
                    var eaveZ = side < 0f ? Z0 - overhang : Z1 + overhang;
                    m.Hexahedron(Trim, new[]
                    {
                        new Vector3(x, eaveY - 0.18f, eaveZ), new Vector3(x + 0.03f, eaveY - 0.18f, eaveZ), new Vector3(x + 0.03f, eaveY + lift, eaveZ), new Vector3(x, eaveY + lift, eaveZ),
                        new Vector3(x, Ridge - 0.18f, ZMid), new Vector3(x + 0.03f, Ridge - 0.18f, ZMid), new Vector3(x + 0.03f, Ridge + lift, ZMid), new Vector3(x, Ridge + lift, ZMid),
                    });
                }
            }

            // Nokvorst.
            m.Box(Roof, new Vector3(X0 - verge, Ridge + lift - 0.05f, ZMid - 0.16f), new Vector3(X1 + verge, Ridge + lift + 0.12f, ZMid + 0.16f));

            // Gemetselde schoorsteen op het achterdakvlak, met een betonnen afdekplaat.
            m.Box(Brick, new Vector3(2.2f, Ridge - 2.2f, ZMid + 0.35f), new Vector3(2.9f, Ridge + 1.1f, ZMid + 1.05f));
            m.Box(Stone, new Vector3(2.12f, Ridge + 1.1f, ZMid + 0.27f), new Vector3(2.98f, Ridge + 1.2f, ZMid + 1.13f));
        }

        private static void Gutters(ModelMesh m)
        {
            const float overhang = 0.45f;
            var slope = (Ridge - Eaves) / (ZMid - Z0);
            var top = Eaves - (overhang * slope) - 0.05f;
            foreach (var side in new[] { -1f, 1f })
            {
                // Bakgoot tegen het boeidel.
                var fascia = side < 0f ? Z0 - overhang - 0.03f : Z1 + overhang + 0.03f;
                var outer = fascia + (side * 0.14f);
                m.Box(Zinc, new Vector3(X0 - 0.3f, top - 0.14f, Mathf.Min(fascia, outer)), new Vector3(X1 + 0.3f, top, Mathf.Max(fascia, outer)));

                // Regenpijp op de hoek: van de goot naar de gevel en dan recht naar beneden.
                var pipeNear = side < 0f ? Z0 - 0.1f : Z1 + 0.02f;
                var gutterMid = fascia + (side * 0.07f);
                var pipeX = X1 - 0.25f;
                m.Box(Zinc, new Vector3(pipeX, top - 0.5f, Mathf.Min(pipeNear, gutterMid)), new Vector3(pipeX + 0.08f, top - 0.42f, Mathf.Max(pipeNear + 0.08f, gutterMid)));
                m.Box(Zinc, new Vector3(pipeX, top - 0.5f, gutterMid - 0.04f), new Vector3(pipeX + 0.08f, top - 0.14f, gutterMid + 0.04f));
                m.Box(Zinc, new Vector3(pipeX, 0.15f, pipeNear), new Vector3(pipeX + 0.08f, top - 0.42f, pipeNear + 0.08f));
            }
        }

        private static void Openings(ModelMesh m)
        {
            var front = new Facade(new Vector3(0f, 0f, Z0), Vector3.right, Vector3.back);
            var back = new Facade(new Vector3(0f, 0f, Z1), Vector3.left, Vector3.forward);
            var left = new Facade(new Vector3(X0, 0f, ZMid), Vector3.back, Vector3.left);
            var right = new Facade(new Vector3(X1, 0f, ZMid), Vector3.forward, Vector3.right);

            // Voorgevel: voordeur met bovenlicht, toiletraampje, woonkamerraam; boven drie slaapkamerramen.
            FrontDoor(m, front, -2.6f);
            Window(m, front, -1.45f, 1.55f, 0.6f, 0.9f);
            Window(m, front, 1.6f, 0.85f, 2.8f, 1.7f);
            Window(m, front, -2.6f, FirstFloor + 0.55f, 1.1f, 1.35f);
            Window(m, front, 0f, FirstFloor + 0.55f, 1.1f, 1.35f);
            Window(m, front, 2.4f, FirstFloor + 0.55f, 1.7f, 1.35f);

            // Achtergevel: schuifpui naar de tuin en een keukenraam.
            Window(m, back, -1.4f, PlinthTop, 3.0f, 2.35f, stoneSill: false);
            Window(m, back, 2.7f, 1.05f, 1.3f, 1.25f);
            Window(m, back, -2.1f, FirstFloor + 0.55f, 1.3f, 1.35f);
            Window(m, back, 2.1f, FirstFloor + 0.55f, 1.3f, 1.35f);

            // Zijgevels, met een zolderraam in de punt.
            Window(m, left, 1.6f, 1.0f, 1.1f, 1.35f);
            Window(m, left, -1.2f, FirstFloor + 0.55f, 1.1f, 1.35f);
            Window(m, left, 0f, Eaves + 0.75f, 0.8f, 1.0f);
            Window(m, right, -1.8f, 1.5f, 0.7f, 0.9f);
            Window(m, right, 1.2f, FirstFloor + 0.55f, 1.1f, 1.35f);
            Window(m, right, 0f, Eaves + 0.75f, 0.8f, 1.0f);
        }

        private static void FrontDoor(ModelMesh m, Facade f, float u)
        {
            const float width = 1.0f;
            const float height = 2.25f;
            var u0 = u - (width * 0.5f);
            var u1 = u + (width * 0.5f);

            // Deurblad iets terug in het kozijn, met een bovenlicht erboven.
            m.Hexahedron(Door, f.Block(u0, u1, PlinthTop, PlinthTop + height, -0.05f, 0.02f));
            Frame(m, f, u0 - FrameWidth, u1 + FrameWidth, PlinthTop, PlinthTop + height + 0.5f);
            m.Hexahedron(Trim, f.Block(u0, u1, PlinthTop + height, PlinthTop + height + FrameWidth, 0f, FrameDepth));
            m.Quad(Glass, f.Point(u0, PlinthTop + height, 0.01f), f.Point(u1, PlinthTop + height, 0.01f), f.Point(u1, PlinthTop + height + 0.5f, 0.01f), f.Point(u0, PlinthTop + height + 0.5f, 0.01f), f.Normal);

            // Deurklink en brievenbus.
            m.Hexahedron(Zinc, f.Block(u1 - 0.18f, u1 - 0.06f, PlinthTop + 1.02f, PlinthTop + 1.06f, 0.02f, 0.08f));
            m.Hexahedron(Zinc, f.Block(u - 0.17f, u + 0.17f, PlinthTop + 0.85f, PlinthTop + 0.9f, 0.02f, 0.04f));

            // Stoep van natuursteen.
            m.Hexahedron(Stone, f.Block(u0 - 0.3f, u1 + 0.3f, -0.2f, PlinthTop - 0.02f, 0f, 0.6f));
        }

        /// <summary>Raam met kozijn, glas en dorpel; brede ramen krijgen een middenstijl, hoge een bovenlicht.</summary>
        private static void Window(ModelMesh m, Facade f, float u, float sill, float width, float height, bool stoneSill = true)
        {
            var u0 = u - (width * 0.5f);
            var u1 = u + (width * 0.5f);
            var top = sill + height;

            m.Quad(Glass, f.Point(u0, sill, 0.015f), f.Point(u1, sill, 0.015f), f.Point(u1, top, 0.015f), f.Point(u0, top, 0.015f), f.Normal);
            Frame(m, f, u0, u1, sill, top);

            if (width > 1.25f)
            {
                var mullions = width > 2.4f ? 2 : 1;
                for (var i = 1; i <= mullions; i++)
                {
                    var at = u0 + (width * i / (mullions + 1));
                    m.Hexahedron(Trim, f.Block(at - (FrameWidth * 0.5f), at + (FrameWidth * 0.5f), sill, top, 0f, FrameDepth * 0.8f));
                }
            }

            if (height > 1.5f)
            {
                var transom = sill + (height * 0.74f);
                m.Hexahedron(Trim, f.Block(u0, u1, transom - (FrameWidth * 0.5f), transom + (FrameWidth * 0.5f), 0f, FrameDepth * 0.8f));
            }

            if (stoneSill)
            {
                m.Hexahedron(Stone, f.Block(u0 - 0.06f, u1 + 0.06f, sill - 0.07f, sill, -0.02f, 0.11f));
            }

            // Stalen latei boven de opening.
            m.Hexahedron(Zinc, f.Block(u0 - 0.1f, u1 + 0.1f, top, top + 0.04f, 0f, 0.012f));
        }

        private static void Frame(ModelMesh m, Facade f, float u0, float u1, float bottom, float top)
        {
            m.Hexahedron(Trim, f.Block(u0, u0 + FrameWidth, bottom, top, -0.02f, FrameDepth));
            m.Hexahedron(Trim, f.Block(u1 - FrameWidth, u1, bottom, top, -0.02f, FrameDepth));
            m.Hexahedron(Trim, f.Block(u0, u1, bottom, bottom + FrameWidth, -0.02f, FrameDepth));
            m.Hexahedron(Trim, f.Block(u0, u1, top - FrameWidth, top, -0.02f, FrameDepth));
        }

        private static void Garden(ModelMesh m)
        {
            // Pad van betontegels van de straat naar de voordeur.
            m.Box(Paving, new Vector3(-3.25f, -0.2f, -8f), new Vector3(-1.95f, 0.04f, Z0 - 0.6f));

            // Lage haag langs de straat, met een opening voor het pad.
            m.Box(Hedge, new Vector3(-7.7f, 0f, -7.9f), new Vector3(-3.5f, 0.85f, -7.3f));
            m.Box(Hedge, new Vector3(-1.7f, 0f, -7.9f), new Vector3(7.7f, 0.85f, -7.3f));
        }

        /// <summary>Assenstelsel van een gevel: <c>u</c> naar rechts gezien van buiten, <c>d</c> naar buiten.</summary>
        private readonly struct Facade
        {
            public Facade(Vector3 origin, Vector3 right, Vector3 normal)
            {
                Origin = origin;
                Right = right;
                Normal = normal;
            }

            public Vector3 Origin { get; }

            public Vector3 Right { get; }

            public Vector3 Normal { get; }

            public Vector3 Point(float u, float y, float d) => Origin + (Right * u) + (Vector3.up * y) + (Normal * d);

            public Vector3[] Block(float u0, float u1, float y0, float y1, float d0, float d1) => new[]
            {
                Point(u0, y0, d0), Point(u1, y0, d0), Point(u1, y1, d0), Point(u0, y1, d0),
                Point(u0, y0, d1), Point(u1, y0, d1), Point(u1, y1, d1), Point(u0, y1, d1),
            };
        }
    }
}
