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

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainPath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Hoofdscène gebouwd: " + MainPath);
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
                    case Object reference: property.objectReferenceValue = reference; break;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
