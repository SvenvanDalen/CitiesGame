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

        public WorldMap Map { get; private set; }

        private void Awake() => Map = new WorldMap(WorldDimensions.Default, _baseHeight);

        private void OnDestroy()
        {
            Map?.Dispose();
            Map = null;
        }
    }
}
