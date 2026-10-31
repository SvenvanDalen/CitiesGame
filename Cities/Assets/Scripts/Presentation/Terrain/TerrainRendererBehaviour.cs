using System.Collections.Generic;
using Cities.World.Grid;
using Cities.Presentation.Water;
using Cities.World.Terrain;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Cities.Presentation.Terrain
{
    /// <summary>
    /// Tekent de hoogtekaart van de <see cref="WorldMapHostBehaviour"/> als chunks van 256 m met vier detailniveaus.
    /// Elk frame kiest hij per chunk het niveau op afstand van de camera en bouwt hij de dichtstbijzijnde chunks
    /// die moeten veranderen opnieuw in een Burst-job; gewijzigde hoogtes (<see cref="WorldMap.HeightChanged"/>)
    /// komen zo vanzelf in beeld. Houdt ook de splatmap bij (een pixel per cel met zand, rots, akker en bebouwde
    /// grond) die de terreinshader als <c>_CitySplatMap</c> leest.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TerrainRendererBehaviour : MonoBehaviour
    {
        [SerializeField] private WorldMapHostBehaviour _world;
        [SerializeField] private Material _material;
        [SerializeField] private UnityEngine.Camera _camera;

        [Tooltip("Optioneel: het water, zodat de zandrand met de oever meeverschuift.")]
        [SerializeField] private WaterRendererBehaviour _water;

        [Tooltip("Afstand in meters waarboven een chunk een niveau grover wordt (drie oplopende grenzen).")]
        [SerializeField] private float[] _lodDistances = { 600f, 1400f, 3200f };

        [Tooltip("Zoveel chunks worden per frame hoogstens opnieuw gebouwd; de dichtstbijzijnde eerst.")]
        [SerializeField, Min(1)] private int _maxRebuildsPerFrame = 64;

        [Tooltip("Hoe ver de rokken onder de randen hangen, om kieren tussen detailniveaus te dichten.")]
        [SerializeField, Min(0f)] private float _skirtDepth = 12f;

        [SerializeField] private TerrainSplatSettings _splat = TerrainSplatSettings.Default;

        private static readonly int SplatMapId = Shader.PropertyToID("_CitySplatMap");
        private static readonly int WorldSizeId = Shader.PropertyToID("_CityWorldSize");

        private readonly List<int> _touched = new List<int>();
        private readonly List<(float Distance, int Chunk, int Lod)> _pending = new List<(float, int, int)>();
        private readonly List<Mesh> _batchMeshes = new List<Mesh>();

        private TerrainChunkLayout _layout;
        private WorldMap _map;
        private Mesh[] _meshes;
        private MeshRenderer[] _renderers;
        private int[] _lods;
        private int[] _desired;
        private int4[] _builtNeighbours;
        private bool[] _dirty;
        private bool _built;
        private float2[] _heightRanges;
        private TerrainMeshBuilder _builder;
        private NativeArray<Color32> _splatPixels;
        private RenderTexture _splatMap;
        private Texture2D _splatUpload;
        private RectInt _splatDirty;

        public TerrainChunkLayout Layout => _layout;

        /// <summary>Huidig detailniveau per chunk, of -1 als hij nog niet gebouwd is.</summary>
        public IReadOnlyList<int> Lods => _lods;

        /// <summary>De splatmap op de GPU; <see cref="SplatPixels"/> is de kopie in het geheugen.</summary>
        public Texture SplatMap => _splatMap;

        public NativeArray<Color32> SplatPixels => _splatPixels;

        /// <summary>Chunks die in het laatste frame opnieuw gebouwd zijn.</summary>
        public int LastRebuildCount { get; private set; }

        private void Awake()
        {
            if (_camera == null)
            {
                _camera = UnityEngine.Camera.main;
            }

            _map = _world.Map;
            _layout = new TerrainChunkLayout(_map.Dimensions);
            _builder = new TerrainMeshBuilder(_skirtDepth);

            var count = _layout.ChunkCount;
            _meshes = new Mesh[count];
            _renderers = new MeshRenderer[count];
            _lods = new int[count];
            _desired = new int[count];
            _builtNeighbours = new int4[count];
            _dirty = new bool[count];
            _heightRanges = new float2[count];
            for (var i = 0; i < count; i++)
            {
                var origin = _layout.Origin(i);
                var chunk = new GameObject($"Terrain {_layout.Coord(i)}");
                chunk.transform.SetParent(transform, false);
                chunk.transform.localPosition = new Vector3(origin.x, 0f, origin.y);
                _meshes[i] = new Mesh { name = chunk.name };
                chunk.AddComponent<MeshFilter>().sharedMesh = _meshes[i];
                _renderers[i] = chunk.AddComponent<MeshRenderer>();
                _renderers[i].sharedMaterial = _material;
                _renderers[i].enabled = false;
                _lods[i] = -1;
                _dirty[i] = true;
            }

            var dimensions = _map.Dimensions;
            _splatPixels = new NativeArray<Color32>(dimensions.CellCount, Allocator.Persistent);
            _splatMap = new RenderTexture(new RenderTextureDescriptor(dimensions.CellsX, dimensions.CellsZ, GraphicsFormat.R8G8B8A8_UNorm, 0)
            {
                useMipMap = true,
                autoGenerateMips = false,
            })
            {
                name = "Terrain splatmap",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };
            _splatMap.Create();
            _splatDirty = _map.AllCells;
            Shader.SetGlobalTexture(SplatMapId, _splatMap);
            Shader.SetGlobalVector(WorldSizeId, new Vector4(dimensions.SizeX, dimensions.SizeZ, 1f / dimensions.SizeX, 1f / dimensions.SizeZ));

            _map.HeightChanged += OnHeightChanged;
            _map.GroundCoverChanged += OnGroundCoverChanged;
            if (_water != null)
            {
                _water.ShoreChanged += MarkSplatDirty;
            }
        }

        private void OnDestroy()
        {
            if (_map != null)
            {
                _map.HeightChanged -= OnHeightChanged;
                _map.GroundCoverChanged -= OnGroundCoverChanged;
            }

            if (_water != null)
            {
                _water.ShoreChanged -= MarkSplatDirty;
            }

            Destroy(_splatMap);
            Destroy(_splatUpload);
            if (_splatPixels.IsCreated)
            {
                _splatPixels.Dispose();
            }

            if (_meshes != null)
            {
                foreach (var mesh in _meshes)
                {
                    Destroy(mesh);
                }
            }

            _builder?.Dispose();
        }

        private void LateUpdate()
        {
            if (_map == null || _map.IsDisposed || _camera == null)
            {
                return;
            }

            UpdateSplatMap();
            CollectPending(_camera.transform.position);

            // Bij de start alles in één keer, zodat er geen gaten zijn; daarna de dichtstbijzijnde eerst.
            var budget = _pending.Count;
            if (_built)
            {
                budget = _maxRebuildsPerFrame;
                if (_pending.Count > budget)
                {
                    _pending.Sort((a, b) => a.Distance.CompareTo(b.Distance));
                }
            }

            LastRebuildCount = Mathf.Min(budget, _pending.Count);
            if (LastRebuildCount > 0)
            {
                Rebuild(LastRebuildCount);
            }

            _built = true;
        }

        private void CollectPending(Vector3 eye)
        {
            _pending.Clear();
            for (var i = 0; i < _lods.Length; i++)
            {
                // Ongebouwde chunks meten vanaf het maaiveld van de kaart; daarna vanaf hun echte hoogtebereik.
                var range = _lods[i] < 0 ? new float2(0f, 0f) : _heightRanges[i];
                _desired[i] = TerrainChunkLayout.ChooseLod(_layout.Distance(i, eye, range.x, range.y), _lodDistances);
            }

            for (var i = 0; i < _lods.Length; i++)
            {
                // Ook opnieuw als een buur van niveau wisselt: de randen worden tegen de grovere buur gelegd.
                if (_dirty[i] || _desired[i] != _lods[i] || math.any(_layout.NeighbourSteps(i, _desired) != _builtNeighbours[i]))
                {
                    var range = _lods[i] < 0 ? new float2(0f, 0f) : _heightRanges[i];
                    _pending.Add((_layout.Distance(i, eye, range.x, range.y), i, _desired[i]));
                }
            }
        }

        private void Rebuild(int count)
        {
            var requests = new NativeArray<TerrainChunkRequest>(count, Allocator.TempJob);
            var ranges = new NativeArray<float2>(count, Allocator.TempJob);
            var data = Mesh.AllocateWritableMeshData(count);
            var applied = false;
            try
            {
                for (var i = 0; i < count; i++)
                {
                    var (_, chunk, lod) = _pending[i];
                    var origin = _layout.OriginCorner(chunk);
                    _builtNeighbours[chunk] = _layout.NeighbourSteps(chunk, _desired);
                    requests[i] = new TerrainChunkRequest(new int2(origin.x, origin.y), lod, _builtNeighbours[chunk]);
                }

                _builder.Build(_map, requests, data, ranges);

                _batchMeshes.Clear();
                for (var i = 0; i < count; i++)
                {
                    _batchMeshes.Add(_meshes[_pending[i].Chunk]);
                }

                Mesh.ApplyAndDisposeWritableMeshData(data, _batchMeshes, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
                applied = true;
                for (var i = 0; i < count; i++)
                {
                    var (_, chunk, lod) = _pending[i];
                    var mesh = _meshes[chunk];
                    mesh.bounds = mesh.GetSubMesh(0).bounds;
                    _heightRanges[chunk] = ranges[i];
                    _lods[chunk] = lod;
                    _dirty[chunk] = false;
                    _renderers[chunk].enabled = true;
                }
            }
            finally
            {
                if (!applied)
                {
                    data.Dispose();
                }

                requests.Dispose();
                ranges.Dispose();
            }
        }

        /// <summary>
        /// Werkt het vuile deel van de splatmap bij en stuurt alleen dat deel naar de GPU, die de mipmaps maakt;
        /// de hele map met mipmaps op de CPU kost tientallen milliseconden, te veel voor een penseel dat elk frame werkt.
        /// </summary>
        private void UpdateSplatMap()
        {
            var size = new Vector2Int(_map.Dimensions.CellsX, _map.Dimensions.CellsZ);
            var min = Vector2Int.Max(_splatDirty.min, Vector2Int.zero);
            var max = Vector2Int.Min(_splatDirty.max, size);
            _splatDirty = default;
            if (max.x <= min.x || max.y <= min.y)
            {
                return;
            }

            var area = new RectInt(min, max - min);
            TerrainSplatBuilder.Build(_map, _splat, area, _splatPixels);
            if (_splatUpload == null || _splatUpload.width != area.width || _splatUpload.height != area.height)
            {
                Destroy(_splatUpload);
                _splatUpload = new Texture2D(area.width, area.height, GraphicsFormat.R8G8B8A8_UNorm, TextureCreationFlags.None) { name = "Terrain splat upload" };
            }

            var upload = _splatUpload.GetPixelData<Color32>(0);
            for (var row = 0; row < area.height; row++)
            {
                NativeArray<Color32>.Copy(_splatPixels, ((area.yMin + row) * size.x) + area.xMin, upload, row * area.width, area.width);
            }

            _splatUpload.Apply(false);
            Graphics.CopyTexture(_splatUpload, 0, 0, 0, 0, area.width, area.height, _splatMap, 0, 0, area.xMin, area.yMin);
            _splatMap.GenerateMips();
        }

        private void MarkSplatDirty(RectInt cells)
        {
            if (_splatDirty.width <= 0 || _splatDirty.height <= 0)
            {
                _splatDirty = cells;
                return;
            }

            var min = Vector2Int.Min(_splatDirty.min, cells.min);
            var max = Vector2Int.Max(_splatDirty.max, cells.max);
            _splatDirty = new RectInt(min, max - min);
        }

        private void OnGroundCoverChanged(RectInt cells) => MarkSplatDirty(cells);

        private void OnHeightChanged(RectInt corners)
        {
            // Een hoekpunt hoort bij de cellen links en rechts ervan.
            MarkSplatDirty(new RectInt(corners.xMin - 1, corners.yMin - 1, corners.width + 1, corners.height + 1));
            _layout.ChunksTouching(corners, _touched);
            foreach (var chunk in _touched)
            {
                _dirty[chunk] = true;
            }
        }
    }
}
