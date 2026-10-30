using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cities.World.Terrain
{
    /// <summary>Hoekpunt van de terreinmesh: positie ten opzichte van de chunk en normaal.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TerrainVertex
    {
        public float3 Position;
        public float3 Normal;

        public static NativeArray<VertexAttributeDescriptor> CreateLayout(Allocator allocator)
        {
            var layout = new NativeArray<VertexAttributeDescriptor>(2, allocator);
            layout[0] = new VertexAttributeDescriptor(VertexAttribute.Position);
            layout[1] = new VertexAttributeDescriptor(VertexAttribute.Normal);
            return layout;
        }
    }

    /// <summary>Welke chunk op welk detailniveau gebouwd moet worden.</summary>
    public struct TerrainChunkRequest
    {
        /// <summary>Hoekpunt linksonder in de hoogtelaag.</summary>
        public int2 OriginCorner;

        public int Lod;

        /// <summary>
        /// Stap (in hoekpunten) van de buren zuid, oost, noord en west. Is een buur grover, dan komen de randpunten van
        /// deze chunk op diens rechte rand te liggen, zodat er geen kier ontstaat.
        /// </summary>
        public int4 NeighbourSteps;
    }

    /// <summary>
    /// Bouwt chunkmeshes uit de hoogtelaag (hoekpunten). Normalen komen uit de buren op volle resolutie, zodat ze
    /// op gedeelde randen gelijk zijn, ook tussen chunks met een ander detailniveau.
    /// </summary>
    [BurstCompile]
    public struct TerrainChunkMeshJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> Heights;
        public int HeightsWidth;
        public int HeightsDepth;
        public float CellSize;
        public float SkirtDepth;

        [ReadOnly] public NativeArray<TerrainChunkRequest> Requests;

        /// <summary>Indices van alle detailniveaus achter elkaar; <see cref="IndexRanges"/> geeft begin en lengte per niveau.</summary>
        [ReadOnly] public NativeArray<ushort> Indices;
        [ReadOnly] public NativeArray<int2> IndexRanges;
        [ReadOnly] public NativeArray<VertexAttributeDescriptor> Layout;

        public Mesh.MeshDataArray Meshes;

        /// <summary>Laagste (inclusief rok) en hoogste punt per chunk, voor de bounds en de LOD-afstand.</summary>
        [WriteOnly] public NativeArray<float2> HeightRanges;

        public void Execute(int index)
        {
            var request = Requests[index];
            var resolution = TerrainChunkLayout.ChunkCells >> request.Lod;
            var step = 1 << request.Lod;
            var n = resolution + 1;
            var gridCount = n * n;
            var vertexCount = gridCount + (4 * n);

            var mesh = Meshes[index];
            mesh.SetVertexBufferParams(vertexCount, Layout);
            var vertices = mesh.GetVertexData<TerrainVertex>();
            var low = float.MaxValue;
            var high = float.MinValue;
            for (var z = 0; z < n; z++)
            {
                for (var x = 0; x < n; x++)
                {
                    var gx = request.OriginCorner.x + (x * step);
                    var gz = request.OriginCorner.y + (z * step);
                    var height = Height(gx, gz);
                    vertices[(z * n) + x] = new TerrainVertex
                    {
                        Position = new float3(x * step * CellSize, height, z * step * CellSize),
                        Normal = Normal(gx, gz),
                    };
                }
            }

            Stitch(vertices, request, n, step);
            for (var i = 0; i < gridCount; i++)
            {
                low = math.min(low, vertices[i].Position.y);
                high = math.max(high, vertices[i].Position.y);
            }

            for (var skirt = 0; skirt < 4 * n; skirt++)
            {
                var top = vertices[TerrainChunkTopology.EdgeVertex(resolution, skirt / n, skirt % n)];
                top.Position.y -= SkirtDepth;
                vertices[gridCount + skirt] = top;
            }

            var range = IndexRanges[request.Lod];
            mesh.SetIndexBufferParams(range.y, IndexFormat.UInt16);
            NativeArray<ushort>.Copy(Indices, range.x, mesh.GetIndexData<ushort>(), 0, range.y);

            var size = TerrainChunkLayout.ChunkCells * CellSize;
            var bottom = low - SkirtDepth;
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(
                0,
                new SubMeshDescriptor(0, range.y)
                {
                    vertexCount = vertexCount,
                    bounds = new Bounds(new Vector3(size * 0.5f, (bottom + high) * 0.5f, size * 0.5f), new Vector3(size, high - bottom, size)),
                },
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);
            HeightRanges[index] = new float2(bottom, high);
        }

        /// <summary>Legt de randpunten tegen een grovere buur op de lijn tussen diens hoekpunten.</summary>
        private void Stitch(NativeArray<TerrainVertex> vertices, TerrainChunkRequest request, int n, int step)
        {
            var origin = request.OriginCorner;
            var last = (n - 1) * step;
            for (var i = 0; i < n; i++)
            {
                var along = i * step;
                StitchVertex(vertices, i, origin.x + along, origin.y, request.NeighbourSteps.x, step, true);
                StitchVertex(vertices, (i * n) + (n - 1), origin.x + last, origin.y + along, request.NeighbourSteps.y, step, false);
                StitchVertex(vertices, ((n - 1) * n) + i, origin.x + along, origin.y + last, request.NeighbourSteps.z, step, true);
                StitchVertex(vertices, i * n, origin.x, origin.y + along, request.NeighbourSteps.w, step, false);
            }
        }

        private void StitchVertex(NativeArray<TerrainVertex> vertices, int index, int x, int z, int neighbourStep, int step, bool alongX)
        {
            if (neighbourStep <= step)
            {
                return;
            }

            var coordinate = alongX ? x : z;
            var start = coordinate / neighbourStep * neighbourStep;
            if (start == coordinate)
            {
                return;
            }

            var t = (float)(coordinate - start) / neighbourStep;
            var a = alongX ? Height(start, z) : Height(x, start);
            var b = alongX ? Height(start + neighbourStep, z) : Height(x, start + neighbourStep);
            var vertex = vertices[index];
            vertex.Position.y = math.lerp(a, b, t);
            vertices[index] = vertex;
        }

        private float Height(int x, int z)
        {
            x = math.clamp(x, 0, HeightsWidth - 1);
            z = math.clamp(z, 0, HeightsDepth - 1);
            return Heights[(z * HeightsWidth) + x];
        }

        private float3 Normal(int x, int z) =>
            math.normalize(new float3(Height(x - 1, z) - Height(x + 1, z), 2f * CellSize, Height(x, z - 1) - Height(x, z + 1)));
    }
}
