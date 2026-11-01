using System;
using System.Collections.Generic;
using Cities.World.Buildings;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Buildings
{
    public class BuildingTemplateTests
    {
        private static readonly BuildingSpec Spec = new BuildingSpec { FootprintWidth = 2, FootprintDepth = 3 };

        private static List<string> Check((float, float, float) min, (float, float, float) max, int renderers = 1)
        {
            var errors = new List<string>();
            BuildingTemplate.Validate(Spec, min, max, renderers, 8f, errors);
            return errors;
        }

        [Test]
        public void AModelInsideItsFootprintOnTheGroundIsFine()
        {
            Assert.That(Check((-7.5f, 0f, -11.5f), (7.5f, 12f, 11.5f)), Is.Empty);
            Assert.That(Check((-8.4f, -0.1f, -12.4f), (8.4f, 5f, 12.4f)), Is.Empty, "binnen de tolerantie");
        }

        [Test]
        public void ModelOutsideTheFootprintIsReported()
        {
            Assert.That(Check((-9f, 0f, -11f), (7f, 10f, 11f)), Has.Some.Contains("buiten de footprint"));
            Assert.That(Check((-7f, 0f, -11f), (7f, 10f, 13f)), Has.Some.Contains("buiten de footprint"));
        }

        [Test]
        public void ATinyModelIsReported()
        {
            Assert.That(Check((-2f, 0f, -2f), (2f, 10f, 2f)), Has.Some.Contains("minder dan de helft"));
        }

        [Test]
        public void ModelNotOnTheGroundOrWithoutHeightIsReported()
        {
            Assert.That(Check((-7f, 1f, -11f), (7f, 10f, 11f)), Has.Some.Contains("maaiveld"));
            Assert.That(Check((-7f, 0f, -11f), (7f, 0f, 11f)), Has.Some.Contains("geen hoogte"));
        }

        [Test]
        public void NoRendererStopsEarlyAndNullErrorsAreRejected()
        {
            Assert.That(Check(default, default, 0), Is.EqualTo(new[] { "Het model heeft geen renderer." }));
            Assert.Throws<ArgumentNullException>(() => BuildingTemplate.Validate(Spec, default, default, 1, 8f, null));
        }
    }
}
