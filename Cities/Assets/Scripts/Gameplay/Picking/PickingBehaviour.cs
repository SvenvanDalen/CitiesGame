using System;
using System.Collections.Generic;
using Cities.Core.Diagnostics;
using Cities.Core.Spatial;
using Cities.Gameplay.Cameras;
using Cities.World.Grid;
using Cities.World.Picking;
using Cities.World.Terrain;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Cities.Gameplay.Picking
{
    /// <summary>
    /// Wijst elk frame aan wat er onder de muis ligt. Tools zetten <see cref="Mask"/> op de soorten die ze zoeken en
    /// lezen <see cref="Current"/>; systemen zetten hun objecten in de lagen van <see cref="BoxLayer"/>,
    /// <see cref="CapsuleLayer"/> en <see cref="AgentLayer"/>. Boven UI wordt niets aangewezen.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-500)]
    public sealed class PickingBehaviour : MonoBehaviour
    {
        [SerializeField] private InputActionAsset _controls;
        [SerializeField] private UnityEngine.Camera _camera;
        [SerializeField] private WorldMapHostBehaviour _world;

        [Tooltip("Colliders als terrein, voor scenes zonder hoogtekaart; anders het oppervlak van de WorldMap.")]
        [SerializeField] private bool _physicsTerrain;
        [SerializeField] private LayerMask _terrainLayers = ~0;

        [Tooltip("UI-documenten die de muis afvangen: boven een element daarvan wordt niets aangewezen.")]
        [SerializeField] private UIDocument[] _blockingDocuments = Array.Empty<UIDocument>();

        [SerializeField] private PickMask _mask = PickMask.All;

        [Tooltip("Zet de debug-overlay \"Picking\" aan bij het starten (testscenes).")]
        [SerializeField] private bool _showOverlay;

        private readonly Dictionary<PickKind, IPickLayer> _layers = new Dictionary<PickKind, IPickLayer>();
        private readonly List<IDisposable> _registrations = new List<IDisposable>();
        private InputAction _pointer;
        private WorldDimensions _dimensions;
        private IDisposable _overlay;

        public PickingService Service { get; private set; }

        /// <summary>Wat er nu onder de muis ligt.</summary>
        public PickResult Current { get; private set; }

        public PickMask Mask
        {
            get => _mask;
            set => _mask = value;
        }

        /// <summary>Als het aangewezen object (soort of ID) verandert.</summary>
        public event Action<PickResult> HoverChanged;

        public BoxPickLayer BoxLayer(PickKind kind = PickKind.Building) =>
            GetOrCreate(kind, () => new BoxPickLayer(Grid(32f), 4096, kind));

        public CapsulePickLayer CapsuleLayer(PickKind kind) =>
            GetOrCreate(kind, () => new CapsulePickLayer(kind, Grid(32f)));

        public AgentPickLayer AgentLayer(PickKind kind) =>
            GetOrCreate(kind, () => new AgentPickLayer(kind, Grid(32f)));

        /// <summary>Wijst aan langs een schermpositie (pixels, oorsprong linksonder).</summary>
        public PickResult PickAtScreen(Vector2 screenPosition)
        {
            var ray = _camera.ScreenPointToRay(screenPosition);
            return Service.Pick(new PickRay(ray.origin, ray.direction, _camera.farClipPlane), _mask);
        }

        private void Awake()
        {
            if (_camera == null)
            {
                _camera = UnityEngine.Camera.main;
            }

            _pointer = _controls.FindActionMap("Camera", throwIfNotFound: true).FindAction("Pointer", true);
            _dimensions = _world != null && _world.Map != null ? _world.Map.Dimensions : WorldDimensions.Default;
            ITerrainHeight terrain;
            if (_physicsTerrain)
            {
                terrain = new PhysicsSurfaceHeight(_terrainLayers);
            }
            else if (_world != null && _world.Map != null)
            {
                terrain = new WorldMapTerrain(_world.Map);
            }
            else
            {
                terrain = FlatTerrain.Zero;
            }

            Service = new PickingService();
            var terrainLayer = new TerrainPickLayer(terrain, new float2(_dimensions.SizeX, _dimensions.SizeZ));
            _layers.Add(PickKind.Terrain, terrainLayer);
            _registrations.Add(Service.AddLayer(terrainLayer));

            var registry = DebugRegistry.Shared;
            if (registry.Find(PickingOverlay.OverlayName) == null)
            {
                _overlay = registry.AddOverlay(new PickingOverlay(() => Current));
            }

            if (_showOverlay)
            {
                registry.SetEnabled(PickingOverlay.OverlayName, true);
            }
        }

        private void Update()
        {
            var previous = Current;
            Current = _camera != null && !IsPointerOverUi(_pointer.ReadValue<Vector2>())
                ? PickAtScreen(_pointer.ReadValue<Vector2>())
                : default;

            if (previous.Hit.Kind != Current.Hit.Kind || previous.Hit.Id != Current.Hit.Id)
            {
                HoverChanged?.Invoke(Current);
            }
        }

        private void OnDestroy()
        {
            _overlay?.Dispose();
            foreach (var registration in _registrations)
            {
                registration.Dispose();
            }

            foreach (var layer in _layers.Values)
            {
                (layer as IDisposable)?.Dispose();
            }

            _registrations.Clear();
            _layers.Clear();
        }

        private bool IsPointerOverUi(Vector2 screenPosition)
        {
            foreach (var document in _blockingDocuments)
            {
                var panel = document != null ? document.rootVisualElement?.panel : null;
                if (panel == null)
                {
                    continue;
                }

                var panelPosition = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
                var element = panel.Pick(panelPosition);
                if (element != null && element != document.rootVisualElement && document.rootVisualElement.Contains(element))
                {
                    return true;
                }
            }

            return false;
        }

        private TLayer GetOrCreate<TLayer>(PickKind kind, Func<TLayer> create) where TLayer : class, IPickLayer
        {
            if (_layers.TryGetValue(kind, out var existing))
            {
                return existing as TLayer ?? throw new InvalidOperationException($"Laag {kind} bestaat al als {existing.GetType().Name}.");
            }

            var layer = create();
            _layers.Add(kind, layer);
            _registrations.Add(Service.AddLayer(layer));
            return layer;
        }

        private GridGeometry Grid(float cellSize) =>
            GridGeometry.Covering(float2.zero, new float2(_dimensions.SizeX, _dimensions.SizeZ), cellSize);
    }
}
