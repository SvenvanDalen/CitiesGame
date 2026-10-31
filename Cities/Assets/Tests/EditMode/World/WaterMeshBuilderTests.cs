using Cities.World.Water;
using NUnit.Framework;

namespace Cities.Tests.EditMode.World
{
    public class WaterMeshBuilderTests
    {
        private const int Width = 40;
        private const int Depth = 40;
        private const float Spacing = 8f;

        private float[] _ground;
        private float[] _water;
        private WaterMeshBuilder _builder;

        [SetUp]
        public void SetUp()
        {
            _ground = new float[Width * Depth];
            _water = new float[Width * Depth];
            _builder = new WaterMeshBuilder();
        }

        private bool Build(int cornerX = 0, int cornerZ = 0) =>
            _builder.Build(_ground, _water, Width, Depth, Spacing, cornerX, cornerZ);

        [Test]
        public void ADryBlockHasNoMesh()
        {
            Assert.That(Build(), Is.False);
            Assert.That(_builder.IndexCount, Is.EqualTo(0));
        }

        [Test]
        public void WaterBelowTheThresholdCountsAsDry()
        {
            _water[(5 * Width) + 5] = WaterMeshBuilder.WetThreshold * 0.5f;

            Assert.That(Build(), Is.False);
        }

        [Test]
        public void OnePuddleMakesTheFourQuadsAroundIt()
        {
            _ground[(5 * Width) + 5] = 3f;
            _water[(5 * Width) + 5] = 0.5f;

            Assert.That(Build(), Is.True);

            // Een nat hoekpunt raakt vier cellen, elk twee driehoeken.
            Assert.That(_builder.IndexCount, Is.EqualTo(4 * 6));
            Assert.That(_builder.Indices.Length, Is.EqualTo(4 * 6));
            var vertex = _builder.Vertices[(5 * 33) + 5];
            Assert.That(vertex.Y, Is.EqualTo(3.5f).Within(1e-5f));
            Assert.That(vertex.Depth, Is.EqualTo(0.5f));
            Assert.That(vertex.X, Is.EqualTo(5 * Spacing));
            Assert.That(vertex.Z, Is.EqualTo(5 * Spacing));
        }

        [Test]
        public void ADryCornerLiesOnTheGroundWithDepthZero()
        {
            _ground[(6 * Width) + 6] = 7f;
            _water[(5 * Width) + 5] = 1f;

            Build();

            var dry = _builder.Vertices[(6 * 33) + 6];
            Assert.That(dry.Depth, Is.EqualTo(0f));
            Assert.That(dry.Y, Is.EqualTo(7f));
        }

        [Test]
        public void TheBlockOffsetMovesTheWindow()
        {
            _water[(35 * Width) + 35] = 1f;

            Assert.That(Build(0, 0), Is.False, "ver buiten het eerste blok");
            Assert.That(Build(32, 32), Is.True);
            Assert.That(_builder.Vertices[(3 * 33) + 3].Depth, Is.EqualTo(1f));
        }

        [Test]
        public void TheEdgeOfTheMapIsClampedInsteadOfRead()
        {
            // Blok van 33 punten begint op 32, maar de kaart heeft maar 40: de rest herhaalt de laatste rij en kolom.
            _water[(39 * Width) + 39] = 1f;

            Assert.That(Build(32, 32), Is.True);
            Assert.That(_builder.Vertices[(32 * 33) + 32].X, Is.EqualTo(39 * Spacing));
        }

        [Test]
        public void FlowPointsDownTheSlopeOfTheSurface()
        {
            for (var z = 0; z < Depth; z++)
            {
                for (var x = 0; x < Width; x++)
                {
                    _ground[(z * Width) + x] = -x * 0.2f; // verval naar +x
                    _water[(z * Width) + x] = 1f;
                }
            }

            Build();

            var flow = _builder.Vertices[(5 * 33) + 5];
            Assert.That(flow.FlowX, Is.GreaterThan(0.9f));
            Assert.That(flow.FlowZ, Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void StillWaterDoesNotFlowAndSteepWaterIsClamped()
        {
            for (var i = 0; i < _water.Length; i++)
            {
                _water[i] = 1f;
            }

            Build();
            Assert.That(_builder.Vertices[(5 * 33) + 5].FlowX, Is.EqualTo(0f));

            for (var z = 0; z < Depth; z++)
            {
                for (var x = 0; x < Width; x++)
                {
                    _ground[(z * Width) + x] = x * 5f; // steil verval naar -x
                }
            }

            Build();
            var steep = _builder.Vertices[(5 * 33) + 5];
            Assert.That(steep.FlowX, Is.EqualTo(-1f).Within(1e-4f));
        }

        [Test]
        public void TheWetShapeChangesWhenTheShoreMovesEvenIfTheCountStaysEqual()
        {
            Build();
            Assert.That(_builder.WetShape, Is.EqualTo(0));

            _water[(5 * Width) + 5] = 1f;
            Build();
            var first = _builder.WetShape;

            _water[(5 * Width) + 5] = 0f;
            _water[(5 * Width) + 6] = 1f;
            Build();

            Assert.That(first, Is.Not.EqualTo(0));
            Assert.That(_builder.WetShape, Is.Not.EqualTo(first));
            Assert.That(_builder.WetShape, Is.Not.EqualTo(0));

            _water[(5 * Width) + 6] = 2f; // dieper, zelfde oever
            Build();
            var deeper = _builder.WetShape;
            _water[(5 * Width) + 6] = 3f;
            Build();
            Assert.That(_builder.WetShape, Is.EqualTo(deeper));
        }
    }
}
