using System.Collections.Generic;
using System.Diagnostics;
using Cities.Gameplay.Overlays;
using Cities.Presentation.UI;
using Cities.World.Grid;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Cities.Presentation.Overlays
{
    /// <summary>
    /// Toont de actieve overlay (E17-01). Een celoverlay wordt in stukken gebakken (<see cref="CellOverlayBaker"/>, hoogstens
    /// <c>_bakeBudgetMs</c> per frame) naar een textuur die de terreinshader als <c>_CityOverlay</c> leest, zodat wisselen niet
    /// haperen; een gebouw- of segmentoverlay wordt een lint-mesh in de kleur van de waarde. De legenda staat in het
    /// <see cref="OverlayPanel"/>. Andere systemen registreren hun bron via <see cref="Controller"/>.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class OverlayBehaviour : MonoBehaviour
    {
        private const int CellsPerChunk = 8192;

        private static readonly int OverlayId = Shader.PropertyToID("_CityOverlay");
        private static readonly int ParamsId = Shader.PropertyToID("_CityOverlayParams");

        [SerializeField] private WorldMapHostBehaviour _world;

        [Tooltip("Dekking van de celoverlay op het terrein, 0 tot 1.")]
        [SerializeField, Range(0f, 1f)] private float _opacity = 0.7f;

        [Tooltip("Hoogstens zoveel milliseconden per frame aan het bakken van een celoverlay.")]
        [SerializeField, Min(0.1f)] private float _bakeBudgetMs = 2f;

        [Tooltip("Registreer de overlays van de kaartlagen (hoogte, landwaarde, grondvervuiling, waterdiepte).")]
        [SerializeField] private bool _registerWorldLayers = true;

        private readonly CellOverlayBaker _baker = new CellOverlayBaker();
        private readonly List<OverlayEntityValue> _values = new List<OverlayEntityValue>();
        private readonly List<float3> _vertices = new List<float3>();
        private readonly List<uint> _packedColors = new List<uint>();
        private readonly List<int> _indices = new List<int>();
        private readonly List<Vector3> _meshVertices = new List<Vector3>();
        private readonly List<Color32> _meshColors = new List<Color32>();
        private readonly Stopwatch _clock = new Stopwatch();
        private OverlayController _controller;
        private OverlayPanel _panel;
        private Texture2D _texture;
        private Mesh _mesh;
        private Material _ribbonMaterial;
        private int _uploadedCount;
        private bool _hasRibbons;
        private UIDocument _document;
        private CityButton _toggle;

        public OverlayController Controller => _controller ??= new OverlayController();

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            _ribbonMaterial = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
            _ribbonMaterial.SetInt("_ZWrite", 0);
            _ribbonMaterial.SetInt("_Cull", (int)CullMode.Off);
            _ribbonMaterial.SetInt("_ZTest", (int)CompareFunction.LessEqual);
            _mesh = new Mesh { name = "Overlay-lint", indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.HideAndDontSave };
            _mesh.MarkDynamic();
            if (_registerWorldLayers && _world != null && _world.Map != null)
            {
                foreach (var source in WorldLayerOverlays.Create(_world.Map))
                {
                    Controller.Register(source);
                }
            }
        }

        private void OnEnable()
        {
            Shader.SetGlobalVector(ParamsId, Vector4.zero);
            var root = _document.rootVisualElement;
            root.AddToClassList(UiClasses.Root);
            _panel = new OverlayPanel(Controller);
            _panel.style.position = Position.Absolute;
            _panel.style.left = 16;
            _panel.style.bottom = 56;
            _panel.style.display = DisplayStyle.None;
            _panel.CloseRequested += HidePanel;
            root.Add(_panel);

            _toggle = new CityButton(OverlayText.Toggle, ButtonVariant.Secondary) { Small = true };
            _toggle.style.position = Position.Absolute;
            _toggle.style.left = 16;
            _toggle.style.bottom = 16;
            _toggle.clicked += TogglePanel;
            root.Add(_toggle);

            Controller.Changed += OnChanged;
            OnChanged();
        }

        private void OnDisable()
        {
            Controller.Changed -= OnChanged;
            Shader.SetGlobalVector(ParamsId, Vector4.zero);
            _baker.Cancel();
            _panel?.Dispose();
            _panel?.RemoveFromHierarchy();
            _panel = null;
            _toggle?.RemoveFromHierarchy();
            _toggle = null;
        }

        private void OnDestroy()
        {
            if (_texture != null)
            {
                Destroy(_texture);
            }

            if (_mesh != null)
            {
                Destroy(_mesh);
            }

            if (_ribbonMaterial != null)
            {
                Destroy(_ribbonMaterial);
            }
        }

        private void Update()
        {
            var active = Controller.Active;
            if (active != null && Controller.NeedsRefresh)
            {
                StartRefresh(active);
            }

            StepBake();
            if (_hasRibbons)
            {
                Graphics.RenderMesh(new RenderParams(_ribbonMaterial) { layer = gameObject.layer }, _mesh, 0, Matrix4x4.identity);
            }
        }

        private void HidePanel() => _panel.style.display = DisplayStyle.None;

        private void TogglePanel() =>
            _panel.style.display = _panel.style.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;

        private void OnChanged()
        {
            // Een andere overlay: de vorige textuur blijft staan tot de nieuwe klaar is, zodat er niets knippert.
            var active = Controller.Active;
            if (active == null)
            {
                _baker.Cancel();
                Shader.SetGlobalVector(ParamsId, Vector4.zero);
                ClearRibbons();
                return;
            }

            StartRefresh(active);
        }

        private void StartRefresh(IOverlaySource source)
        {
            Controller.MarkShown();
            switch (source)
            {
                case ICellOverlaySource cells:
                    ClearRibbons();
                    _baker.Begin(cells);
                    break;
                case IEntityOverlaySource entities:
                    _baker.Cancel();
                    Shader.SetGlobalVector(ParamsId, Vector4.zero);
                    BuildRibbons(entities);
                    break;
            }
        }

        private void StepBake()
        {
            if (!_baker.IsBaking)
            {
                return;
            }

            _clock.Restart();
            bool finished;
            do
            {
                finished = _baker.Step(CellsPerChunk);
            }
            while (!finished && _clock.Elapsed.TotalMilliseconds < _bakeBudgetMs);

            if (_baker.CompletedCount != _uploadedCount)
            {
                _uploadedCount = _baker.CompletedCount;
                Upload();
            }
        }

        private void Upload()
        {
            if (_texture == null || _texture.width != _baker.Width || _texture.height != _baker.Height)
            {
                if (_texture != null)
                {
                    Destroy(_texture);
                }

                _texture = new Texture2D(_baker.Width, _baker.Height, TextureFormat.RGBA32, false)
                {
                    name = "Overlay",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };
            }

            _texture.SetPixelData(_baker.Front, 0);
            _texture.Apply(false);
            Shader.SetGlobalTexture(OverlayId, _texture);
            Shader.SetGlobalVector(ParamsId, new Vector4(_opacity, 0f, 0f, 0f));
        }

        private void BuildRibbons(IEntityOverlaySource source)
        {
            _values.Clear();
            source.Collect(_values);
            OverlayRibbonBuilder.Build(_values, source.Legend, OverlayRibbonBuilder.DefaultLift, _vertices, _packedColors, _indices);
            _meshVertices.Clear();
            _meshColors.Clear();
            foreach (var v in _vertices)
            {
                _meshVertices.Add(new Vector3(v.x, v.y, v.z));
            }

            foreach (var packed in _packedColors)
            {
                _meshColors.Add(new Color32((byte)packed, (byte)(packed >> 8), (byte)(packed >> 16), (byte)(packed >> 24)));
            }

            _mesh.Clear();
            _mesh.SetVertices(_meshVertices);
            _mesh.SetColors(_meshColors);
            _mesh.SetIndices(_indices, MeshTopology.Triangles, 0);
            _hasRibbons = _indices.Count > 0;
        }

        private void ClearRibbons()
        {
            _hasRibbons = false;
            _mesh.Clear();
        }
    }
}
