using System.Collections.Generic;
using Cities.Core.Diagnostics;
using Cities.Gameplay.Cameras;
using Cities.Presentation.UI;
using Cities.Simulation.Scheduling;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Cities.Presentation.Debugging
{
    /// <summary>
    /// Debugpaneel (F3) en wereld-overlays (F4 voor het cellenraster; de rest via het paneel). Lijnen gaan als één
    /// lijnen-mesh door de gewone render-pipeline (URP), labels als UI Toolkit-elementen. Zolang het paneel open is,
    /// meet de scheduler de tijd per systeem.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class DebugOverlayBehaviour : MonoBehaviour
    {
        [SerializeField] private InputActionAsset _controls;
        [SerializeField] private SimulationHostBehaviour _simulation;
        [SerializeField] private UnityEngine.Camera _camera;
        [SerializeField] private bool _openOnStart;

        [Tooltip("Seconden tussen twee verversingen van het paneel.")]
        [SerializeField] private float _refreshInterval = 0.25f;

        private readonly DebugGeometry _geometry = new DebugGeometry();
        private readonly List<Label> _labels = new List<Label>();
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Color32> _colors = new List<Color32>();
        private readonly List<int> _indices = new List<int>();
        private Mesh _lineMesh;
        private DebugRegistry _registry;
        private UIDocument _document;
        private VisualElement _labelLayer;
        private Material _lineMaterial;
        private CityCameraBehaviour _cityCamera;
        private InputAction _togglePanel;
        private InputAction _toggleCells;
        private float _nextRefresh;

        public DebugPanel Panel { get; private set; }

        public bool IsPanelOpen => Panel != null && Panel.style.display == DisplayStyle.Flex;

        /// <summary>Inwoners voor het budget; komt later uit de bevolkingssimulatie (E07).</summary>
        public long Population { get; set; }

        public void SetPanelOpen(bool open)
        {
            if (Panel == null)
            {
                return;
            }

            Panel.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (_simulation != null && _simulation.Scheduler != null)
            {
                _simulation.Scheduler.MeasureSystems = open;
                if (!open)
                {
                    _simulation.Scheduler.Timings.Clear();
                }
            }
        }

        private void Awake()
        {
            _registry = DebugRegistry.Shared;
            _document = GetComponent<UIDocument>();
            if (_camera == null)
            {
                _camera = UnityEngine.Camera.main;
            }

            _cityCamera = _camera != null ? _camera.GetComponent<CityCameraBehaviour>() : null;
            var map = _controls.FindActionMap("Debug", throwIfNotFound: true);
            _togglePanel = map.FindAction("TogglePanel", true);
            _toggleCells = map.FindAction("ToggleCells", true);

            // Ingebouwde shader die in elke build zit; vertexkleuren, geen belichting, en de lijnen verdwijnen achter terrein.
            _lineMaterial = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
            _lineMaterial.SetInt("_ZWrite", 0);
            _lineMaterial.SetInt("_Cull", (int)CullMode.Off);
            _lineMaterial.SetInt("_ZTest", (int)CompareFunction.LessEqual);
            _lineMesh = new Mesh { name = "Debug-overlay", indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.HideAndDontSave };
            _lineMesh.MarkDynamic();
        }

        private void OnEnable()
        {
            var root = _document.rootVisualElement;
            root.AddToClassList(UiClasses.Root);
            _labelLayer = new VisualElement { name = "debug-labels", pickingMode = PickingMode.Ignore };
            _labelLayer.style.position = Position.Absolute;
            _labelLayer.style.left = 0;
            _labelLayer.style.top = 0;
            _labelLayer.style.right = 0;
            _labelLayer.style.bottom = 0;
            root.Add(_labelLayer);

            Panel = new DebugPanel(_registry);
            Panel.style.position = Position.Absolute;
            Panel.style.left = 16;
            Panel.style.top = 16;
            Panel.CloseRequested += ClosePanel;
            root.Add(Panel);
            SetPanelOpen(_openOnStart);

            _togglePanel.performed += OnTogglePanel;
            _toggleCells.performed += OnToggleCells;
            _controls.FindActionMap("Debug", true).Enable();
        }

        private void OnDisable()
        {
            _togglePanel.performed -= OnTogglePanel;
            _toggleCells.performed -= OnToggleCells;
            SetPanelOpen(false);
            if (Panel != null)
            {
                Panel.CloseRequested -= ClosePanel;
                Panel.Dispose();
                Panel.RemoveFromHierarchy();
                Panel = null;
            }

            _labelLayer?.RemoveFromHierarchy();
            _labelLayer = null;
            _labels.Clear();
        }

        private void OnDestroy()
        {
            if (_lineMaterial != null)
            {
                Destroy(_lineMaterial);
            }

            if (_lineMesh != null)
            {
                Destroy(_lineMesh);
            }
        }

        private void Update()
        {
            if (IsPanelOpen && Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + _refreshInterval;
                Panel.Refresh(_simulation != null ? _simulation.Scheduler?.Timings : null, Population);
            }
        }

        private void LateUpdate()
        {
            if (_camera == null)
            {
                return;
            }

            _registry.Collect(_geometry, CreateView(), (overlay, exception) =>
                Debug.LogError($"Debug-overlay {overlay.Name} faalde: {exception.Message}"));
            UpdateLabels();
            DrawLines();
        }

        private DebugView CreateView()
        {
            float3 position = _camera.transform.position;
            var focus = _cityCamera != null && _cityCamera.Controller != null
                ? _cityCamera.Controller.Current.Pivot
                : GroundPointAhead(position, _camera.transform.forward);
            var altitude = math.max(1f, position.y - focus.y);
            return new DebugView(position, focus, math.clamp(altitude * 2f, 200f, 4000f));
        }

        /// <summary>Snijpunt van de kijkrichting met het grondvlak (y = 0), of het punt recht onder de camera.</summary>
        private static float3 GroundPointAhead(float3 position, float3 forward)
        {
            if (forward.y >= -0.01f)
            {
                return new float3(position.x, 0f, position.z);
            }

            var distance = -position.y / forward.y;
            return position + (forward * distance);
        }

        private void UpdateLabels()
        {
            var labels = _geometry.Labels;
            while (_labels.Count < labels.Count)
            {
                var label = new Label { pickingMode = PickingMode.Ignore };
                label.AddToClassList(UiClasses.DebugLabel);
                _labelLayer.Add(label);
                _labels.Add(label);
            }

            var panel = _labelLayer.panel;
            for (var i = 0; i < _labels.Count; i++)
            {
                var label = _labels[i];
                var visible = i < labels.Count && panel != null;
                Vector3 screen = default;
                if (visible)
                {
                    screen = _camera.WorldToScreenPoint(labels[i].Position);
                    visible = screen.z > 0f;
                }

                label.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (!visible)
                {
                    continue;
                }

                var point = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
                label.text = labels[i].Text;
                label.style.left = point.x;
                label.style.top = point.y;
                label.style.color = ToColor(labels[i].Color);
            }
        }

        /// <summary>Zet alle lijnen in één mesh met MeshTopology.Lines en laat de camera die deze frame tekenen.</summary>
        private void DrawLines()
        {
            var lines = _geometry.Lines;
            if (lines.Count == 0)
            {
                return;
            }

            _vertices.Clear();
            _colors.Clear();
            _indices.Clear();
            foreach (var line in lines)
            {
                Color32 color = ToColor(line.Color);
                _indices.Add(_vertices.Count);
                _vertices.Add(line.From);
                _colors.Add(color);
                _indices.Add(_vertices.Count);
                _vertices.Add(line.To);
                _colors.Add(color);
            }

            _lineMesh.Clear();
            _lineMesh.SetVertices(_vertices);
            _lineMesh.SetColors(_colors);
            _lineMesh.SetIndices(_indices, MeshTopology.Lines, 0, calculateBounds: true);
            var parameters = new RenderParams(_lineMaterial)
            {
                camera = _camera,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = _lineMesh.bounds,
            };
            Graphics.RenderMesh(parameters, _lineMesh, 0, Matrix4x4.identity);
        }

        /// <summary>Kleuren uit Tokens.uss, zodat overlay en UI hetzelfde betekenen.</summary>
        private static Color ToColor(DebugColor color)
        {
            switch (color)
            {
                case DebugColor.Accent: return new Color32(52, 186, 164, 255);
                case DebugColor.Good: return new Color32(98, 186, 96, 255);
                case DebugColor.Warning: return new Color32(236, 172, 74, 255);
                case DebugColor.Bad: return new Color32(219, 82, 72, 255);
                case DebugColor.Info: return new Color32(92, 160, 230, 200);
                default: return new Color32(232, 236, 241, 110);
            }
        }

        private void OnTogglePanel(InputAction.CallbackContext context) => SetPanelOpen(!IsPanelOpen);

        private void OnToggleCells(InputAction.CallbackContext context) => _registry.Toggle(Cities.World.Grid.CellGridOverlay.OverlayName);

        private void ClosePanel() => SetPanelOpen(false);
    }
}
