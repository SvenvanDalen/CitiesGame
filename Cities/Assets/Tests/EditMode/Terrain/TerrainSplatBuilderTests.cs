using System.Linq;
using Cities.World.Grid;
using Cities.World.Terrain;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace Cities.Tests.EditMode.Terrain
{
    [Category("UnityRuntime")]
    public class TerrainSplatBuilderTests
    {
        private static readonly WorldDimensions Dimensions = new WorldDimensions(1, 1, 32, 8f);
        private static readonly Color32 Marker = new Color32(1, 2, 3, 4);

        [Test]
        public void PixelsFollowCoverSlopeAndShore()
        {
            using (var map = new WorldMap(Dimensions, 40f))
            using (var pixels = new NativeArray<Color32>(Dimensions.CellCount, Allocator.TempJob))
            {
                map.GroundCover[3, 3] = GroundCover.Built;
                map.GroundCover[4, 3] = GroundCover.Field;

                // Een steile wand langs x = 20 en een meertje bij (6, 26).
                for (var z = 0; z <= 32; z++)
                {
                    map.Height[21, z] += 20f;
                    map.Height[22, z] += 20f;
                }

                map.WaterDepth[6, 26] = 1.2f;

                TerrainSplatBuilder.Build(map, TerrainSplatSettings.Default, map.AllCells, pixels);

                Assert.That(At(pixels, 3, 3), Is.EqualTo(new Color32(0, 0, 0, 255)), "bebouwd");
                Assert.That(At(pixels, 4, 3), Is.EqualTo(new Color32(0, 0, 255, 0)), "akker");
                Assert.That(At(pixels, 10, 10), Is.EqualTo(new Color32(0, 0, 0, 0)), "vlak gras");
                Assert.That(At(pixels, 20, 10).g, Is.EqualTo(255), "rotswand");
                Assert.That(At(pixels, 5, 25).r, Is.EqualTo(255), "zand op de oever");
                Assert.That(At(pixels, 2, 25).r, Is.EqualTo(0), "verder van het water gras");
            }
        }

        [Test]
        public void OnlyTheRequestedCellsChange()
        {
            using (var map = new WorldMap(Dimensions, 40f))
            using (var pixels = new NativeArray<Color32>(Enumerable.Repeat(Marker, Dimensions.CellCount).ToArray(), Allocator.TempJob))
            {
                TerrainSplatBuilder.Build(map, TerrainSplatSettings.Default, new RectInt(30, 30, 10, 10), pixels);

                Assert.That(At(pixels, 31, 31), Is.EqualTo(new Color32(0, 0, 0, 0)));
                Assert.That(At(pixels, 29, 31), Is.EqualTo(Marker));
                Assert.That(At(pixels, 31, 29), Is.EqualTo(Marker));
            }
        }

        [Test]
        public void WrongTextureSizeIsRejected()
        {
            using (var map = new WorldMap(Dimensions))
            using (var pixels = new NativeArray<Color32>(10, Allocator.TempJob))
            {
                Assert.Throws<System.ArgumentException>(() => TerrainSplatBuilder.Build(map, TerrainSplatSettings.Default, map.AllCells, pixels));
            }
        }

        private static Color32 At(NativeArray<Color32> pixels, int x, int z) => pixels[(z * Dimensions.CellsX) + x];
    }
}
