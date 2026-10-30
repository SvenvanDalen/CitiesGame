using System.Linq;
using Cities.Gameplay.Tools;
using Cities.World.Grid;
using Cities.World.Picking;
using Cities.World.Water;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.World
{
    public class WaterSourcesTests
    {
        private WaterSources _sources;
        private WaterSourceTool _tool;

        [SetUp]
        public void SetUp()
        {
            _sources = new WaterSources();
            _tool = new WaterSourceTool(_sources, new WorldDimensions(1, 1, 32, 8f));
        }

        private static ToolInput At(float x, float z) =>
            new ToolInput(new PickResult(PickHit.None, new PickHit(PickKind.Terrain, 0, new float3(x, 10f, z), 100f)));

        private ToolPreview Click(float x, float z)
        {
            var preview = _tool.UpdatePreview(At(x, z));
            if (preview.IsValid)
            {
                _tool.Apply(preview);
            }

            return preview;
        }

        [Test]
        public void SourcesAreAddedAndFoundByDistance()
        {
            Assert.That(_sources.Add(10f, 10f, 5f), Is.True);
            Assert.That(_sources.Add(100f, 100f, -8f), Is.True);

            Assert.That(_sources.Nearest(12f, 11f, 5f).Value.Discharge, Is.EqualTo(5f));
            Assert.That(_sources.Nearest(99f, 100f, 5f).Value.IsDrain, Is.True);
            Assert.That(_sources.Nearest(50f, 50f, 5f), Is.Null);
        }

        [Test]
        public void AnEmptyOrInvalidDischargeIsRefused()
        {
            Assert.That(_sources.Add(0f, 0f, 0f), Is.False);
            Assert.That(_sources.Add(0f, 0f, float.NaN), Is.False);
            Assert.That(_sources.Count, Is.EqualTo(0));
        }

        [Test]
        public void TheListHasALimit()
        {
            for (var i = 0; i < WaterSources.MaxCount; i++)
            {
                Assert.That(_sources.Add(i, 0f, 1f), Is.True);
            }

            Assert.That(_sources.Add(0f, 0f, 1f), Is.False);
        }

        [Test]
        public void ChangesBumpTheVersionAndReplaceSwapsEverything()
        {
            var start = _sources.Version;
            _sources.Add(1f, 1f, 3f);
            _sources.RemoveNearest(1f, 1f, 1f);
            Assert.That(_sources.RemoveNearest(1f, 1f, 1f), Is.False, "er is niets meer");
            Assert.That(_sources.Version, Is.EqualTo(start + 2));

            _sources.Replace(new[] { new WaterSource(5f, 5f, 2f), new WaterSource(6f, 6f, -2f) });
            Assert.That(_sources.Items.Select(s => s.Discharge), Is.EqualTo(new[] { 2f, -2f }));
        }

        [Test]
        public void TheToolPlacesASourceAndAClickOnItRemovesIt()
        {
            _tool.Discharge = 30f;

            var placing = Click(100f, 100f);
            Assert.That(placing.Info, Does.Contain("Bron van 30 m³/s"));
            Assert.That(_sources.Count, Is.EqualTo(1));
            Assert.That(_sources.Items[0].Discharge, Is.EqualTo(30f));

            Assert.That(_tool.UpdatePreview(At(105f, 100f)).Info, Does.Contain("klik om weg te halen"));
            Click(105f, 100f);
            Assert.That(_sources.Count, Is.EqualTo(0));
        }

        [Test]
        public void ADrainHasANegativeDischargeAndTheToolClampsTheSetting()
        {
            _tool.Drain = true;
            _tool.Discharge = 5000f;
            Assert.That(_tool.Discharge, Is.EqualTo(WaterSourceTool.MaxDischarge));
            Assert.That(_tool.Name, Is.EqualTo("Afvoer"));

            Click(50f, 50f);

            Assert.That(_sources.Items[0].Discharge, Is.EqualTo(-WaterSourceTool.MaxDischarge));
            Assert.That(_tool.UpdatePreview(At(50f, 50f)).Info, Does.StartWith("Afvoer van 500"));
        }

        [Test]
        public void OutsideTheMapOrOffTheGroundNothingHappens()
        {
            var outside = Click(-50f, 100f);

            Assert.That(outside.IsValid, Is.False);
            Assert.That(_sources.Count, Is.EqualTo(0));
            Assert.That(_tool.UpdatePreview(new ToolInput(new PickResult(PickHit.None, PickHit.None))).IsVisible, Is.False);
            Assert.That(_tool.CancelStep(), Is.False);
            Assert.Throws<System.ArgumentNullException>(() => new WaterSourceTool(null, new WorldDimensions(1, 1, 32, 8f)));
        }
    }
}
