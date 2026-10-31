using System.Collections.Generic;
using Cities.Core.Diagnostics;
using Cities.World.Grid;
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

        private readonly List<System.IDisposable> _overlays = new List<System.IDisposable>();

        private void Awake()
        {
            var map = _world.Map;
            var started = Time.realtimeSinceStartupAsDouble;
            TerrainGenerator.Generate(map, _settings);
            var range = map.Height.Range();
            Log.Info("Terrein gemaakt in {0:0} ms, hoogte {1}", (Time.realtimeSinceStartupAsDouble - started) * 1000.0, $"{range.Min:0} tot {range.Max:0} m");
            RegisterResourceOverlays(map);
        }

        private void OnDestroy()
        {
            foreach (var overlay in _overlays)
            {
                overlay.Dispose();
            }

            _overlays.Clear();
        }

        /// <summary>Een overlay per grondstoflaag (E03-08), aan te zetten in het debugpaneel; een tweede generator in dezelfde scene voegt ze niet dubbel toe.</summary>
        private void RegisterResourceOverlays(WorldMap map)
        {
            var registry = DebugRegistry.Shared;
            var terrain = new WorldMapTerrain(map);
            foreach (var (kind, name, color) in new[]
            {
                (ResourceKind.Oil, "Olie", DebugColor.Neutral),
                (ResourceKind.Ore, "Erts", DebugColor.Warning),
                (ResourceKind.FertileGround, "Vruchtbaar", DebugColor.Good),
                (ResourceKind.Forest, "Bos", DebugColor.Info),
            })
            {
                if (registry.Find(name) == null)
                {
                    _overlays.Add(registry.AddOverlay(new ResourceOverlay(kind, name, color, _settings, terrain)));
                }
            }
        }
    }
}
