using System.Linq;
using Cities.Gameplay.Cameras;
using Cities.Gameplay.Diagnostics;
using Cities.Gameplay.Persistence;
using Cities.Presentation.Cheats;
using Cities.Presentation.Overlays;
using Cities.Presentation.Screenshots;
using Cities.Simulation.Network;
using Cities.Simulation.Scheduling;
using Cities.World.Grid;
using Cities.World.Water;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cities.Editor.Build
{
    /// <summary>
    /// Bouwt de speelbare hoofdscène <c>Assets/Scenes/Main.unity</c>: de volledigste testscène (Fauna: terrein, water, bomen,
    /// wegen, zones, gereedschap, UI) plus opslaan, overlays, console en logging, en zet die als eerste scène in de build.
    /// </summary>
    public static class MainSceneBuilder
    {
        public const string SourcePath = "Assets/Scenes/Tests/Fauna.unity";
        public const string MainPath = "Assets/Scenes/Main.unity";

        [MenuItem("Cities/Bouw hoofdscène")]
        public static void Build()
        {
            AssetDatabase.DeleteAsset(MainPath);
            AssetDatabase.CopyAsset(SourcePath, MainPath);
            var scene = EditorSceneManager.OpenScene(MainPath, OpenSceneMode.Single);

            var simulation = Object.FindAnyObjectByType<SimulationHostBehaviour>();
            var world = Object.FindAnyObjectByType<WorldMapHostBehaviour>();
            var network = Object.FindAnyObjectByType<RoadNetworkHostBehaviour>();
            var water = Object.FindAnyObjectByType<WaterHostBehaviour>();
            var city = Object.FindAnyObjectByType<CityCameraBehaviour>();
            var controls = Object.FindAnyObjectByType<Cities.Gameplay.Controls.ControlBindingsBehaviour>();
            var panelSettings = Object.FindObjectsByType<UIDocument>().Select(d => d.panelSettings).FirstOrDefault(p => p != null);

            var save = new GameObject("Save").AddComponent<SaveGameBehaviour>();
            Wire(save, ("_simulation", simulation), ("_world", world), ("_network", network), ("_water", water), ("_camera", city));
            Wire(save, ("_slot", "Stad"));

            var overlays = NewUi("Overlay UI", panelSettings);
            overlays.AddComponent<OverlayBehaviour>();
            Wire(overlays.GetComponent<OverlayBehaviour>(), ("_world", world));

            var console = NewUi("Console UI", panelSettings);
            console.AddComponent<ConsoleBehaviour>();
            Wire(console.GetComponent<ConsoleBehaviour>(), ("_simulation", simulation), ("_camera", city));

            var tools = Object.FindAnyObjectByType<Cities.Gameplay.Tools.ToolsBehaviour>();
            var menu = NewUi("Menu UI", panelSettings).AddComponent<Cities.Presentation.UI.MenuBehaviour>();
            Wire(menu, ("_simulation", simulation), ("_save", save), ("_tools", tools), ("_controls", controls != null ? ReadControls(controls) : null));
            NewUi("Laadscherm", panelSettings).AddComponent<Cities.Presentation.UI.LoadingScreenBehaviour>();
            new GameObject("Underground view").AddComponent<UndergroundViewBehaviour>();
            new GameObject("Log file").AddComponent<LogFileBehaviour>();
            var telemetry = new GameObject("Telemetry").AddComponent<TelemetryBehaviour>();
            Wire(telemetry, ("_host", simulation));

            var shots = new GameObject("Screenshots").AddComponent<ScreenshotBehaviour>();
            Wire(shots, ("_controls", controls != null ? ReadControls(controls) : null), ("_camera", city));

            BuildUtilities(simulation);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainPath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Hoofdscène gebouwd: " + MainPath);
        }

        /// <summary>E26-03: nutsnetten in de scene, met vier plaatsbare voorzieningen in de werkbalk.</summary>
        private static void BuildUtilities(SimulationHostBehaviour simulation)
        {
            var spawner = Object.FindAnyObjectByType<Cities.Gameplay.Zoning.BuildingSpawnerBehaviour>();
            var template = Object.FindAnyObjectByType<Cities.Gameplay.Tools.PrefabPlacementBehaviour>();
            var toolbar = Object.FindAnyObjectByType<Cities.Presentation.Tools.ToolbarBehaviour>();
            if (template == null)
            {
                Debug.LogWarning("Geen PrefabPlacementBehaviour in de bronscène; nutsvoorzieningen niet geplaatst.");
                return;
            }

            var source = new SerializedObject(template);
            var host = new GameObject("Nutsvoorzieningen");
            var utilities = host.AddComponent<Cities.Gameplay.Utilities.UtilitiesHostBehaviour>();
            var kinds = new[]
            {
                (Cities.Gameplay.Utilities.PlantKind.PowerPlant, "Centrale", 4, new Color(0.9f, 0.75f, 0.2f)),
                (Cities.Gameplay.Utilities.PlantKind.WaterPump, "Waterpomp", 2, new Color(0.25f, 0.55f, 0.9f)),
                (Cities.Gameplay.Utilities.PlantKind.SewagePlant, "Rioolzuivering", 3, new Color(0.45f, 0.6f, 0.35f)),
                (Cities.Gameplay.Utilities.PlantKind.Landfill, "Stortplaats", 4, new Color(0.5f, 0.4f, 0.3f)),
            };
            var placements = new Cities.Gameplay.Tools.PrefabPlacementBehaviour[kinds.Length];
            for (var i = 0; i < kinds.Length; i++)
            {
                var (kind, name, cells, color) = kinds[i];
                var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                prefab.name = "Prefab " + name;
                prefab.transform.SetParent(host.transform);
                prefab.transform.localScale = new Vector3(cells * 8f - 2f, 10f, cells * 8f - 2f);
                var renderer = prefab.GetComponent<Renderer>();
                renderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
                Object.DestroyImmediate(prefab.GetComponent<Collider>());
                var holder = new GameObject("Plaatsing " + name);
                holder.transform.SetParent(host.transform);
                var placement = holder.AddComponent<Cities.Gameplay.Tools.PrefabPlacementBehaviour>();
                CopyReference(source, placement, "_tools");
                CopyReference(source, placement, "_world");
                CopyReference(source, placement, "_roads");
                Wire(placement, ("_prefab", prefab), ("_toolName", name), ("_width", cells), ("_depth", cells));
                prefab.SetActive(false);
                placements[i] = placement;
            }

            var so = new SerializedObject(utilities);
            so.FindProperty("_simulation").objectReferenceValue = simulation;
            so.FindProperty("_spawner").objectReferenceValue = spawner;
            var plants = so.FindProperty("_plants");
            plants.arraySize = kinds.Length;
            for (var i = 0; i < kinds.Length; i++)
            {
                var element = plants.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Kind").enumValueIndex = (int)kinds[i].Item1;
                element.FindPropertyRelative("Placement").objectReferenceValue = placements[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            if (toolbar != null)
            {
                AppendPlacements(toolbar, placements);
            }
        }

        private static void AppendPlacements(Object toolbar, Object[] added)
        {
            var so = new SerializedObject(toolbar);
            var list = so.FindProperty("_placements");
            var start = list.arraySize;
            list.arraySize = start + added.Length;
            for (var i = 0; i < added.Length; i++)
            {
                list.GetArrayElementAtIndex(start + i).objectReferenceValue = added[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CopyReference(SerializedObject source, Object target, string field)
        {
            var property = source.FindProperty(field);
            if (property != null && property.objectReferenceValue != null)
            {
                Wire(target, (field, property.objectReferenceValue));
            }
        }

        private static GameObject NewUi(string name, PanelSettings panelSettings)
        {
            var go = new GameObject(name);
            var document = go.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            return go;
        }

        private static Object ReadControls(Object bindings)
        {
            var property = new SerializedObject(bindings).GetIterator();
            while (property.NextVisible(true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null && property.objectReferenceValue.GetType().Name == "InputActionAsset")
                {
                    return property.objectReferenceValue;
                }
            }

            return null;
        }

        private static void Wire(Object target, params (string field, object value)[] values)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in values)
            {
                var property = so.FindProperty(field);
                if (property == null)
                {
                    Debug.LogWarning($"{target.GetType().Name}.{field} bestaat niet.");
                    continue;
                }

                switch (value)
                {
                    case string text: property.stringValue = text; break;
                    case int number: property.intValue = number; break;
                    case float number: property.floatValue = number; break;
                    case bool flag: property.boolValue = flag; break;
                    case Object reference: property.objectReferenceValue = reference; break;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
