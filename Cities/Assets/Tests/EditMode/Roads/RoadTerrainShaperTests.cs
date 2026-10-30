using System.Collections.Generic;
using Cities.Simulation.Network;
using Cities.World.Grid;
using Cities.World.Roads;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Tests.EditMode.Roads
{
    /// <summary>Werkt op een <see cref="WorldMap"/> (NativeArrays) en een <see cref="Mesh"/>; draait daarom alleen in Unity.</summary>
    [Category("UnityRuntime")]
    public class RoadTerrainShaperTests
    {
        private static readonly WorldDimensions Dimensions = new WorldDimensions(1, 1, 64, 8f);

        [Test]
        public void TheTerrainUnderTheRoadGoesToTheRoadHeightAndFadesOutAside()
        {
            using (var map = new WorldMap(Dimensions, 10f))
            {
                var changes = new List<RectInt>();
                map.HeightChanged += changes.Add;
                var curve = Bezier3.Straight(new float3(200f, 14f, 100f), new float3(200f, 14f, 300f));

                var corners = RoadTerrainShaper.Apply(map, curve, 5.25f);

                Assert.That(changes, Is.EqualTo(new[] { corners }));
                Assert.That(map.Height[25, 20], Is.EqualTo(14f - RoadTerrainShaper.SurfaceClearance).Within(1e-4f), "op de hartlijn");
                Assert.That(map.Height[26, 20], Is.EqualTo(14f - RoadTerrainShaper.SurfaceClearance).Within(1e-4f), "binnen de halve breedte plus een cel");
                Assert.That(map.Height[28, 20], Is.GreaterThan(10f).And.LessThan(13.85f), "in het zachte randje");
                Assert.That(map.Height[40, 20], Is.EqualTo(10f), "ver weg verandert niets");
                Assert.That(map.Height[25, 5], Is.EqualTo(10f), "voor het begin van de weg");
            }
        }

        [Test]
        public void TreesAndGroundCoverUnderTheRoadAreCleared()
        {
            using (var map = new WorldMap(Dimensions, 10f))
            {
                map.Trees[25, 20] = 200;
                map.Trees[40, 20] = 200;
                var treeChanges = new List<RectInt>();
                map.TreesChanged += treeChanges.Add;

                RoadTerrainShaper.Apply(map, Bezier3.Straight(new float3(200f, 14f, 100f), new float3(200f, 14f, 300f)), 5.25f);

                Assert.That(map.Trees[25, 20], Is.EqualTo(0));
                Assert.That(map.GroundCover[25, 20], Is.EqualTo(GroundCover.Built));
                Assert.That(map.Trees[40, 20], Is.EqualTo(200), "naast de weg blijven de bomen");
                Assert.That(treeChanges, Has.Count.EqualTo(1));
            }
        }

        [Test]
        public void ARoadOutsideTheMapChangesNothing()
        {
            using (var map = new WorldMap(Dimensions, 10f))
            {
                var corners = RoadTerrainShaper.Apply(map, Bezier3.Straight(new float3(-900f, 5f, -900f), new float3(-800f, 5f, -900f)), 5f);

                Assert.That(corners.width, Is.EqualTo(0));
                Assert.That(map.Height[0, 0], Is.EqualTo(10f));
            }
        }

        [Test]
        public void MeshDataFillsAMeshWithASubmeshPerSurface()
        {
            var mesh = new Mesh();
            try
            {
                var data = RoadMeshBuilder.Build(Bezier3.Straight(float3.zero, new float3(0f, 0f, 50f)), LaneProfiles.Road(2, true, true));

                data.CopyTo(mesh);

                Assert.That(mesh.vertexCount, Is.EqualTo(data.Vertices.Count));
                Assert.That(mesh.subMeshCount, Is.EqualTo(6));
                Assert.That(mesh.GetIndexCount(0), Is.EqualTo(data.Triangles[0].Count));
                Assert.That(mesh.bounds.size.z, Is.EqualTo(50f).Within(0.1f));
                Assert.Throws<System.ArgumentNullException>(() => data.CopyTo(null));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
