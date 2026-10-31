using System;
using Cities.World.Grid;
using Unity.Collections;
using Cities.World.Water;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cities.Presentation.Water
{
    /// <summary>
    /// Tekent het water van de <see cref="WorldMap.WaterDepth"/>-laag als blokken van 32 × 32 cellen. Elk frame
    /// herbouwt hij een vast aantal blokken om beurten (<see cref="WaterMeshBuilder"/>); blokken zonder water hebben
    /// geen object, dus een droge kaart kost niets. Het water volgt het terrein en de simulatie vanzelf, omdat de
    /// mesh uit dezelfde lagen komt.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaterRendererBehaviour : MonoBehaviour
    {
        private static readonly VertexAttributeDescriptor[] Layout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 3),
        };

        [SerializeField] private WorldMapHostBehaviour _world;
        [SerializeField] private Material _material;

        [Tooltip("Zoveel blokken worden per frame hoogstens opnieuw gebouwd; ze komen om beurten aan de beurt.")]
        [SerializeField, Min(1)] private int _blocksPerFrame = 96;

        private readonly WaterMeshBuilder _builder = new WaterMeshBuilder();

        private WorldMap _map;
        private int _blocksX;
        private int _next;
        private MeshRenderer[] _renderers;
        private Mesh[] _meshes;
        private int[] _shapes;
        private NativeArray<WaterVertex> _vertexBuffer;
        private NativeArray<ushort> _indexBuffer;

        /// <summary>
        /// De oever in dit blok cellen is verschoven (water erbij of weg); het terrein kan er zijn zandrand op aanpassen.
        /// </summary>
        public event Action<RectInt> ShoreChanged;

        /// <summary>Blokken met een zichtbaar wateroppervlak.</summary>
        public int VisibleBlocks { get; private set; }

        private void Awake()
        {
            _map = _world.Map;
            _blocksX = _map.Dimensions.CellsX / WaterMeshBuilder.BlockCells;
            var blocks = _blocksX * (_map.Dimensions.CellsZ / WaterMeshBuilder.BlockCells);
            _renderers = new MeshRenderer[blocks];
            _meshes = new Mesh[blocks];
            _shapes = new int[blocks];
            _vertexBuffer = new NativeArray<WaterVertex>(_builder.Vertices.Length, Allocator.Persistent);
            _indexBuffer = new NativeArray<ushort>(WaterMeshBuilder.BlockCells * WaterMeshBuilder.BlockCells * 6, Allocator.Persistent);
        }

        private void OnDestroy()
        {
            foreach (var mesh in _meshes)
            {
                Destroy(mesh);
            }

            _vertexBuffer.Dispose();
            _indexBuffer.Dispose();
        }

        private void LateUpdate()
        {
            if (_map == null || _map.IsDisposed)
            {
                return;
            }

            var count = Mathf.Min(_blocksPerFrame, _meshes.Length);
            for (var i = 0; i < count; i++)
            {
                Rebuild(_next);
                _next = (_next + 1) % _meshes.Length;
            }
        }

        private void Rebuild(int block)
        {
            var ground = _map.Height;
            var layout = ground.Layout;
            var spacing = layout.Spacing;
            var cornerX = block % _blocksX * WaterMeshBuilder.BlockCells;
            var cornerZ = block / _blocksX * WaterMeshBuilder.BlockCells;

            var wet = _builder.Build(ground.Values.AsReadOnlySpan(), _map.WaterDepth.Values.AsReadOnlySpan(), layout.Width, layout.Depth, spacing, cornerX, cornerZ);
            if (_builder.WetShape != _shapes[block])
            {
                _shapes[block] = _builder.WetShape;
                ShoreChanged?.Invoke(new RectInt(cornerX, cornerZ, WaterMeshBuilder.BlockCells, WaterMeshBuilder.BlockCells));
            }

            var existing = _renderers[block];
            if (!wet)
            {
                if (existing != null && existing.enabled)
                {
                    existing.enabled = false;
                    VisibleBlocks--;
                }

                return;
            }

            if (existing == null)
            {
                existing = CreateBlock(block);
            }

            Upload(_meshes[block]);
            if (!existing.enabled)
            {
                existing.enabled = true;
                VisibleBlocks++;
            }
        }

        private MeshRenderer CreateBlock(int block)
        {
            var go = new GameObject($"Water {block % _blocksX},{block / _blocksX}");
            go.transform.SetParent(transform, false);
            _meshes[block] = new Mesh { name = go.name };
            _meshes[block].MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = _meshes[block];
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            _renderers[block] = renderer;
            return renderer;
        }

        private void Upload(Mesh mesh)
        {
            var vertices = _builder.Vertices;
            var indices = _builder.Indices;
            vertices.CopyTo(_vertexBuffer.AsSpan());
            indices.CopyTo(_indexBuffer.AsSpan());
            mesh.Clear();
            mesh.SetVertexBufferParams(vertices.Length, Layout);
            mesh.SetVertexBufferData(_vertexBuffer, 0, 0, vertices.Length, 0, MeshUpdateFlags.DontRecalculateBounds);
            mesh.SetIndexBufferParams(indices.Length, IndexFormat.UInt16);
            mesh.SetIndexBufferData(_indexBuffer, 0, 0, indices.Length, MeshUpdateFlags.DontRecalculateBounds);
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, indices.Length), MeshUpdateFlags.DontRecalculateBounds);
            mesh.RecalculateBounds();
        }
    }
}
