using System;
using System.IO;
using Cities.World.Utilities;
using UnityEditor;
using UnityEngine;
using static Cities.Editor.Models.PowerPlantMaterials.Slots;

namespace Cities.Editor.Models
{
    /// <summary>
    /// Bouwt de modellen van de drie centrales (E06-02): een windturbine van 85 m naafhoogte, een zonnepark met gekantelde paneelrijen,
    /// omheining en omvormerstation, en een kolencentrale met ketelhuis, machinehal, twee koeltorens, schoorsteen, kolenhoop met
    /// transportband en een schakelstation. De voorkant ligt aan lokaal -Z; de oorsprong is het midden van de kavel op maaiveld.
    /// </summary>
    internal static class PowerPlantBuilder
    {
        private const string ArtFolder = "Assets/Art/Buildings/PowerPlants";
        private const string DataFolder = "Assets/Data/PowerPlants";

        [MenuItem("Cities/Modellen/Centrales bouwen")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(ArtFolder);
            var materials = PowerPlantMaterials.Create();
            Save("WindTurbine", WindTurbine(), materials);
            Save("SolarPark", SolarPark(), materials);
            Save("CoalPlant", CoalPlant(), materials);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void Save(string name, ModelMesh model, Material[] materials)
        {
            var mesh = ModelMesh.Save(model.Build(name + "Mesh"), $"{ArtFolder}/{name}Mesh.asset");
            var root = new GameObject(name);
            GameObject prefab;
            try
            {
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                root.AddComponent<MeshRenderer>().sharedMaterials = materials;
                prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{ArtFolder}/{name}.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            var definition = AssetDatabase.LoadAssetAtPath<PowerPlantDefinition>($"{DataFolder}/{name}.asset");
            if (definition != null)
            {
                var so = new SerializedObject(definition);
                so.FindProperty("_prefab").objectReferenceValue = prefab;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ---------- Windturbine ----------

        private static ModelMesh WindTurbine()
        {
            const float hub = 85f;
            const float radius0 = 2.1f;
            const float radius1 = 1.4f;
            const int sections = 5;
            var m = new ModelMesh(Count);

            m.Frustum(Concrete, new Vector3(0f, 0f, 0f), 5.2f, 5.0f, 0.5f, 28);
            var step = hub / sections;
            for (var i = 0; i < sections; i++)
            {
                var r0 = Mathf.Lerp(radius0, radius1, i / (float)sections);
                var r1 = Mathf.Lerp(radius0, radius1, (i + 1) / (float)sections);
                m.Frustum(White, new Vector3(0f, i * step, 0f), r0, r1, step, 28);
                // Flenzen waar de torensecties op elkaar aansluiten.
                m.Frustum(Steel, new Vector3(0f, (i * step) - 0.15f, 0f), r0 + 0.08f, r0 + 0.08f, 0.3f, 28);
            }

            // Toegangsdeur, ladder-platform en schakelkast aan de voet.
            m.Box(Door, new Vector3(-0.55f, 0.5f, -2.18f), new Vector3(0.55f, 2.7f, -1.95f));
            m.Box(Steel, new Vector3(-1.6f, 0.5f, -3.3f), new Vector3(1.6f, 0.6f, -1.9f));
            m.Box(White, new Vector3(2.4f, 0.5f, -3.4f), new Vector3(4.4f, 3.0f, -2.2f));

            // Gondel met een kopstuk en een afgeronde neus.
            m.Box(White, new Vector3(-1.75f, hub - 1.75f, -3f), new Vector3(1.75f, hub + 1.75f, 7.5f));
            m.Box(Steel, new Vector3(-0.8f, hub + 1.75f, 3.5f), new Vector3(0.8f, hub + 2.1f, 6.5f));
            m.Box(White, new Vector3(-1.4f, hub - 1.4f, -5.6f), new Vector3(1.4f, hub + 1.4f, -3f));
            m.Hexahedron(White, new[]
            {
                new Vector3(-1.4f, hub - 1.4f, -5.6f), new Vector3(1.4f, hub - 1.4f, -5.6f), new Vector3(1.4f, hub + 1.4f, -5.6f), new Vector3(-1.4f, hub + 1.4f, -5.6f),
                new Vector3(-0.35f, hub - 0.35f, -7.4f), new Vector3(0.35f, hub - 0.35f, -7.4f), new Vector3(0.35f, hub + 0.35f, -7.4f), new Vector3(-0.35f, hub + 0.35f, -7.4f),
            });

            // Drie bladen onder 120 graden, elk als vijf aansluitende stukken met afnemende koorde en dikte.
            var t = new[] { 1.4f, 9f, 20f, 34f, 48f };
            var chord = new[] { 2.4f, 4.2f, 3.1f, 1.8f, 0.4f };
            var thick = new[] { 1.1f, 0.65f, 0.38f, 0.2f, 0.08f };
            for (var blade = 0; blade < 3; blade++)
            {
                var angle = (20f + (blade * 120f)) * Mathf.Deg2Rad;
                var along = new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0f);
                var across = new Vector3(Mathf.Cos(angle), -Mathf.Sin(angle), 0f);
                for (var i = 0; i < t.Length - 1; i++)
                {
                    m.Hexahedron(White, Section(new Vector3(0f, hub, -4.2f), along, across, new Vector3(t[i], chord[i], thick[i]), new Vector3(t[i + 1], chord[i + 1], thick[i + 1])));
                }
            }

            return m;
        }

        /// <summary>Acht hoeken van een bladstuk tussen twee dwarsdoorsneden (x = afstand langs het blad, y = koorde, z = dikte); de koorde loopt langs Z, de dikte langs <paramref name="across"/>.</summary>
        private static Vector3[] Section(Vector3 hub, Vector3 along, Vector3 across, Vector3 from, Vector3 to)
        {
            Vector3[] Ring(Vector3 at)
            {
                var centre = hub + (along * at.x);
                var z = Vector3.forward * (at.y * 0.5f);
                var d = across * (at.z * 0.5f);
                return new[] { centre + z + d, centre + z - d, centre - z - d, centre - z + d };
            }

            var a = Ring(from);
            var b = Ring(to);
            return new[] { a[0], a[1], a[2], a[3], b[0], b[1], b[2], b[3] };
        }

        // ---------- Zonnepark ----------

        private static ModelMesh SolarPark()
        {
            var m = new ModelMesh(Count);
            const float low = 0.8f;
            const float high = 2.9f;
            const float depth = 4f;
            const float width = 12f;

            // Paneelrijen: zes rijen van vier tafels, schuin omhoog naar het noorden (+Z), met poten en een draagbalk.
            var normal = new Vector3(0f, depth, -(high - low)).normalized;
            for (var row = 0; row < 6; row++)
            {
                var zFront = -22f + (row * 8f) - (depth * 0.5f);
                var zBack = zFront + depth;
                for (var col = 0; col < 4; col++)
                {
                    var x0 = -24f + (col * 16f) - (width * 0.5f);
                    var x1 = x0 + width;
                    var a = new[] { new Vector3(x0, low, zFront), new Vector3(x1, low, zFront), new Vector3(x1, high, zBack), new Vector3(x0, high, zBack) };
                    var b = new Vector3[4];
                    for (var i = 0; i < 4; i++)
                    {
                        b[i] = a[i] + (normal * 0.06f);
                    }

                    m.Hexahedron(Steel, new[] { a[0], a[1], a[2], a[3], b[0], b[1], b[2], b[3] });
                    m.Quad(SolarPanel, b[0] + (normal * 0.004f), b[1] + (normal * 0.004f), b[2] + (normal * 0.004f), b[3] + (normal * 0.004f), normal);
                    foreach (var x in new[] { x0 + 1f, (x0 + x1) * 0.5f, x1 - 1f })
                    {
                        m.Box(Steel, new Vector3(x - 0.05f, 0f, zFront - 0.05f), new Vector3(x + 0.05f, low, zFront + 0.05f));
                        m.Box(Steel, new Vector3(x - 0.05f, 0f, zBack - 0.05f), new Vector3(x + 0.05f, high, zBack + 0.05f));
                    }

                    // Draagbalk onder het midden van de tafel, parallel aan de helling.
                    var mid = Vector3.Lerp(a[0], a[3], 0.5f) - (normal * 0.1f);
                    m.Box(Steel, new Vector3(x0, mid.y - 0.05f, mid.z - 0.05f), new Vector3(x1, mid.y + 0.05f, mid.z + 0.05f));
                }
            }

            Inverter(m);
            Fence(m, 31f, 31f, 6f);
            return m;
        }

        private static void Inverter(ModelMesh m)
        {
            // Omvormerstation: betonnen cabine met een deur en luchtroosters, een transformator ernaast.
            m.Box(Concrete, new Vector3(-12f, 0f, 24f), new Vector3(-2f, 3.2f, 29.5f));
            m.Box(Stone, new Vector3(-12.2f, 3.2f, 23.8f), new Vector3(-1.8f, 3.4f, 29.7f));
            m.Box(Door, new Vector3(-9f, 0f, 23.9f), new Vector3(-7.6f, 2.4f, 24f));
            for (var i = 0; i < 4; i++)
            {
                m.Box(Steel, new Vector3(-5.8f + (i * 1.0f), 1.2f, 23.93f), new Vector3(-5.1f + (i * 1.0f), 2.6f, 24f));
            }

            m.Box(White, new Vector3(2f, 0f, 24.5f), new Vector3(7f, 3.4f, 28.5f));
            for (var i = 0; i < 8; i++)
            {
                m.Box(Steel, new Vector3(2.3f + (i * 0.6f), 0.6f, 23.9f), new Vector3(2.5f + (i * 0.6f), 3.2f, 24.5f));
            }
        }

        /// <summary>Gaashek langs een rechthoek van ±<paramref name="hx"/> bij ±<paramref name="hz"/> met palen om de 4 m en een poort aan de voorkant.</summary>
        private static void Fence(ModelMesh m, float hx, float hz, float gate)
        {
            const float spacing = 4f;
            const float height = 2.2f;
            void Run(Vector3 from, Vector3 to, bool skipGate)
            {
                var length = Vector3.Distance(from, to);
                var count = Mathf.CeilToInt(length / spacing);
                for (var i = 0; i <= count; i++)
                {
                    var p = Vector3.Lerp(from, to, i / (float)count);
                    if (skipGate && Mathf.Abs(p.x) < gate * 0.5f)
                    {
                        continue;
                    }

                    m.Box(Steel, new Vector3(p.x - 0.05f, 0f, p.z - 0.05f), new Vector3(p.x + 0.05f, height, p.z + 0.05f));
                }

                foreach (var y in new[] { 0.15f, height - 0.05f })
                {
                    var lo = Vector3.Min(from, to);
                    var hi = Vector3.Max(from, to);
                    m.Box(Steel, new Vector3(lo.x - 0.02f, y, lo.z - 0.02f), new Vector3(hi.x + 0.02f, y + 0.04f, hi.z + 0.02f));
                }
            }

            Run(new Vector3(-hx, 0f, hz), new Vector3(hx, 0f, hz), false);
            Run(new Vector3(-hx, 0f, -hz), new Vector3(hx, 0f, -hz), true);
            Run(new Vector3(-hx, 0f, -hz), new Vector3(-hx, 0f, hz), false);
            Run(new Vector3(hx, 0f, -hz), new Vector3(hx, 0f, hz), false);
        }

        // ---------- Kolencentrale ----------

        private static ModelMesh CoalPlant()
        {
            var m = new ModelMesh(Count);
            m.Box(Asphalt, new Vector3(-40f, -0.2f, -32f), new Vector3(40f, 0.03f, 32f));

            Hall(m, new Vector3(-28f, 0f, -24f), new Vector3(4f, 18f, -10f), 3);
            Boiler(m);
            Stack(m, new Vector3(-8f, 0f, 22f));
            CoolingTower(m, new Vector3(26f, 0f, -1f));
            CoolingTower(m, new Vector3(26f, 0f, 20f));
            CoalPile(m);
            Switchyard(m);

            // Administratiegebouw voor, links van de machinehal.
            m.Box(Concrete, new Vector3(-38f, 0f, -29f), new Vector3(-31f, 7f, -19f));
            m.Quad(Glass, new Vector3(-37.5f, 0.9f, -29.02f), new Vector3(-31.5f, 0.9f, -29.02f), new Vector3(-31.5f, 2.9f, -29.02f), new Vector3(-37.5f, 2.9f, -29.02f), Vector3.back);
            m.Quad(Glass, new Vector3(-37.5f, 4.2f, -29.02f), new Vector3(-31.5f, 4.2f, -29.02f), new Vector3(-31.5f, 6.2f, -29.02f), new Vector3(-37.5f, 6.2f, -29.02f), Vector3.back);
            m.Box(Stone, new Vector3(-38.2f, 7f, -29.2f), new Vector3(-30.8f, 7.25f, -18.8f));
            m.Box(Door, new Vector3(-35.8f, 0f, -29.05f), new Vector3(-33.8f, 2.3f, -29f));
            return m;
        }

        /// <summary>Hal met betonnen sokkel, golfplaat erboven, een lichtstrook en een dakrand.</summary>
        private static void Hall(ModelMesh m, Vector3 min, Vector3 max, int bays)
        {
            const float plinth = 4f;
            m.Box(Concrete, min, new Vector3(max.x, plinth, max.z));
            m.Box(Cladding, new Vector3(min.x, plinth, min.z), max);
            m.Box(Stone, new Vector3(min.x - 0.2f, max.y, min.z - 0.2f), new Vector3(max.x + 0.2f, max.y + 0.3f, max.z + 0.2f));
            var z = min.z - 0.02f;
            m.Quad(Glass, new Vector3(min.x + 1f, 12f, z), new Vector3(max.x - 1f, 12f, z), new Vector3(max.x - 1f, 15f, z), new Vector3(min.x + 1f, 15f, z), Vector3.back);
            for (var i = 0; i <= bays * 4; i++)
            {
                var x = Mathf.Lerp(min.x + 1f, max.x - 1f, i / (bays * 4f));
                m.Box(Steel, new Vector3(x - 0.06f, 12f, z - 0.05f), new Vector3(x + 0.06f, 15f, z + 0.02f));
            }

            // Overheaddeur voor onderhoud en een personeelsdeur.
            m.Box(Door, new Vector3(-6f, 0f, z - 0.05f), new Vector3(0f, 6f, z + 0.02f));
            m.Box(Door, new Vector3(2f, 0f, z - 0.03f), new Vector3(3.1f, 2.3f, z + 0.02f));
        }

        private static void Boiler(ModelMesh m)
        {
            // Ketelhuis: vierkante toren van golfplaat met betonnen hoekkolommen en stalen banden om de 8 m.
            var min = new Vector3(-26f, 0f, -8f);
            var max = new Vector3(-2f, 40f, 14f);
            m.Box(Cladding, min, max);
            foreach (var x in new[] { min.x - 0.3f, max.x - 0.9f })
            {
                foreach (var z in new[] { min.z - 0.3f, max.z - 0.9f })
                {
                    m.Box(Concrete, new Vector3(x, 0f, z), new Vector3(x + 1.2f, max.y + 0.4f, z + 1.2f));
                }
            }

            for (var y = 8f; y < max.y; y += 8f)
            {
                m.Box(Steel, new Vector3(min.x - 0.1f, y, min.z - 0.1f), new Vector3(max.x + 0.1f, y + 0.5f, max.z + 0.1f));
                m.Quad(Glass, new Vector3(min.x + 2f, y + 1.5f, min.z - 0.12f), new Vector3(max.x - 2f, y + 1.5f, min.z - 0.12f), new Vector3(max.x - 2f, y + 3.5f, min.z - 0.12f), new Vector3(min.x + 2f, y + 3.5f, min.z - 0.12f), Vector3.back);
            }

            m.Box(Stone, new Vector3(min.x - 0.4f, max.y + 0.4f, min.z - 0.4f), new Vector3(max.x + 0.4f, max.y + 0.7f, max.z + 0.4f));
            // Dakopbouw voor de rookgasreiniging.
            m.Box(Steel, new Vector3(-20f, max.y + 0.7f, 2f), new Vector3(-8f, max.y + 5f, 11f));
        }

        /// <summary>Betonnen schoorsteen in vier afnemende stukken, met rode banden bovenaan.</summary>
        private static void Stack(ModelMesh m, Vector3 at)
        {
            var radii = new[] { 5.2f, 4.5f, 3.9f, 3.5f, 3.3f };
            const float section = 18f;
            for (var i = 0; i < 4; i++)
            {
                m.Frustum(Concrete, at + (Vector3.up * (i * section)), radii[i], radii[i + 1], section, 28);
            }

            foreach (var y in new[] { 54f, 63f })
            {
                m.Frustum(Red, at + (Vector3.up * y), Mathf.Lerp(radii[2], radii[4], (y - 36f) / 36f) + 0.04f, Mathf.Lerp(radii[2], radii[4], ((y + 4.5f) - 36f) / 36f) + 0.04f, 4.5f, 28);
            }

            m.Frustum(Steel, at + (Vector3.up * 72f), 3.4f, 3.4f, 0.8f, 28);
        }

        /// <summary>Natuurtrekkoeltoren met hyperbolisch profiel van 60 m hoog en 22 m breed aan de voet.</summary>
        private static void CoolingTower(ModelMesh m, Vector3 at)
        {
            var radii = new[] { 11f, 10.1f, 9.3f, 8.6f, 8.1f, 7.8f, 7.9f, 8.3f, 8.8f, 9.3f, 9.6f };
            const float step = 6f;
            for (var i = 0; i < radii.Length - 1; i++)
            {
                m.Frustum(Concrete, at + (Vector3.up * (i * step)), radii[i], radii[i + 1], step, 32);
            }

            // Verdikte bovenrand, lage waterbak eromheen en een luchtinlaat met schaduwgaten onderaan.
            m.Frustum(Stone, at + (Vector3.up * 60f), 9.7f, 9.7f, 0.5f, 32);
            m.Frustum(Stone, at + (Vector3.up * -0.1f), 13.2f, 13.2f, 0.9f, 32);
            m.Frustum(Asphalt, at + (Vector3.up * 0.7f), 12.9f, 12.9f, 0.05f, 32);
            for (var i = 0; i < 16; i++)
            {
                var a = i * Mathf.PI * 2f / 16f;
                var pos = at + new Vector3(Mathf.Cos(a) * 10.9f, 0f, Mathf.Sin(a) * 10.9f);
                m.Box(Steel, pos + new Vector3(-0.4f, 0f, -0.4f), pos + new Vector3(0.4f, 5f, 0.4f));
            }
        }

        private static void CoalPile(ModelMesh m)
        {
            m.Frustum(Coal, new Vector3(-31f, 0f, 22f), 8f, 2.5f, 6.5f, 20);
            m.Frustum(Coal, new Vector3(-22f, 0f, 27f), 5f, 1.5f, 4f, 20);

            // Transportband in een omkaste gang van de hoop naar de bovenkant van het ketelhuis, op stalen steunen.
            var from = new Vector3(-30f, 7f, 22f);
            var to = new Vector3(-14f, 38f, 12f);
            var direction = (to - from).normalized;
            var side = Vector3.Cross(Vector3.up, direction).normalized * 0.9f;
            var up = Vector3.Cross(direction, side).normalized * 0.9f;
            m.Hexahedron(Cladding, new[]
            {
                from - side - up, from + side - up, from + side + up, from - side + up,
                to - side - up, to + side - up, to + side + up, to - side + up,
            });
            for (var i = 1; i <= 4; i++)
            {
                var p = Vector3.Lerp(from, to, i / 5f);
                m.Box(Steel, new Vector3(p.x - 0.2f, 0f, p.z - 0.2f), new Vector3(p.x + 0.2f, p.y - 1.1f, p.z + 0.2f));
            }
        }

        private static void Switchyard(ModelMesh m)
        {
            // Twee krachttransformatoren met koelribben en een portaal met isolatoren en een dwarsbalk.
            foreach (var x in new[] { 18f, 30f })
            {
                m.Box(White, new Vector3(x - 3f, 0.6f, -27f), new Vector3(x + 3f, 5.8f, -21f));
                m.Box(Steel, new Vector3(x - 3.2f, 0f, -27.2f), new Vector3(x + 3.2f, 0.6f, -20.8f));
                for (var i = 0; i < 9; i++)
                {
                    m.Box(Steel, new Vector3(x - 3.6f, 0.9f, -26.5f + (i * 0.6f)), new Vector3(x - 3f, 5.2f, -26.2f + (i * 0.6f)));
                }

                for (var i = 0; i < 3; i++)
                {
                    m.Frustum(Stone, new Vector3(x - 1.8f + (i * 1.8f), 5.8f, -24f), 0.35f, 0.2f, 2.2f, 10);
                }
            }

            foreach (var x in new[] { 14f, 24f, 34f })
            {
                m.Box(Steel, new Vector3(x - 0.25f, 0f, -17.3f), new Vector3(x + 0.25f, 14f, -16.8f));
            }

            m.Box(Steel, new Vector3(13.75f, 13f, -17.2f), new Vector3(34.25f, 13.4f, -16.9f));
            m.Box(Steel, new Vector3(13.75f, 10.2f, -17.2f), new Vector3(34.25f, 10.5f, -16.9f));
            for (var x = 16f; x < 34f; x += 4.5f)
            {
                m.Frustum(Stone, new Vector3(x, 10.5f, -17.05f), 0.18f, 0.18f, 2.5f, 8);
            }
        }
    }
}
