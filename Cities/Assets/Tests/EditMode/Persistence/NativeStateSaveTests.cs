using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using Cities.Core.Ids;
using Cities.Core.Persistence;
using Cities.World.Grid;
using NUnit.Framework;
using Unity.Collections;

namespace Cities.Tests.EditMode.Persistence
{
    /// <summary>Kaartlagen en ID-allocators zitten in NativeArrays; draait daarom alleen in Unity.</summary>
    [Category("UnityRuntime")]
    public class NativeStateSaveTests
    {
        private static readonly WorldDimensions Small = new WorldDimensions(2, 2, 16, 8f);

        [Test]
        public void IdAllocatorContinuesWithTheSameIdsAfterLoading()
        {
            var original = new IdAllocator<BuildingKind>(4, Allocator.Temp);
            var copy = new IdAllocator<BuildingKind>(0, Allocator.Temp);
            try
            {
                var a = original.Allocate();
                var b = original.Allocate();
                original.Allocate();
                original.Release(b);
                original.Release(a);

                var reader = SaveBytes.ReaderFor(original.WriteTo);
                copy.Restore(IdAllocatorState.Read(reader, 1000));

                Assert.That(copy.AliveCount, Is.EqualTo(original.AliveCount));
                Assert.That(copy.SlotCount, Is.EqualTo(3));
                Assert.That(copy.IsAlive(b), Is.False);
                for (var i = 0; i < 4; i++)
                {
                    Assert.That(copy.Allocate(), Is.EqualTo(original.Allocate()), $"ID {i} na laden");
                }
            }
            finally
            {
                original.Dispose();
                copy.Dispose();
            }
        }

        [Test]
        public void WorldMapLayersRoundTrip()
        {
            using (var map = new WorldMap(Small, 12f))
            using (var loaded = new WorldMap(Small))
            {
                map.Height[3, 4] = 57.25f;
                map.WaterDepth[5, 5] = 1.5f;
                map.Zones.SetForCell(new CellCoord(7, 9), new ZoneCell(ZoneType.Industrial, ZoneDensity.High));
                map.GroundPollution[1, 2] = 0.8f;
                map.LandValue[2, 1] = 340f;

                var bytes = SaveBytes.Write(new SaveMetadata(), new WorldMapSection(map));
                SaveBytes.Read(bytes, new WorldMapSection(loaded));

                Assert.That(loaded.Height[3, 4], Is.EqualTo(57.25f));
                Assert.That(loaded.Height[0, 0], Is.EqualTo(12f));
                Assert.That(loaded.WaterDepth[5, 5], Is.EqualTo(1.5f));
                Assert.That(loaded.Zones.GetForCell(new CellCoord(7, 9)), Is.EqualTo(new ZoneCell(ZoneType.Industrial, ZoneDensity.High)));
                Assert.That(loaded.GroundPollution[1, 2], Is.EqualTo(0.8f));
                Assert.That(loaded.LandValue[2, 1], Is.EqualTo(340f));
            }
        }

        [Test]
        public void LoadingReportsThatAllHeightsChanged()
        {
            using (var map = new WorldMap(Small, 12f))
            using (var loaded = new WorldMap(Small))
            {
                var changes = new System.Collections.Generic.List<UnityEngine.RectInt>();
                loaded.HeightChanged += changes.Add;

                SaveBytes.Read(SaveBytes.Write(new SaveMetadata(), new WorldMapSection(map)), new WorldMapSection(loaded));

                Assert.That(changes, Is.EqualTo(new[] { loaded.AllCorners }), "de terreinmesh moet na laden opnieuw gebouwd worden");
                Assert.That(loaded.AllCorners.width, Is.EqualTo(Small.CellsX + 1));
            }
        }

        [Test]
        public void OtherMapSizeIsRejectedWithoutChanges()
        {
            using (var map = new WorldMap(Small, 12f))
            using (var other = new WorldMap(new WorldDimensions(3, 2, 16, 8f), 1f))
            {
                var bytes = SaveBytes.Write(new SaveMetadata(), new WorldMapSection(map));

                Assert.Throws<SaveFormatException>(() => SaveBytes.Read(bytes, new WorldMapSection(other)));
                Assert.That(other.Height[0, 0], Is.EqualTo(1f));
            }
        }

        [Test]
        public void UnknownZoneIsRejected()
        {
            using (var map = new WorldMap(Small))
            using (var loaded = new WorldMap(Small))
            {
                var zones = map.Zones.Values.Reinterpret<byte>(2);
                zones[10] = 9;

                var bytes = SaveBytes.Write(new SaveMetadata(), new WorldMapSection(map));

                Assert.Throws<SaveFormatException>(() => SaveBytes.Read(bytes, new WorldMapSection(loaded)));
            }
        }

        /// <summary>De volledige kaart (9×9 tegels, ruim 5 miljoen cellen): grootte en tijd van opslaan en laden.</summary>
        [Test]
        public void FullMapSaveStaysWithinBudget()
        {
            using (var map = new WorldMap(WorldDimensions.Default, 20f))
            using (var loaded = new WorldMap(WorldDimensions.Default))
            {
                // Een realistisch reliëf: zachte heuvels en een rivierdal, plus een gezoneerde wijk.
                var height = map.Height.Values;
                var width = map.Height.Layout.Width;
                for (var i = 0; i < height.Length; i++)
                {
                    var x = i % width;
                    var z = i / width;
                    height[i] = 20f + 15f * UnityEngine.Mathf.PerlinNoise(x * 0.004f, z * 0.004f) + 3f * UnityEngine.Mathf.PerlinNoise(x * 0.03f, z * 0.03f);
                }

                for (var x = 1000; x < 1100; x++)
                {
                    for (var z = 1000; z < 1100; z++)
                    {
                        map.Zones.SetForCell(new CellCoord(x, z), new ZoneCell(ZoneType.Residential, ZoneDensity.Low));
                    }
                }

                var section = new WorldMapSection(map);
                var watch = Stopwatch.StartNew();
                byte[] bytes;
                using (var stream = new MemoryStream())
                {
                    SaveFile.Write(stream, new SaveMetadata(), new ISaveSection[] { section }, CompressionLevel.Fastest);
                    bytes = stream.ToArray();
                }

                var saveMs = watch.ElapsedMilliseconds;
                watch.Restart();
                SaveBytes.Read(bytes, new WorldMapSection(loaded));
                var loadMs = watch.ElapsedMilliseconds;

                TestContext.WriteLine($"Volledige kaart: {bytes.Length / 1048576f:F1} MB (ongecomprimeerd {map.MemoryBytes / 1048576f:F1} MB), opslaan {saveMs} ms, laden {loadMs} ms");
                Assert.That(loaded.Height[1234, 2001], Is.EqualTo(map.Height[1234, 2001]));
                Assert.That(bytes.Length, Is.LessThan(map.MemoryBytes), "Compressie werkt");
                Assert.That(saveMs + loadMs, Is.LessThan(10000), "Ruime grens; het budget komt in E23-01");
            }
        }
    }
}
