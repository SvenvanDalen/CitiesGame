using System;
using System.Collections.Generic;
using Cities.World.Grid;
using Cities.World.Trees;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cities.Presentation.Trees
{
    /// <summary>
    /// Tekent de bomen van de bosdichtheidskaart. De kaart is verdeeld in regio's van 1 km met per soort een lijst
    /// bomen op de GPU, die opnieuw wordt opgebouwd als de bomen, de hoogte of de grondbedekking daar verandert. Elke
    /// regio tekent al zijn bomen in één keer als impostor (de shader laat bomen dichtbij weg); in de regio's rond de
    /// camera komt daar per boom het volle model of de eenvoudige kroon bij, met schaduw.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TreeRendererBehaviour : MonoBehaviour
    {
        /// <summary>Regio's van 128 × 128 cellen (1 km).</summary>
        public const int RegionCells = 128;

        private const int BatchSize = 1023;

        private static readonly int InstancesId = Shader.PropertyToID("_TreeInstances");
        private static readonly int ImpostorStartId = Shader.PropertyToID("_TreeImpostorStart");

        [SerializeField] private WorldMapHostBehaviour _world;
        [SerializeField] private TreeModelSet _models;
        [SerializeField] private UnityEngine.Camera _camera;

        [Tooltip("Tot hier het volle model met schaduw.")]
        [SerializeField, Min(0f)] private float _detailDistance = 180f;

        [Tooltip("Tot hier de eenvoudige kroon met schaduw; verder een impostor zonder schaduw.")]
        [SerializeField, Min(0f)] private float _simpleDistance = 500f;

        [Tooltip("Verder weg worden geen bomen getekend.")]
        [SerializeField, Min(0f)] private float _impostorDistance = 4500f;

        [Tooltip("Zoveel regio's worden per frame hoogstens opnieuw opgebouwd; de dichtstbijzijnde eerst.")]
        [SerializeField, Min(1)] private int _maxRegionBuildsPerFrame = 2;

        private readonly List<int> _dirtyNear = new List<int>();
        private WorldMap _map;
        private int _regionsX;
        private int _regionsZ;
        private Region[] _regions;
        private NativeList<Matrix4x4>[] _detail;
        private NativeList<Matrix4x4>[] _simple;
        private Vector3 _classifiedAt;
        private bool _classified;

        /// <summary>Bomen in het laatste frame: vol model, kroon en het totaal in de getekende regio's.</summary>
        public int3 LastDrawn { get; private set; }

        /// <summary>Codeert schaal (0,001 nauwkeurig) en draaiing in één getal voor de impostor-shader.</summary>
        public static float PackScaleAndYaw(float scale, float yawDegrees) => math.floor(scale * 1000f) + (math.frac(yawDegrees / 360f) * 0.999f);

        private void Awake()
        {
            if (_camera == null)
            {
                _camera = UnityEngine.Camera.main;
            }

            _map = _world.Map;
            _regionsX = (_map.Dimensions.CellsX + RegionCells - 1) / RegionCells;
            _regionsZ = (_map.Dimensions.CellsZ + RegionCells - 1) / RegionCells;
            _regions = new Region[_regionsX * _regionsZ];
            var species = _models.Species.Length;
            for (var i = 0; i < _regions.Length; i++)
            {
                _regions[i] = new Region(species);
            }

            _detail = Lists(species);
            _simple = Lists(species);
            _map.TreesChanged += MarkDirty;
            _map.GroundCoverChanged += MarkDirty;
            _map.HeightChanged += OnHeightChanged;
        }

        private void OnDestroy()
        {
            if (_map != null)
            {
                _map.TreesChanged -= MarkDirty;
                _map.GroundCoverChanged -= MarkDirty;
                _map.HeightChanged -= OnHeightChanged;
            }

            if (_regions != null)
            {
                foreach (var region in _regions)
                {
                    region.Dispose();
                }
            }

            Release(_detail);
            Release(_simple);
        }

        private void LateUpdate()
        {
            if (_map == null || _map.IsDisposed || _camera == null)
            {
                return;
            }

            var eye = _camera.transform.position;
            Shader.SetGlobalFloat(ImpostorStartId, _simpleDistance);
            BuildDirtyRegions(eye);

            // De indeling dichtbij hangt alleen af van de plek van de camera; draaien verandert niets.
            var reclassify = !_classified || (eye - _classifiedAt).sqrMagnitude > 16f;
            if (reclassify)
            {
                foreach (var list in _detail)
                {
                    list.Clear();
                }

                foreach (var list in _simple)
                {
                    list.Clear();
                }

                _classifiedAt = eye;
                _classified = true;
            }

            var total = 0;
            for (var i = 0; i < _regions.Length; i++)
            {
                var region = _regions[i];
                var distance = DistanceTo(i, eye);
                if (!region.Built || distance > _impostorDistance)
                {
                    continue;
                }

                total += DrawImpostors(region);
                if (reclassify && distance < _simpleDistance)
                {
                    Classify(region, eye);
                }
            }

            var near = DrawNear();
            LastDrawn = new int3(near.x, near.y, total);
        }

        private int DrawImpostors(Region region)
        {
            var count = 0;
            for (var s = 0; s < region.Trees.Length; s++)
            {
                var trees = region.Trees[s].Length;
                if (trees == 0)
                {
                    continue;
                }

                var model = _models.Species[s];
                var parameters = new RenderParams(model.ImpostorMaterial)
                {
                    worldBounds = region.Bounds,
                    matProps = region.Properties[s],
                    shadowCastingMode = ShadowCastingMode.Off,
                };
                Graphics.RenderMeshPrimitives(parameters, model.Impostor, 0, trees);
                count += trees;
            }

            return count;
        }

        /// <summary>Zet de bomen van een nabije regio die binnen bereik staan om in matrices voor het volle model of de kroon.</summary>
        private void Classify(Region region, Vector3 eye)
        {
            var detail = _detailDistance * _detailDistance;
            var simple = _simpleDistance * _simpleDistance;
            for (var s = 0; s < region.Trees.Length; s++)
            {
                var trees = region.Trees[s];
                for (var t = 0; t < trees.Length; t++)
                {
                    var tree = trees[t];
                    var position = new Vector3(tree.x, tree.y, tree.z);
                    var distance = (position - eye).sqrMagnitude;
                    if (distance >= simple)
                    {
                        continue;
                    }

                    var matrix = Matrix4x4.TRS(position, Quaternion.Euler(0f, math.frac(tree.w) / 0.999f * 360f, 0f), Vector3.one * (math.floor(tree.w) / 1000f));
                    (distance < detail ? _detail[s] : _simple[s]).Add(matrix);
                }
            }
        }

        private int2 DrawNear()
        {
            var counts = int2.zero;
            var bounds = new Bounds(_camera.transform.position, Vector3.one * (_simpleDistance * 2.2f));
            for (var s = 0; s < _detail.Length; s++)
            {
                var model = _models.Species[s];
                var bark = new RenderParams(model.Bark) { worldBounds = bounds, shadowCastingMode = ShadowCastingMode.On, receiveShadows = true };
                var foliage = new RenderParams(model.Foliage) { worldBounds = bounds, shadowCastingMode = ShadowCastingMode.On, receiveShadows = true };
                Draw(bark, model.Detail, 0, _detail[s].AsArray());
                Draw(foliage, model.Detail, 1, _detail[s].AsArray());
                Draw(bark, model.Simple, 0, _simple[s].AsArray());
                Draw(foliage, model.Simple, 1, _simple[s].AsArray());
                counts += new int2(_detail[s].Length, _simple[s].Length);
            }

            return counts;
        }

        private static void Draw(in RenderParams parameters, Mesh mesh, int submesh, NativeArray<Matrix4x4> instances)
        {
            for (var start = 0; start < instances.Length; start += BatchSize)
            {
                Graphics.RenderMeshInstanced(parameters, mesh, submesh, instances, math.min(BatchSize, instances.Length - start), start);
            }
        }

        /// <summary>Bouwt eerst de dichtstbijzijnde vuile regio's binnen zicht; de rest volgt in latere frames.</summary>
        private void BuildDirtyRegions(Vector3 eye)
        {
            _dirtyNear.Clear();
            for (var i = 0; i < _regions.Length; i++)
            {
                if (_regions[i].Dirty && DistanceTo(i, eye) <= _impostorDistance)
                {
                    _dirtyNear.Add(i);
                }
            }

            _dirtyNear.Sort((a, b) => DistanceTo(a, eye).CompareTo(DistanceTo(b, eye)));
            for (var i = 0; i < math.min(_maxRegionBuildsPerFrame, _dirtyNear.Count); i++)
            {
                Build(_dirtyNear[i]);
                _classified = false;
            }
        }

        /// <summary>
        /// Zet de bomen van een regio klaar voor de GPU. Een cel met bebouwde grond krijgt geen bomen; de boom staat
        /// 20 cm in de grond, zodat hij op een helling niet zweeft.
        /// </summary>
        private void Build(int index)
        {
            // shortcut: een regio (16.000 cellen) wordt op de main thread opgebouwd, rond 2 ms; als terraformen in bos hapert, naar een Burst-job.
            var region = _regions[index];
            foreach (var list in region.Trees)
            {
                list.Clear();
            }

            var cellSize = _map.Dimensions.CellSize;
            var x0 = (index % _regionsX) * RegionCells;
            var z0 = (index / _regionsX) * RegionCells;
            var x1 = math.min(x0 + RegionCells, _map.Dimensions.CellsX);
            var z1 = math.min(z0 + RegionCells, _map.Dimensions.CellsZ);
            var low = float.MaxValue;
            var high = float.MinValue;
            for (var z = z0; z < z1; z++)
            {
                for (var x = x0; x < x1; x++)
                {
                    var density = _map.Trees[x, z];
                    if (density == 0 || _map.GroundCover[x, z] == GroundCover.Built)
                    {
                        continue;
                    }

                    var count = TreePlacement.Count(x, z, density);
                    var share = TreePlacement.ConiferShare(_map.Height[x, z]);
                    for (var t = 0; t < count; t++)
                    {
                        var tree = TreePlacement.Tree(x, z, t, cellSize, share);
                        var y = _map.Height.Sample(new Vector2(tree.Position.x, tree.Position.y)) - 0.2f;
                        region.Trees[(int)tree.Species].Add(new float4(tree.Position.x, y, tree.Position.y, PackScaleAndYaw(tree.Scale, tree.YawDegrees)));
                        low = math.min(low, y);
                        high = math.max(high, y);
                    }
                }
            }

            region.Upload();
            var min = new Vector3(x0 * cellSize, low - 1f, z0 * cellSize);
            var max = new Vector3(x1 * cellSize, high + 30f, z1 * cellSize);
            region.Bounds = low <= high ? new Bounds((min + max) * 0.5f, max - min) : default;
            region.Dirty = false;
            region.Built = true;
        }

        /// <summary>Afstand van de camera tot de rand van de regio, in het horizontale vlak.</summary>
        private float DistanceTo(int index, Vector3 eye)
        {
            var size = RegionCells * _map.Dimensions.CellSize;
            var min = new float2(index % _regionsX, index / _regionsX) * size;
            var point = new float2(eye.x, eye.z);
            return math.distance(point, math.clamp(point, min, min + size));
        }

        private void MarkDirty(RectInt cells)
        {
            var min = math.max(new int2(cells.xMin, cells.yMin) / RegionCells, 0);
            var max = math.min(new int2(cells.xMax - 1, cells.yMax - 1) / RegionCells, new int2(_regionsX - 1, _regionsZ - 1));
            for (var z = min.y; z <= max.y; z++)
            {
                for (var x = min.x; x <= max.x; x++)
                {
                    _regions[(z * _regionsX) + x].Dirty = true;
                }
            }
        }

        // Een hoekpunt hoort bij de cellen links en rechts ervan.
        private void OnHeightChanged(RectInt corners) => MarkDirty(new RectInt(corners.xMin - 1, corners.yMin - 1, corners.width + 1, corners.height + 1));

        private static NativeList<Matrix4x4>[] Lists(int count)
        {
            var lists = new NativeList<Matrix4x4>[count];
            for (var i = 0; i < count; i++)
            {
                lists[i] = new NativeList<Matrix4x4>(1024, Allocator.Persistent);
            }

            return lists;
        }

        private static void Release(NativeList<Matrix4x4>[] lists)
        {
            if (lists == null)
            {
                return;
            }

            foreach (var list in lists)
            {
                if (list.IsCreated)
                {
                    list.Dispose();
                }
            }
        }

        /// <summary>De bomen van één regio per soort: plek (x, y, z) en geschaalde draaiing (w), in het geheugen en op de GPU.</summary>
        private sealed class Region : IDisposable
        {
            private readonly GraphicsBuffer[] _buffers;

            public Region(int species)
            {
                Trees = new NativeList<float4>[species];
                Properties = new MaterialPropertyBlock[species];
                _buffers = new GraphicsBuffer[species];
                for (var s = 0; s < species; s++)
                {
                    Trees[s] = new NativeList<float4>(0, Allocator.Persistent);
                    Properties[s] = new MaterialPropertyBlock();
                }
            }

            public NativeList<float4>[] Trees { get; }

            public MaterialPropertyBlock[] Properties { get; }

            public Bounds Bounds { get; set; }

            public bool Dirty { get; set; } = true;

            public bool Built { get; set; }

            /// <summary>Zet de bomen in de GPU-buffers; een buffer groeit alleen als hij te klein is.</summary>
            public void Upload()
            {
                for (var s = 0; s < Trees.Length; s++)
                {
                    var count = Trees[s].Length;
                    if (count == 0)
                    {
                        continue;
                    }

                    if (_buffers[s] == null || _buffers[s].count < count)
                    {
                        _buffers[s]?.Release();
                        _buffers[s] = new GraphicsBuffer(GraphicsBuffer.Target.Structured, math.max(count, 64), sizeof(float) * 4);
                        Properties[s].SetBuffer(InstancesId, _buffers[s]);
                    }

                    _buffers[s].SetData(Trees[s].AsArray(), 0, 0, count);
                }
            }

            public void Dispose()
            {
                foreach (var list in Trees)
                {
                    if (list.IsCreated)
                    {
                        list.Dispose();
                    }
                }

                foreach (var buffer in _buffers)
                {
                    buffer?.Release();
                }
            }
        }
    }
}
