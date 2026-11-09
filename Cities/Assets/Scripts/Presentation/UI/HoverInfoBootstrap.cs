using Cities.Gameplay.Tools;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Cities.Presentation.UI
{
    /// <summary>Zet de <see cref="HoverInfoBehaviour"/> in elke scène met gereedschappen, zodat de hoofdscène er niet voor hoeft te veranderen (E27-10).</summary>
    public static class HoverInfoBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init() => SceneManager.sceneLoaded += (scene, _) => Spawn();

        private static void Spawn()
        {
            var roads = Object.FindAnyObjectByType<RoadToolBehaviour>();
            var hud = Object.FindAnyObjectByType<HudBehaviour>();
            var tools = Object.FindAnyObjectByType<ToolsBehaviour>();
            if (roads == null || hud == null || tools == null || Object.FindAnyObjectByType<HoverInfoBehaviour>() != null)
            {
                return;
            }

            var host = new GameObject("Hover info");
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = hud.GetComponent<UIDocument>().panelSettings;
            document.sortingOrder = 50;
            host.AddComponent<HoverInfoBehaviour>().Setup(tools, roads);
        }
    }
}
