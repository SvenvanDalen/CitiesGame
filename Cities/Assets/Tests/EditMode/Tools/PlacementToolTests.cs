using System;
using System.Collections.Generic;
using Cities.Gameplay.Tools;
using Cities.World.Grid;
using Cities.World.Picking;
using Cities.World.Terrain;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Tools
{
    public class PlacementToolTests
    {
        private sealed class Target : IPlacementTarget
        {
            public readonly FootprintOccupancy Occupancy = new FootprintOccupancy();
            public readonly List<float3> Positions = new List<float3>();

            public string Check(in Footprint footprint) => Occupancy.Check(footprint);

            public void Place(in Footprint footprint, float3 position)
            {
                Occupancy.Add(footprint);
                Positions.Add(position);
            }
        }

        /// <summary>Helling langs X: elke meter oostwaarts een vaste stijging.</summary>
        private sealed class Slope : ITerrainHeight
        {
            private readonly float _rise;

            public Slope(float rise) => _rise = rise;

            public float SurfaceHeight(float2 position) => 10f + (position.x * _rise);
        }

        private static readonly WorldDimensions Dimensions = new WorldDimensions(1, 1, 64, 8f);

        private static ToolInput At(float x, float z, int rotate = 0, float y = 0f) =>
            new ToolInput(new PickResult(PickHit.None, new PickHit(PickKind.Terrain, 0, new float3(x, y, z), 100f)), rotate);

        private static PlacementTool Tool(Target target, ITerrainHeight terrain = null) =>
            new PlacementTool("Huis", 2, 3, Dimensions, terrain ?? new FlatTerrain(4f), target);

        [Test]
        public void PreviewSnapsToTheGridAndSitsOnTheGround()
        {
            var tool = Tool(new Target());
            tool.Activate();

            var preview = tool.UpdatePreview(At(33f, 30f));

            Assert.That(preview.IsValid, Is.True);
            Assert.That(preview.Position, Is.EqualTo(new float3(32f, 4f, 28f)));
            Assert.That(preview.Size, Is.EqualTo(new float2(16f, 24f)));
            Assert.That(preview.YawDegrees, Is.EqualTo(0f));
        }

        [Test]
        public void RotateStepsTurnTheFootprint()
        {
            var tool = Tool(new Target());
            tool.Activate();

            var preview = tool.UpdatePreview(At(33f, 30f, rotate: 1));
            Assert.That(preview.YawDegrees, Is.EqualTo(90f));
            Assert.That(tool.QuarterTurns, Is.EqualTo(1));

            tool.UpdatePreview(At(33f, 30f, rotate: -2));
            Assert.That(tool.QuarterTurns, Is.EqualTo(3));

            tool.Activate();
            Assert.That(tool.QuarterTurns, Is.EqualTo(0), "elke keer dat de tool aangaat staat hij weer recht");
        }

        [Test]
        public void NoGroundMeansNoPreview()
        {
            var tool = Tool(new Target());

            Assert.That(tool.UpdatePreview(new ToolInput(default)).IsVisible, Is.False);
        }

        [Test]
        public void PlacesOncePerClickAndCarriesTheFrameTime()
        {
            var tool = Tool(new Target());

            Assert.That(tool.RepeatsWhileHeld, Is.False);
            Assert.That(new ToolInput(default, 0, 0.25f).DeltaTime, Is.EqualTo(0.25f));
        }

        [Test]
        public void OutsideTheMapIsInvalid()
        {
            var tool = Tool(new Target());

            Assert.That(tool.UpdatePreview(At(2f, 100f)).InvalidReason, Is.EqualTo(PlacementTool.OutsideMap));
            Assert.That(tool.UpdatePreview(At(510f, 100f)).InvalidReason, Is.EqualTo(PlacementTool.OutsideMap));
        }

        [Test]
        public void SteepGroundIsInvalidAndTheBaseIsTheLowestPoint()
        {
            var gentle = Tool(new Target(), new Slope(0.05f)).UpdatePreview(At(100f, 100f));
            Assert.That(gentle.IsValid, Is.True);
            Assert.That(gentle.Position.y, Is.EqualTo(10f + (96f * 0.05f)).Within(1e-4f));

            var steep = Tool(new Target(), new Slope(0.2f)).UpdatePreview(At(100f, 100f));
            Assert.That(steep.InvalidReason, Is.EqualTo(PlacementTool.TooSteep));
        }

        [Test]
        public void ApplyPlacesAndBlocksTheSameCells()
        {
            var target = new Target();
            var controller = new ToolController();
            controller.Select(Tool(target));

            controller.Update(At(100f, 100f));
            Assert.That(controller.Apply(), Is.True);
            Assert.That(target.Positions, Has.Count.EqualTo(1));

            controller.Update(At(108f, 100f));
            Assert.That(controller.Preview.InvalidReason, Is.EqualTo(FootprintOccupancy.Occupied));
            Assert.That(controller.Apply(), Is.False);

            controller.Update(At(124f, 100f));
            Assert.That(controller.Preview.IsValid, Is.True, "naast het eerste huis is nog plaats");
        }

        [Test]
        public void PlacementCannotBeCancelledStepwise()
        {
            Assert.That(Tool(new Target()).CancelStep(), Is.False);
        }

        [Test]
        public void ConstructorRejectsBadArguments()
        {
            var target = new Target();
            Assert.That(() => new PlacementTool("x", 0, 1, Dimensions, FlatTerrain.Zero, target), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new PlacementTool("x", 1, 1, Dimensions, FlatTerrain.Zero, target, -1f), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new PlacementTool("x", 1, 1, Dimensions, null, target), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => new PlacementTool("x", 1, 1, Dimensions, FlatTerrain.Zero, null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void OccupancyCanBeCleared()
        {
            var occupancy = new FootprintOccupancy();
            occupancy.Add(new Footprint(default, 1, 1, 0));
            occupancy.Clear();

            Assert.That(occupancy.Placed, Is.Empty);
            Assert.That(occupancy.Check(new Footprint(default, 1, 1, 0)), Is.Null);
        }
    }
}
