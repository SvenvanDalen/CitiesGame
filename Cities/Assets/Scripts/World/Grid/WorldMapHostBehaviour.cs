using UnityEngine;

namespace Cities.World.Grid
{
    /// <summary>Eigenaar van de <see cref="WorldMap"/> in een scene: maakt hem bij het starten en ruimt hem op.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public sealed class WorldMapHostBehaviour : MonoBehaviour
    {
        [Tooltip("Hoogte van het lege terrein in meters.")]
        [SerializeField] private float _baseHeight;

        private System.IDisposable _overlay;

        public WorldMap Map { get; private set; }

        private void Awake()
        {
            Map = new WorldMap(WorldDimensions.Default, _baseHeight);
            // Eén raster tegelijk; een tweede kaart in dezelfde scene (tests) tekent geen eigen raster.
            var registry = Cities.Core.Diagnostics.DebugRegistry.Shared;
            if (registry.Find(CellGridOverlay.OverlayName) == null)
            {
                _overlay = registry.AddOverlay(new CellGridOverlay(Map.Dimensions, new Cities.World.Terrain.WorldMapTerrain(Map)));
            }
        }

        private void OnDestroy()
        {
            _overlay?.Dispose();
            _overlay = null;
            Map?.Dispose();
            Map = null;
        }
    }
}
