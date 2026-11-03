using System;
using System.Collections.Generic;
using Cities.World.Fauna;
using Cities.World.Grid;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cities.Presentation.Fauna
{
    /// <summary>
    /// Tekent vogels, vissen en herten als sfeerleven. De dieren zijn geen simulatie: <see cref="FaunaMotion"/> rekent
    /// hun stand elk frame uit de tijd, alleen voor de groepen rond de camera, en er wordt niets opgeslagen. Elk
    /// model gaat in batches met GPU-instancing de lucht, het water of het veld in.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FaunaRendererBehaviour : MonoBehaviour
    {
        private const int BatchSize = 1023;
        private const int MaxInstances = 6000;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private WorldMapHostBehaviour _world;
        [SerializeField] private UnityEngine.Camera _camera;

        [Tooltip("Materiaal met een _BaseColor (URP Lit); leeg gebruikt URP Lit.")]
        [SerializeField] private Material _material;

        [Tooltip("Wereldseed: dezelfde kaart en seed geven dezelfde dieren.")]
        [SerializeField] private int _seed = 1;

        [SerializeField, Min(0f)] private float _birdDistance = 1800f;
        [SerializeField, Min(0f)] private float _fishDistance = 120f;
        [SerializeField, Min(0f)] private float _deerDistance = 600f;

        private readonly Dictionary<long, FaunaGroup[]> _cache = new Dictionary<long, FaunaGroup[]>();
        private readonly List<FaunaGroup> _scratch = new List<FaunaGroup>();
        private readonly List<Matrix4x4> _gulls = new List<Matrix4x4>();
        private readonly List<Matrix4x4> _crows = new List<Matrix4x4>();
        private readonly List<Matrix4x4> _gullWings = new List<Matrix4x4>();
        private readonly List<Matrix4x4> _crowWings = new List<Matrix4x4>();
        private readonly List<Matrix4x4> _fish = new List<Matrix4x4>();
        private readonly List<Matrix4x4> _deer = new List<Matrix4x4>();
        private readonly Matrix4x4[] _batch = new Matrix4x4[BatchSize];
        private WorldMap _map;
        private IFaunaHabitat _habitat;
        private Mesh _birdMesh;
        private Mesh _wingMesh;
        private Mesh _fishMesh;
        private Mesh _deerMesh;
        private RenderParams _gullParams;
        private RenderParams _crowParams;
        private RenderParams _fishParams;
        private RenderParams _deerParams;
        private Material _owned;

        /// <summary>Dieren in het laatste frame: vogels, vissen, herten.</summary>
        public int3 LastDrawn { get; private set; }

        private void Awake()
        {
            if (_camera == null)
            {
                _camera = UnityEngine.Camera.main;
            }

            _map = _world.Map;
            _habitat = new WorldMapHabitat(_map);
            _birdMesh = ToMesh(FaunaShapes.BirdBody(), "Vogel");
            _wingMesh = ToMesh(FaunaShapes.BirdWing(), "Vleugel");
            _fishMesh = ToMesh(FaunaShapes.Fish(), "Vis");
            _deerMesh = ToMesh(FaunaShapes.Deer(), "Ree");
            var baseMaterial = _material;
            if (baseMaterial == null)
            {
                baseMaterial = _owned = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            }

            _gullParams = Params(baseMaterial, new Color(0.9f, 0.91f, 0.92f), ShadowCastingMode.Off);
            _crowParams = Params(baseMaterial, new Color(0.06f, 0.06f, 0.07f), ShadowCastingMode.Off);
            _fishParams = Params(baseMaterial, new Color(0.55f, 0.58f, 0.5f), ShadowCastingMode.Off);
            _deerParams = Params(baseMaterial, new Color(0.42f, 0.27f, 0.15f), ShadowCastingMode.On);
            _map.HeightChanged += OnMapChanged;
            _map.GroundCoverChanged += OnMapChanged;
            _map.TreesChanged += OnMapChanged;
        }

        private void OnDestroy()
        {
            if (_map != null)
            {
                _map.HeightChanged -= OnMapChanged;
                _map.GroundCoverChanged -= OnMapChanged;
                _map.TreesChanged -= OnMapChanged;
            }

            DestroyImmediate(_birdMesh);
            DestroyImmediate(_wingMesh);
            DestroyImmediate(_fishMesh);
            DestroyImmediate(_deerMesh);
            if (_owned != null)
            {
                DestroyImmediate(_owned);
            }
        }

        private void LateUpdate()
        {
            if (_map == null || _map.IsDisposed || _camera == null)
            {
                return;
            }

            var eye = _camera.transform.position;
            var center = new float2(eye.x, eye.z);
            var time = Time.time;
            _gulls.Clear();
            _crows.Clear();
            _gullWings.Clear();
            _crowWings.Clear();
            _fish.Clear();
            _deer.Clear();
            Collect(FaunaKind.Birds, center, _birdDistance, time);
            Collect(FaunaKind.Fish, center, _fishDistance, time);
            Collect(FaunaKind.Deer, center, _deerDistance, time);
            Draw(_birdMesh, _gullParams, _gulls);
            Draw(_birdMesh, _crowParams, _crows);
            Draw(_wingMesh, _gullParams, _gullWings);
            Draw(_wingMesh, _crowParams, _crowWings);
            Draw(_fishMesh, _fishParams, _fish);
            Draw(_deerMesh, _deerParams, _deer);
            LastDrawn = new int3(_gulls.Count + _crows.Count, _fish.Count, _deer.Count);
        }

        private void OnMapChanged(RectInt _) => _cache.Clear();

        private void Collect(FaunaKind kind, float2 center, float distance, float time)
        {
            var minX = (int)math.floor((center.x - distance) / FaunaGroups.PatchSize);
            var maxX = (int)math.floor((center.x + distance) / FaunaGroups.PatchSize);
            var minZ = (int)math.floor((center.y - distance) / FaunaGroups.PatchSize);
            var maxZ = (int)math.floor((center.y + distance) / FaunaGroups.PatchSize);
            for (var x = math.max(minX, 0); x <= maxX; x++)
            {
                for (var z = math.max(minZ, 0); z <= maxZ; z++)
                {
                    var groups = GroupsOf(kind, x, z);
                    for (var i = 0; i < groups.Length; i++)
                    {
                        if (math.distance(groups[i].Anchor, center) <= distance + FaunaGroups.Reach(groups[i]))
                        {
                            Pose(groups[i], center, distance, time);
                        }
                    }
                }
            }
        }

        private void Pose(in FaunaGroup group, float2 center, float distance, float time)
        {
            for (var member = 0; member < group.Count; member++)
            {
                if (_gulls.Count + _crows.Count + _fish.Count + _deer.Count >= MaxInstances ||
                    !FaunaMotion.TryPose(group, member, time, _habitat, out var pose) ||
                    math.distance(pose.Position.xz, center) > distance)
                {
                    continue;
                }

                var rotation = Quaternion.Euler(-math.degrees(pose.Pitch), math.degrees(pose.Yaw), -math.degrees(pose.Roll));
                var body = Matrix4x4.TRS(new Vector3(pose.Position.x, pose.Position.y, pose.Position.z), rotation, Vector3.one);
                switch (group.Kind)
                {
                    case FaunaKind.Birds:
                        (group.OnWater ? _gulls : _crows).Add(body);
                        var wings = group.OnWater ? _gullWings : _crowWings;
                        var flap = math.degrees(pose.Flap);
                        wings.Add(body * Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, flap)));
                        wings.Add(body * Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, -flap)) * Matrix4x4.Scale(new Vector3(-1f, 1f, 1f)));
                        break;
                    case FaunaKind.Fish:
                        _fish.Add(body);
                        break;
                    default:
                        _deer.Add(body);
                        break;
                }
            }
        }

        private FaunaGroup[] GroupsOf(FaunaKind kind, int patchX, int patchZ)
        {
            var key = ((long)kind << 56) ^ ((long)patchX << 28) ^ (uint)patchZ;
            if (_cache.TryGetValue(key, out var groups))
            {
                return groups;
            }

            _scratch.Clear();
            FaunaGroups.Spawn(kind, patchX, patchZ, (ulong)_seed, _habitat, _scratch);
            groups = _scratch.ToArray();
            _cache[key] = groups;
            return groups;
        }

        private void Draw(Mesh mesh, RenderParams parameters, List<Matrix4x4> instances)
        {
            for (var start = 0; start < instances.Count; start += BatchSize)
            {
                var count = math.min(BatchSize, instances.Count - start);
                instances.CopyTo(start, _batch, 0, count);
                Graphics.RenderMeshInstanced(parameters, mesh, 0, _batch, count);
            }
        }

        private static RenderParams Params(Material material, Color color, ShadowCastingMode shadows)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            return new RenderParams(material)
            {
                matProps = block,
                shadowCastingMode = shadows,
                receiveShadows = true,
                worldBounds = new Bounds(Vector3.zero, new Vector3(1e5f, 1e4f, 1e5f)),
            };
        }

        private static Mesh ToMesh(FaunaShape shape, string name)
        {
            var vertices = new Vector3[shape.Vertices.Length];
            var normals = new Vector3[vertices.Length];
            for (var i = 0; i < vertices.Length; i++)
            {
                vertices[i] = shape.Vertices[i];
                normals[i] = shape.Normals[i];
            }

            var mesh = new Mesh { name = name, vertices = vertices, normals = normals, triangles = shape.Triangles };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
