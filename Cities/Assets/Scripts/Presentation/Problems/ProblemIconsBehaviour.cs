using System;
using System.Collections.Generic;
using Cities.Simulation.Problems;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cities.Presentation.Problems
{
    /// <summary>
    /// Tekent de probleemiconen (E17-03) als camera-gerichte vlakjes boven gebouwen met een zichtbaar probleem uit de
    /// <see cref="ProblemTracker"/>. Alleen gebouwen dichtbij de camera, met een maximum aantal; één instanced draw per soort.
    /// Systemen melden via <see cref="Tracker"/> en leggen met <see cref="SetRoof"/> vast waar het dak van een gebouw ligt.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProblemIconsBehaviour : MonoBehaviour, Cities.Gameplay.Services.IProblemIcons
    {
        private const int BatchSize = 1023;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private UnityEngine.Camera _camera;

        [Tooltip("Ongelit materiaal met een _BaseColor, bijvoorbeeld URP Unlit; leeg gebruikt URP Unlit.")]
        [SerializeField] private Material _material;

        [SerializeField, Min(1f)] private float _iconSize = 6f;
        [SerializeField, Min(0f)] private float _lift = 6f;
        [SerializeField, Min(1f)] private float _maxDistance = 400f;
        [SerializeField, Min(1)] private int _maxIcons = 400;
        [SerializeField, Min(1)] private int _showAfter = 3;
        [SerializeField, Min(1)] private int _hideAfter = 5;

        private readonly Dictionary<int, float3> _roofs = new Dictionary<int, float3>();
        private readonly List<KeyValuePair<int, ProblemSet>> _visible = new List<KeyValuePair<int, ProblemSet>>();
        private readonly List<ProblemIcon> _icons = new List<ProblemIcon>();
        private readonly List<Matrix4x4>[] _perKind = new List<Matrix4x4>[ProblemSet.KindCount];
        private readonly RenderParams[] _params = new RenderParams[ProblemSet.KindCount];
        private readonly Matrix4x4[] _batch = new Matrix4x4[BatchSize];
        private Mesh _quad;
        private Material _owned;

        /// <summary>De tracker waar de simulatiesystemen hun problemen aan melden.</summary>
        public ProblemTracker Tracker { get; private set; }

        /// <summary>Iconen in het laatste frame.</summary>
        public int LastDrawn => _icons.Count;

        /// <summary>Legt de dakpositie (wereld) van een gebouw vast; zonder positie wordt er geen icoon getekend.</summary>
        public void SetRoof(int building, float3 roof) => _roofs[building] = roof;

        public void ClearBuilding(int building)
        {
            _roofs.Remove(building);
            Tracker.Remove(building);
        }

        private static Color ColorOf(BuildingProblem problem) => problem switch
        {
            BuildingProblem.Fire => new Color(0.95f, 0.25f, 0.1f),
            BuildingProblem.NoPower => new Color(1f, 0.85f, 0.1f),
            BuildingProblem.NoWater => new Color(0.2f, 0.55f, 1f),
            BuildingProblem.GarbageFull => new Color(0.55f, 0.45f, 0.3f),
            BuildingProblem.NoSewage => new Color(0.4f, 0.6f, 0.2f),
            _ => new Color(0.6f, 0.2f, 0.7f),
        };

        private void Awake()
        {
            if (_camera == null)
            {
                _camera = UnityEngine.Camera.main;
            }

            Tracker = new ProblemTracker(_showAfter, _hideAfter);
            _quad = BuildQuad();
            var baseMaterial = _material;
            if (baseMaterial == null)
            {
                baseMaterial = _owned = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            }

            for (var i = 0; i < _perKind.Length; i++)
            {
                _perKind[i] = new List<Matrix4x4>();
                var block = new MaterialPropertyBlock();
                block.SetColor(BaseColorId, ColorOf((BuildingProblem)i));
                _params[i] = new RenderParams(baseMaterial)
                {
                    matProps = block,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                    worldBounds = new Bounds(Vector3.zero, new Vector3(1e5f, 1e4f, 1e5f)),
                };
            }
        }

        private void OnDestroy()
        {
            DestroyImmediate(_quad);
            if (_owned != null)
            {
                DestroyImmediate(_owned);
            }
        }

        private void LateUpdate()
        {
            if (_camera == null)
            {
                return;
            }

            var cameraTransform = _camera.transform;
            Tracker.CollectVisible(_visible);
            var view = new ProblemIconView(cameraTransform.position, cameraTransform.right, _iconSize, _lift, _maxDistance, _maxIcons);
            ProblemIconLayout.Build(_visible, RoofOf, view, _icons);
            foreach (var list in _perKind)
            {
                list.Clear();
            }

            var rotation = cameraTransform.rotation;
            var scale = Vector3.one * _iconSize;
            foreach (var icon in _icons)
            {
                _perKind[(int)icon.Problem].Add(Matrix4x4.TRS(new Vector3(icon.Position.x, icon.Position.y, icon.Position.z), rotation, scale));
            }

            for (var kind = 0; kind < _perKind.Length; kind++)
            {
                var list = _perKind[kind];
                for (var start = 0; start < list.Count; start += BatchSize)
                {
                    var count = Math.Min(BatchSize, list.Count - start);
                    list.CopyTo(start, _batch, 0, count);
                    Graphics.RenderMeshInstanced(_params[kind], _quad, 0, _batch, count);
                }
            }
        }

        private float3 RoofOf(int building) => _roofs.TryGetValue(building, out var roof) ? roof : new float3(float.MaxValue);

        private static Mesh BuildQuad()
        {
            var mesh = new Mesh
            {
                name = "Probleemicoon",
                vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) },
                triangles = new[] { 0, 2, 1, 0, 3, 2 },
            };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
