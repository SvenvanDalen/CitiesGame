using System;
using Cities.World.Grid;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cities.World.Terrain
{
    /// <summary>
    /// Bouwt chunkmeshes uit de hoogtekaart met <see cref="TerrainChunkMeshJob"/>. Houdt de indexbuffers van de vier
    /// detailniveaus vast, die voor elke chunk gelijk zijn.
    /// </summary>
    public sealed class TerrainMeshBuilder : IDisposable
    {
        private NativeArray<ushort> _indices;
        private NativeArray<int2> _indexRanges;
        private NativeArray<VertexAttributeDescriptor> _vertexLayout;

        /// <param name="skirtDepth">Hoe ver de rokken onder de randen hangen, in meters.</param>
        public TerrainMeshBuilder(float skirtDepth)
        {
            if (!(skirtDepth >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(skirtDepth), skirtDepth, "De rok kan niet negatief zijn.");
            }

            SkirtDepth = skirtDepth;
            var templates = new ushort[TerrainChunkLayout.LodCount][];
            var total = 0;
            for (var lod = 0; lod < templates.Length; lod++)
            {
                templates[lod] = TerrainChunkTopology.BuildIndices(TerrainChunkLayout.Resolution(lod));
                total += templates[lod].Length;
            }

            _indices = new NativeArray<ushort>(total, Allocator.Persistent);
            _indexRanges = new NativeArray<int2>(templates.Length, Allocator.Persistent);
            var start = 0;
            for (var lod = 0; lod < templates.Length; lod++)
            {
                NativeArray<ushort>.Copy(templates[lod], 0, _indices, start, templates[lod].Length);
                _indexRanges[lod] = new int2(start, templates[lod].Length);
                start += templates[lod].Length;
            }

            _vertexLayout = TerrainVertex.CreateLayout(Allocator.Persistent);
        }

        public float SkirtDepth { get; }

        /// <summary>
        /// Schrijft mesh <c>i</c> van <paramref name="meshes"/> voor <paramref name="requests"/>[i] en het hoogtebereik
        /// (onderkant rok, hoogste punt) in <paramref name="heightRanges"/>[i].
        /// </summary>
        public void Build(WorldMap map, NativeArray<TerrainChunkRequest> requests, Mesh.MeshDataArray meshes, NativeArray<float2> heightRanges)
        {
            var heights = map.Height;
            new TerrainChunkMeshJob
            {
                Heights = heights.Values,
                HeightsWidth = heights.Layout.Width,
                HeightsDepth = heights.Layout.Depth,
                CellSize = map.Dimensions.CellSize,
                SkirtDepth = SkirtDepth,
                Requests = requests,
                Indices = _indices,
                IndexRanges = _indexRanges,
                Layout = _vertexLayout,
                Meshes = meshes,
                HeightRanges = heightRanges,
            }.Schedule(requests.Length, 4).Complete();
        }

        public void Dispose()
        {
            if (_indices.IsCreated)
            {
                _indices.Dispose();
                _indexRanges.Dispose();
                _vertexLayout.Dispose();
            }
        }
    }
}
