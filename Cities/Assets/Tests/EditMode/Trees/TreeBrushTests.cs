using System.Collections.Generic;
using Cities.Gameplay.Tools;
using Cities.World.Grid;
using Cities.World.Picking;
using Cities.World.Trees;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Tests.EditMode.Trees
{
    /// <summary>Werkt op een <see cref="WorldMap"/> (NativeArrays); draait daarom alleen in Unity.</summary>
    [Category("UnityRuntime")]
    public class TreeBrushTests
    {
        private static readonly WorldDimensions Dimensions = new WorldDimensions(1, 1, 32, 8f);
        private static readonly float2 Middle = new float2(132f, 132f);

        [Test]
        public void PlantingThickensTheMiddleMostAndStopsAtFullForest()
        {
            using (var map = new WorldMap(Dimensions, 40f))
            {
                var changed = TreeBrush.Apply(map, true, Middle, 24f, 100f);

                Assert.That(map.Trees[16, 16], Is.EqualTo(100));
                Assert.That(map.Trees[18, 16], Is.InRange(1, 98), "zachte rand");
                Assert.That(map.Trees[20, 16], Is.EqualTo(0), "buiten het penseel");
                Assert.That(changed, Is.EqualTo(new RectInt(13, 13, 7, 7)));

                TreeBrush.Apply(map, true, Middle, 24f, 1000f);
                Assert.That(map.Trees[16, 16], Is.EqualTo(255));
            }
        }

        [Test]
        public void CuttingClearsAndNothingIsPlantedOnFieldsOrBuildings()
        {
            using (var map = new WorldMap(Dimensions, 40f))
            {
                map.Trees[16, 16] = 200;
                map.GroundCover[17, 16] = GroundCover.Field;

                TreeBrush.Apply(map, false, Middle, 24f, 1000f);
                TreeBrush.Apply(map, true, new float2(140f, 132f), 4f, 1000f);

                Assert.That(map.Trees[16, 16], Is.EqualTo(0));
                Assert.That(map.Trees[17, 16], Is.EqualTo(0), "akker");
            }
        }

        [Test]
        public void BuildingsClearTheTreesUnderThem()
        {
            using (var map = new WorldMap(Dimensions, 40f))
            {
                map.Trees[5, 6] = 255;
                map.Trees[7, 6] = 255;
                var trees = new List<RectInt>();
                var cover = new List<RectInt>();
                map.TreesChanged += trees.Add;
                map.GroundCoverChanged += cover.Add;

                TreeBrush.ClearForBuilding(map, new RectInt(5, 5, 2, 2));

                Assert.That(map.Trees[5, 6], Is.EqualTo(0));
                Assert.That(map.Trees[7, 6], Is.EqualTo(255), "naast het gebouw");
                Assert.That(map.GroundCover[6, 6], Is.EqualTo(GroundCover.Built));
                Assert.That(trees, Is.EqualTo(new[] { new RectInt(5, 5, 2, 2) }));
                Assert.That(cover, Is.EqualTo(new[] { new RectInt(5, 5, 2, 2) }));
            }
        }

        [Test]
        public void TheBrushIsClippedToTheMap()
        {
            using (var map = new WorldMap(Dimensions, 40f))
            {
                Assert.That(TreeBrush.Apply(map, true, new float2(-4f, 4f), 12f, 50f), Is.EqualTo(new RectInt(0, 0, 1, 2)));
                Assert.That(TreeBrush.Apply(map, true, new float2(-400f, 4f), 12f, 50f).width, Is.EqualTo(0));
            }
        }

        [Test]
        public void ToolPlantsWithStrengthTimesFrameTimeAndReportsIt()
        {
            using (var map = new WorldMap(Dimensions, 40f))
            {
                var tool = new TreeTool(map) { Plant = true, Strength = 0.4f, Radius = 24f };
                var changes = new List<RectInt>();
                map.TreesChanged += changes.Add;
                var input = new ToolInput(new PickResult(PickHit.None, new PickHit(PickKind.Terrain, 0, new float3(132f, 40f, 132f), 100f)), 0, 0.5f);

                tool.Apply(tool.UpdatePreview(input));

                Assert.That(map.Trees[16, 16], Is.EqualTo(51), "255 × 0,4 × 0,5 in het midden");
                Assert.That(changes, Has.Count.EqualTo(1));
                Assert.That(tool.Name, Is.EqualTo("Bomen planten"));

                tool.Plant = false;
                Assert.That(tool.Name, Is.EqualTo("Bomen kappen"));
                Assert.That(tool.UpdatePreview(new ToolInput(default)).IsVisible, Is.False);
                Assert.That(tool.RepeatsWhileHeld, Is.True);
            }
        }

        [Test]
        public void GeneratedForestsLeaveFieldsAndBuildingsBare()
        {
            using (var map = new WorldMap(Dimensions, 40f))
            {
                map.GroundCover[3, 3] = GroundCover.Built;
                var reported = new List<RectInt>();
                map.TreesChanged += reported.Add;
                var settings = ForestSettings.Default;
                settings.Coverage = 1f;

                ForestGenerator.Generate(map, settings);

                Assert.That(map.Trees[3, 3], Is.EqualTo(0));
                Assert.That(map.Trees[10, 10], Is.GreaterThan(200));
                Assert.That(reported, Is.EqualTo(new[] { map.AllCells }));
            }
        }
    }
}
