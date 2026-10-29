using Cities.Gameplay.Cameras;
using Cities.World.Terrain;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Cameras
{
    public class CameraLimitsTests
    {
        private const float Frame = 1f / 60f;

        /// <summary>Heuvel: 200 m hoog ten oosten van x = 6000, daarvoor vlak op 0 m.</summary>
        private sealed class Ridge : ITerrainHeight
        {
            public float SurfaceHeight(float2 position) => position.x >= 6000f ? 200f : 0f;
        }

        /// <summary>Glooiend terrein: stijgt 1 m per 10 m naar het oosten.</summary>
        private sealed class Slope : ITerrainHeight
        {
            public float SurfaceHeight(float2 position) => position.x * 0.1f;
        }

        private static CameraSettings Instant()
        {
            var settings = CameraSettings.Default;
            settings.SmoothingHalfLife = 0f;
            return settings;
        }

        private static CameraController Create(CameraSettings settings, ITerrainHeight terrain = null, float pitch = 45f, float distance = 500f) =>
            new CameraController(settings, new CameraRig(new float3(5000f, 0f, 5000f), 0f, pitch, distance),
                float2.zero, new float2(10_000f, 10_000f), terrain);

        /// <summary>Projecteert een wereldpunt naar de kijkrichting van de camera: (rechts, omhoog) gedeeld door diepte.</summary>
        private static float2 ViewDirection(CameraRig rig, float3 point)
        {
            var local = math.mul(math.inverse(rig.Rotation), point - rig.Position);
            return local.xy / local.z;
        }

        [Test]
        public void ZoomKeepsCursorPointInPlace()
        {
            var controller = Create(Instant());
            var cursor = new float3(5200f, 0f, 5300f);
            var before = ViewDirection(controller.Current, cursor);

            controller.Update(new CameraInput { ScrollSteps = 3f, HasCursorPoint = true, CursorPoint = cursor }, Frame);

            var after = ViewDirection(controller.Current, cursor);
            Assert.That(controller.Current.Distance, Is.LessThan(500f));
            Assert.That(math.distance(before, after), Is.LessThan(1e-4f), "Punt onder de muis blijft op dezelfde plek in beeld");
            Assert.That(math.distance(controller.Current.Pivot.xz, cursor.xz), Is.LessThan(math.distance(new float2(5000f, 5000f), cursor.xz)));
        }

        [Test]
        public void ZoomOutAlsoKeepsCursorPoint()
        {
            var controller = Create(Instant());
            var cursor = new float3(4800f, 0f, 5150f);
            var before = ViewDirection(controller.Current, cursor);

            controller.Update(new CameraInput { ScrollSteps = -2f, HasCursorPoint = true, CursorPoint = cursor }, Frame);

            Assert.That(math.distance(before, ViewDirection(controller.Current, cursor)), Is.LessThan(1e-4f));
        }

        [Test]
        public void ZoomAtDistanceLimitDoesNotMovePivot()
        {
            var controller = Create(Instant(), distance: CameraSettings.Default.MinDistance);

            controller.Update(new CameraInput { ScrollSteps = 5f, HasCursorPoint = true, CursorPoint = new float3(5100f, 0f, 5100f) }, Frame);

            Assert.That(controller.Current.Pivot, Is.EqualTo(new float3(5000f, 0f, 5000f)));
        }

        [Test]
        public void ZoomToCursorCanBeDisabled()
        {
            var settings = Instant();
            settings.ZoomToCursor = false;
            var controller = Create(settings);

            controller.Update(new CameraInput { ScrollSteps = 3f, HasCursorPoint = true, CursorPoint = new float3(5200f, 0f, 5300f) }, Frame);

            Assert.That(controller.Current.Pivot, Is.EqualTo(new float3(5000f, 0f, 5000f)));
        }

        [Test]
        public void ZoomWithoutCursorPointZoomsToCentre()
        {
            var controller = Create(Instant());

            controller.Update(new CameraInput { ScrollSteps = 3f }, Frame);

            Assert.That(controller.Current.Pivot, Is.EqualTo(new float3(5000f, 0f, 5000f)));
        }

        [Test]
        public void PivotFollowsTerrainHeight()
        {
            var controller = Create(Instant(), new Slope());

            Assert.That(controller.Current.Pivot.y, Is.EqualTo(500f).Within(1e-3f));

            controller.Update(new CameraInput { Move = new float2(1f, 0f) }, 1f);

            Assert.That(controller.Current.Pivot.y, Is.EqualTo(controller.Current.Pivot.x * 0.1f).Within(1e-3f));
        }

        [Test]
        public void CameraTiltsUpRatherThanEnteringHill()
        {
            // Camera kijkt naar het westen, vlak, vanaf het oosten: daar ligt de heuvel van 200 m.
            var controller = new CameraController(Instant(), new CameraRig(new float3(5900f, 0f, 5000f), 270f, 12f, 600f),
                float2.zero, new float2(10_000f, 10_000f), new Ridge());

            var rig = controller.Current;

            Assert.That(rig.Position.x, Is.GreaterThan(6000f), "Camera staat boven de heuvel");
            Assert.That(rig.Position.y, Is.GreaterThanOrEqualTo(206f - 0.01f), "Minstens 6 m boven de heuvel");
            Assert.That(rig.Pitch, Is.GreaterThan(12f));
            Assert.That(rig.Distance, Is.EqualTo(600f).Within(0.01f), "Afstand blijft gelijk als kantelen genoeg is");
        }

        [Test]
        public void CameraBacksOffWhenTiltingIsNotEnough()
        {
            // Draaipunt laag naast een heuvel die hoger is dan de afstand: alleen verder weg helpt.
            var settings = Instant();
            settings.MinDistance = 20f;
            var controller = new CameraController(settings, new CameraRig(new float3(5990f, 0f, 5000f), 270f, 30f, 50f),
                float2.zero, new float2(10_000f, 10_000f), new Ridge());

            var rig = controller.Current;

            Assert.That(rig.Pitch, Is.EqualTo(settings.MaxPitch).Within(1e-3f));
            Assert.That(rig.Position.y, Is.GreaterThanOrEqualTo(settings.TerrainClearance - 0.01f));
            Assert.That(rig.Distance, Is.GreaterThanOrEqualTo(50f));
        }

        [Test]
        public void CameraStaysAboveFlatGround()
        {
            var controller = Create(Instant(), pitch: CameraSettings.Default.MinPitch, distance: CameraSettings.Default.MinDistance);

            Assert.That(controller.Current.Position.y, Is.GreaterThanOrEqualTo(CameraSettings.Default.TerrainClearance - 0.01f));
        }

        [Test]
        public void MaxHeightLimitsDistance()
        {
            var settings = Instant();
            settings.MaxHeight = 1000f;
            var controller = Create(settings, pitch: 60f, distance: 3000f);

            Assert.That(controller.Current.Position.y, Is.EqualTo(1000f).Within(0.01f));
        }

        [Test]
        public void MaxHeightBelowTerrainKeepsMinimumDistance()
        {
            var settings = Instant();
            settings.MaxHeight = 100f;
            var controller = Create(settings, new FlatTerrain(500f));

            Assert.That(controller.Current.Distance, Is.GreaterThanOrEqualTo(settings.MinDistance));
            Assert.That(controller.Current.Pivot.y, Is.EqualTo(500f));
        }

        [Test]
        public void SmoothedCameraNeverDipsIntoTerrain()
        {
            var settings = CameraSettings.Default;
            settings.SmoothingHalfLife = 0.2f;
            var terrain = new Ridge();
            var controller = new CameraController(settings, new CameraRig(new float3(4000f, 0f, 5000f), 90f, 15f, 300f),
                float2.zero, new float2(10_000f, 10_000f), terrain);

            controller.SetTarget(new CameraRig(new float3(8000f, 0f, 5000f), 90f, 15f, 300f));
            for (var i = 0; i < 120; i++)
            {
                controller.Update(default, Frame);
                var position = controller.Current.Position;
                Assert.That(position.y, Is.GreaterThanOrEqualTo(terrain.SurfaceHeight(position.xz) + settings.TerrainClearance - 0.01f), $"Frame {i}");
            }
        }

        [Test]
        public void TerrainCanBeReplaced()
        {
            var controller = Create(Instant());

            controller.Terrain = new FlatTerrain(42f);

            Assert.That(controller.Current.Pivot.y, Is.EqualTo(42f));
            Assert.That(controller.Target.Pivot.y, Is.EqualTo(42f));

            controller.Terrain = null;
            Assert.That(controller.Terrain, Is.SameAs(FlatTerrain.Zero));
            Assert.That(FlatTerrain.Zero.Height, Is.EqualTo(0f));
        }
    }
}
