using Cities.Core.Diagnostics;
using Cities.World.Grid;
using Unity.Jobs;
using UnityEngine;

namespace Cities.World.Terrain
{
    /// <summary>
    /// Vult bij het starten de hoogtekaart van de <see cref="WorldMapHostBehaviour"/> met een procedureel landschap.
    /// Een geladen stad overschrijft de hoogtes daarna weer met die uit de save.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-900)]
    public sealed class TerrainGeneratorBehaviour : MonoBehaviour
    {
        private static readonly SystemLog Log = new SystemLog("Terrain");

        [SerializeField] private WorldMapHostBehaviour _world;
        [SerializeField] private TerrainShapeSettings _settings = TerrainShapeSettings.Default;

        private void Awake()
        {
            var map = _world.Map;
            var heights = map.Height.Values;
            var layout = map.Height.Layout;
            var started = Time.realtimeSinceStartupAsDouble;
            new TerrainGenerateJob
            {
                Settings = _settings,
                Width = layout.Width,
                Spacing = layout.Spacing,
                Heights = heights,
            }.Schedule(layout.Depth, 8).Complete();

            var range = map.Height.Range();
            Log.Info("Terrein gemaakt in {0:0} ms, hoogte {1}", (Time.realtimeSinceStartupAsDouble - started) * 1000.0, $"{range.Min:0} tot {range.Max:0} m");
            map.NotifyHeightChanged(map.AllCorners);
        }
    }
}
