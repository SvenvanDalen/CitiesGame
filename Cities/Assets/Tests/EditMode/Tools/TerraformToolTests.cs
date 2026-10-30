using System.Collections.Generic;
using Cities.Gameplay.Tools;
using Cities.World.Grid;
using Cities.World.Picking;
using Cities.World.Terrain;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Tests.EditMode.Tools
{
    /// <summary>Werkt op een <see cref="WorldMap"/> (NativeArrays); draait daarom alleen in Unity.</summary>
    [Category("UnityRuntime")]
    public class TerraformToolTests
    {
        private static readonly WorldDimensions Dimensions = new WorldDimensions(1, 1, 32, 8f);

        private static ToolInput At(float x, float z, float deltaTime = 0.5f) =>
            new ToolInput(new PickResult(PickHit.None, new PickHit(PickKind.Terrain, 0, new float3(x, 40f, z), 100f)), 0, deltaTime);

        private static void Frame(TerraformTool tool, in ToolInput input, bool apply)
        {
            var preview = tool.UpdatePreview(input);
            if (apply)
            {
                tool.Apply(preview);
            }
        }

        [Test]
        public void RaisesWithStrengthTimesFrameTimeAndReportsTheChange()
        {
            using (var map = new WorldMap(Dimensions, 40f))
            {
                var tool = new TerraformTool(map) { Mode = TerraformMode.Raise, Strength = 4f, Radius = 24f };
                var changes = new List<RectInt>();
                map.HeightChanged += changes.Add;

                Frame(tool, At(128f, 128f), apply: true);

                Assert.That(map.Height[16, 16], Is.EqualTo(42f));
                Assert.That(changes, Has.Count.EqualTo(1));
                Assert.That(changes[0].Contains(new Vector2Int(16, 16)), Is.True);
            }
        }

        [Test]
        public void FlattenKeepsTheHeightWhereTheStrokeStarted()
        {
            using (var map = new WorldMap(Dimensions, 40f))
            {
                map.Height[16, 16] = 50f;
                map.Height[20, 16] = 30f;
                var tool = new TerraformTool(map) { Mode = TerraformMode.Flatten, Strength = 40f, Radius = 16f };

                Frame(tool, At(128f, 128f), apply: true);
                Frame(tool, At(160f, 128f), apply: true);

                Assert.That(map.Height[20, 16], Is.EqualTo(50f), "zelfde streek: naar de beginhoogte");

                map.Height[20, 16] = 30f;
                Frame(tool, At(160f, 128f), apply: false);
                Frame(tool, At(160f, 128f), apply: true);

                Assert.That(map.Height[20, 16], Is.EqualTo(30f), "nieuwe streek begint op de huidige hoogte");
            }
        }

        [Test]
        public void SetHeightUsesTheChosenTarget()
        {
            using (var map = new WorldMap(Dimensions, 40f))
            {
                var tool = new TerraformTool(map) { Mode = TerraformMode.SetHeight, Strength = 40f, TargetHeight = 25f };

                Frame(tool, At(128f, 128f), apply: true);

                Assert.That(map.Height[16, 16], Is.EqualTo(25f));
            }
        }

        [Test]
        public void PreviewIsInvalidOutsideTheMapAndHiddenWithoutGround()
        {
            using (var map = new WorldMap(Dimensions, 40f))
            {
                var tool = new TerraformTool(map);

                Assert.That(tool.UpdatePreview(At(128f, 128f)).IsValid, Is.True);
                Assert.That(tool.UpdatePreview(At(-5f, 128f)).InvalidReason, Is.EqualTo("Buiten de kaart"));
                Assert.That(tool.UpdatePreview(new ToolInput(default)).IsVisible, Is.False);
                Assert.That(tool.RepeatsWhileHeld, Is.True);
            }
        }

        [Test]
        public void SettingsAreClamped()
        {
            using (var map = new WorldMap(Dimensions, 40f))
            {
                var tool = new TerraformTool(map) { Radius = 1f, Strength = 1000f, TargetHeight = -500f };

                Assert.That(tool.Radius, Is.EqualTo(TerraformTool.MinRadius));
                Assert.That(tool.Strength, Is.EqualTo(TerraformTool.MaxStrength));
                Assert.That(tool.TargetHeight, Is.EqualTo(TerrainBrush.MinHeight));
            }
        }
    }
}
