using System;
using Cities.Simulation.Fields;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Fields
{
    // NativeArray en jobs draaien alleen in Unity zelf; de rekenregel staat in DiffusionStepTests.
    [Category("UnityRuntime")]
    public class DiffusionFieldTests
    {
        [Test]
        public void TheJobSpreadsAValueAndKeepsTheTotal()
        {
            using var field = new DiffusionField(5, 5);
            field[2, 2] = 100f;

            field.Step(new DiffusionSettings(0.1f, 0f, float2.zero));

            Assert.That(field[2, 2], Is.EqualTo(60f).Within(1e-4f));
            Assert.That(field[1, 2], Is.EqualTo(10f).Within(1e-4f));
            Assert.That(field.Total, Is.EqualTo(100f).Within(1e-3f));
        }

        [Test]
        public void TheJobAppliesDecayAndWind()
        {
            using var field = new DiffusionField(7, 7);
            field[3, 3] = 100f;

            field.Step(new DiffusionSettings(0f, 0.5f, new float2(0.5f, 0f)));

            Assert.That(field[4, 3], Is.EqualTo(25f).Within(1e-4f));
        }

        [Test]
        public void BadSizesAndCellsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DiffusionField(0, 3));
            using var field = new DiffusionField(2, 2);
            Assert.Throws<ArgumentOutOfRangeException>(() => field[2, 0] = 1f);
            Assert.That(field.Values.Length, Is.EqualTo(4));
        }
    }
}
