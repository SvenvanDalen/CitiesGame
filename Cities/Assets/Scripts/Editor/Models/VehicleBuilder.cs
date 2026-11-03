using System.IO;
using Cities.Simulation.Vehicles;
using UnityEditor;
using UnityEngine;

namespace Cities.Editor.Models
{
    /// <summary>
    /// Bouwt de modellen van de voertuigtypes (E21-04): personenauto, bestelbus, vrachtwagen, stadsbus, tram en hulpdienstbus. De modellen zijn
    /// procedureel (vaste afmetingen uit <c>VehicleSpec</c>), met wielen, ruiten, lichten en een eigen lakkleur. De voorkant ligt aan +Z; de oorsprong
    /// is het midden van het voertuig op het wegdek. De bouwer hangt het prefab aan de <c>VehicleDefinition</c> van dezelfde naam.
    /// </summary>
    internal static class VehicleBuilder
    {
        private const string ArtFolder = "Assets/Art/Vehicles";
        private const string DataFolder = "Assets/Data/Vehicles";
        private const int Body = 0;
        private const int Glass = 1;
        private const int Tyre = 2;
        private const int Dark = 3;
        private const int Accent = 4;
        private const int Count = 5;

        [MenuItem("Cities/Modellen/Voertuigen bouwen")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(ArtFolder);
            Save("PassengerCar", Car(), new Color(0.12f, 0.27f, 0.5f), new Color(0.8f, 0.8f, 0.78f));
            Save("DeliveryVan", Van(), new Color(0.92f, 0.92f, 0.9f), new Color(0.1f, 0.35f, 0.7f));
            Save("Truck", Truck(), new Color(0.82f, 0.4f, 0.1f), new Color(0.85f, 0.85f, 0.85f));
            Save("CityBus", Bus(), new Color(0.95f, 0.75f, 0.1f), new Color(0.1f, 0.1f, 0.12f));
            Save("CityTram", Tram(), new Color(0.75f, 0.1f, 0.12f), new Color(0.9f, 0.9f, 0.9f));
            Save("EmergencyVan", Emergency(), new Color(0.95f, 0.95f, 0.93f), new Color(0.8f, 0.08f, 0.08f));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static Material[] Materials(string name, Color body, Color accent) => new[]
        {
            ModelMaterials.Lit(ArtFolder, name + "Paint", null, body, 0.75f, 0.2f, Vector2.one),
            ModelMaterials.Lit(ArtFolder, "VehicleGlass", null, new Color(0.08f, 0.12f, 0.15f), 0.95f, 0.1f, Vector2.one),
            ModelMaterials.Lit(ArtFolder, "VehicleTyre", null, new Color(0.04f, 0.04f, 0.04f), 0.2f, 0f, Vector2.one),
            ModelMaterials.Lit(ArtFolder, "VehicleDark", null, new Color(0.12f, 0.12f, 0.13f), 0.4f, 0.4f, Vector2.one),
            ModelMaterials.Lit(ArtFolder, name + "Accent", null, accent, 0.6f, 0.1f, Vector2.one),
        };

        private static void Save(string name, ModelMesh model, Color body, Color accent)
        {
            var mesh = ModelMesh.Save(model.Build(name + "Mesh"), $"{ArtFolder}/{name}Mesh.asset");
            var root = new GameObject(name);
            GameObject prefab;
            try
            {
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                root.AddComponent<MeshRenderer>().sharedMaterials = Materials(name, body, accent);
                prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{ArtFolder}/{name}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            var definition = AssetDatabase.LoadAssetAtPath<VehicleDefinition>($"{DataFolder}/{name}.asset");
            if (definition != null)
            {
                var so = new SerializedObject(definition);
                so.FindProperty("_prefab").objectReferenceValue = prefab;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>Wiel als achtkant om de X-as: loopvlak, zijkanten en een velg van donker metaal.</summary>
        private static void Wheel(ModelMesh m, float x, float z, float radius, float width)
        {
            const int sides = 12;
            var centre = new Vector3(x, radius, z);
            var half = Vector3.right * (width * 0.5f);
            for (var i = 0; i < sides; i++)
            {
                var a = Spoke(i, sides) * radius;
                var b = Spoke(i + 1, sides) * radius;
                var outward = a + b;
                m.Quad(Tyre, centre - half + a, centre - half + b, centre + half + b, centre + half + a, outward);
                var rim = 0.6f;
                var side = x < 0f ? -1f : 1f;
                var face = centre + (Vector3.right * (side * width * 0.5f));
                m.Triangle(Dark, face, face + (a * rim), face + (b * rim), Vector3.right * side);
            }
        }

        private static Vector3 Spoke(int index, int sides)
        {
            var angle = index * Mathf.PI * 2f / sides;
            return new Vector3(0f, Mathf.Sin(angle), Mathf.Cos(angle));
        }

        private static void Axle(ModelMesh m, float z, float track, float radius, float width)
        {
            Wheel(m, -track * 0.5f, z, radius, width);
            Wheel(m, track * 0.5f, z, radius, width);
        }

        /// <summary>Lichten voor en achter: koplampen wit, achterlichten rood.</summary>
        private static void Lamps(ModelMesh m, float halfWidth, float y, float front, float rear)
        {
            foreach (var side in new[] { -1f, 1f })
            {
                var x = side * (halfWidth - 0.3f);
                m.Box(Accent, new Vector3(x - 0.15f, y, front - 0.02f), new Vector3(x + 0.15f, y + 0.12f, front + 0.03f));
                m.Box(Dark, new Vector3(x - 0.15f, y, rear - 0.03f), new Vector3(x + 0.15f, y + 0.12f, rear + 0.02f));
            }
        }

        /// <summary>Raamband langs beide zijkanten, iets uit het koetswerk zodat het zichtbaar blijft.</summary>
        private static void WindowBand(ModelMesh m, float halfWidth, float y0, float y1, float z0, float z1, int panes)
        {
            var pane = (z1 - z0) / panes;
            for (var i = 0; i < panes; i++)
            {
                var a = z0 + (i * pane) + 0.06f;
                var b = z0 + ((i + 1) * pane) - 0.06f;
                m.Box(Glass, new Vector3(-halfWidth - 0.02f, y0, a), new Vector3(-halfWidth, y1, b));
                m.Box(Glass, new Vector3(halfWidth, y0, a), new Vector3(halfWidth + 0.02f, y1, b));
            }
        }

        private static ModelMesh Car()
        {
            var m = new ModelMesh(Count);
            m.Box(Body, new Vector3(-0.9f, 0.3f, -2.2f), new Vector3(0.9f, 0.85f, 2.25f));
            // Cabine: smaller aan de bovenkant, met voor- en achterruit schuin.
            m.Hexahedron(Glass, new[]
            {
                new Vector3(-0.85f, 0.85f, -1.2f), new Vector3(0.85f, 0.85f, -1.2f), new Vector3(0.85f, 0.85f, 1.0f), new Vector3(-0.85f, 0.85f, 1.0f),
                new Vector3(-0.72f, 1.42f, -0.8f), new Vector3(0.72f, 1.42f, -0.8f), new Vector3(0.72f, 1.42f, 0.5f), new Vector3(-0.72f, 1.42f, 0.5f),
            });
            m.Box(Body, new Vector3(-0.74f, 1.4f, -0.82f), new Vector3(0.74f, 1.46f, 0.52f));
            m.Box(Dark, new Vector3(-0.9f, 0.25f, -2.25f), new Vector3(0.9f, 0.4f, 2.3f));
            Axle(m, 1.4f, 1.55f, 0.32f, 0.22f);
            Axle(m, -1.4f, 1.55f, 0.32f, 0.22f);
            Lamps(m, 0.9f, 0.62f, 2.25f, -2.2f);
            return m;
        }

        private static ModelMesh Van()
        {
            var m = new ModelMesh(Count);
            m.Box(Body, new Vector3(-1f, 0.4f, -2.6f), new Vector3(1f, 2.3f, 1.2f));
            m.Hexahedron(Body, new[]
            {
                new Vector3(-1f, 0.4f, 1.2f), new Vector3(1f, 0.4f, 1.2f), new Vector3(1f, 1.5f, 1.2f), new Vector3(-1f, 1.5f, 1.2f),
                new Vector3(-1f, 0.4f, 2.6f), new Vector3(1f, 0.4f, 2.6f), new Vector3(1f, 1.1f, 2.6f), new Vector3(-1f, 1.1f, 2.6f),
            });
            m.Hexahedron(Glass, new[]
            {
                new Vector3(-0.98f, 1.1f, 1.25f), new Vector3(0.98f, 1.1f, 1.25f), new Vector3(0.98f, 2.0f, 1.2f), new Vector3(-0.98f, 2.0f, 1.2f),
                new Vector3(-0.98f, 1.1f, 2.0f), new Vector3(0.98f, 1.1f, 2.0f), new Vector3(0.98f, 1.15f, 2.0f), new Vector3(-0.98f, 1.15f, 2.0f),
            });
            m.Box(Accent, new Vector3(-1.01f, 0.9f, -2.6f), new Vector3(1.01f, 1.05f, 1.2f));
            m.Box(Dark, new Vector3(-1f, 0.3f, -2.65f), new Vector3(1f, 0.45f, 2.65f));
            Axle(m, 1.7f, 1.7f, 0.36f, 0.24f);
            Axle(m, -1.5f, 1.7f, 0.36f, 0.24f);
            Lamps(m, 1f, 0.8f, 2.6f, -2.6f);
            return m;
        }

        private static ModelMesh Truck()
        {
            var m = new ModelMesh(Count);
            // Cabine voorin, bakwagen erachter, chassis eronder.
            m.Box(Body, new Vector3(-1.27f, 0.7f, 3.0f), new Vector3(1.27f, 2.9f, 5.0f));
            m.Box(Glass, new Vector3(-1.2f, 1.7f, 4.95f), new Vector3(1.2f, 2.7f, 5.03f));
            m.Box(Glass, new Vector3(-1.29f, 1.7f, 3.4f), new Vector3(-1.27f, 2.6f, 4.6f));
            m.Box(Glass, new Vector3(1.27f, 1.7f, 3.4f), new Vector3(1.29f, 2.6f, 4.6f));
            m.Box(Dark, new Vector3(-1.1f, 0.55f, -5f), new Vector3(1.1f, 0.85f, 5.05f));
            m.Box(Accent, new Vector3(-1.275f, 0.95f, -5f), new Vector3(1.275f, 3.5f, 2.9f));
            m.Box(Dark, new Vector3(-1.0f, 3.5f, -4.9f), new Vector3(1.0f, 3.55f, 2.8f));
            Axle(m, 3.9f, 2.0f, 0.5f, 0.32f);
            Axle(m, -2.6f, 2.0f, 0.5f, 0.32f);
            Axle(m, -3.9f, 2.0f, 0.5f, 0.32f);
            Lamps(m, 1.27f, 1.0f, 5.0f, -5f);
            return m;
        }

        private static ModelMesh Bus()
        {
            var m = new ModelMesh(Count);
            m.Box(Body, new Vector3(-1.275f, 0.45f, -6f), new Vector3(1.275f, 3.2f, 6f));
            m.Box(Accent, new Vector3(-1.28f, 0.45f, -6f), new Vector3(1.28f, 0.9f, 6f));
            WindowBand(m, 1.275f, 1.35f, 2.6f, -5.5f, 4.2f, 8);
            // Voorruit, achterruit en deuren aan de rechterzijde.
            m.Box(Glass, new Vector3(-1.2f, 1.2f, 5.98f), new Vector3(1.2f, 2.9f, 6.03f));
            m.Box(Glass, new Vector3(-1.2f, 1.6f, -6.03f), new Vector3(1.2f, 2.6f, -5.98f));
            m.Box(Glass, new Vector3(1.28f, 0.95f, 4.4f), new Vector3(1.3f, 2.8f, 5.6f));
            m.Box(Glass, new Vector3(1.28f, 0.95f, -0.9f), new Vector3(1.3f, 2.8f, 0.3f));
            m.Box(Dark, new Vector3(-1.0f, 3.2f, -3f), new Vector3(1.0f, 3.4f, 1f));
            Axle(m, 3.6f, 2.1f, 0.5f, 0.3f);
            Axle(m, -3.4f, 2.1f, 0.5f, 0.3f);
            Lamps(m, 1.275f, 0.75f, 6f, -6f);
            return m;
        }

        private static ModelMesh Tram()
        {
            var m = new ModelMesh(Count);
            // Twee wagendelen met een harmonica ertussen.
            m.Box(Body, new Vector3(-1.2f, 0.5f, 0.5f), new Vector3(1.2f, 3.3f, 15f));
            m.Box(Body, new Vector3(-1.2f, 0.5f, -15f), new Vector3(1.2f, 3.3f, -0.5f));
            m.Box(Dark, new Vector3(-1.1f, 0.6f, -0.5f), new Vector3(1.1f, 3.1f, 0.5f));
            m.Box(Accent, new Vector3(-1.205f, 0.5f, -15f), new Vector3(1.205f, 0.9f, 15f));
            WindowBand(m, 1.2f, 1.3f, 2.7f, -14.5f, -0.6f, 6);
            WindowBand(m, 1.2f, 1.3f, 2.7f, 0.6f, 14.5f, 6);
            m.Box(Glass, new Vector3(-1.1f, 1.2f, 14.98f), new Vector3(1.1f, 3.0f, 15.03f));
            m.Box(Glass, new Vector3(-1.1f, 1.2f, -15.03f), new Vector3(1.1f, 3.0f, -14.98f));
            // Stroomafnemer op het dak.
            m.Box(Dark, new Vector3(-0.6f, 3.3f, 6f), new Vector3(0.6f, 3.4f, 7.2f));
            m.Box(Dark, new Vector3(-0.05f, 3.4f, 6.4f), new Vector3(0.05f, 4.2f, 6.5f));
            foreach (var z in new[] { 11f, 5f, -5f, -11f })
            {
                Axle(m, z, 1.435f, 0.35f, 0.14f);
            }

            Lamps(m, 1.2f, 0.9f, 15f, -15f);
            return m;
        }

        private static ModelMesh Emergency()
        {
            var m = Van();
            // Rood-wit hulpdienstbus: lichtbalk en rode band over het koetswerk.
            m.Box(Accent, new Vector3(-0.8f, 2.3f, 0.5f), new Vector3(0.8f, 2.45f, 1.1f));
            m.Box(Glass, new Vector3(-0.5f, 2.45f, 0.55f), new Vector3(0.5f, 2.55f, 1.05f));
            m.Box(Accent, new Vector3(-1.015f, 1.4f, -2.6f), new Vector3(1.015f, 1.7f, 1.2f));
            return m;
        }
    }
}
