using Cities.Gameplay.Tools;
using Cities.World.Grid;
using Cities.World.Picking;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Tools
{
    public class InspectToolTests
    {
        private static readonly WorldDimensions World = new WorldDimensions(1, 1, 32, 8f);

        private sealed class Fixed : IInspectionSource
        {
            private readonly string _title;

            public Fixed(string title) => _title = title;

            public bool TryInspect(float3 point, out InspectionReport report)
            {
                report = _title == null ? default : new InspectionReport(point, _title);
                return _title != null;
            }
        }

        private static ToolInput At(float x, float z) =>
            new ToolInput(new PickResult(PickHit.None, new PickHit(PickKind.Terrain, 0, new float3(x, 10f, z), 100f)));

        [Test]
        public void TheFirstSourceThatFindsSomethingWins()
        {
            var tool = new InspectTool(new Fixed(null), null, new Fixed("eerste"), new Fixed("tweede"));

            var preview = tool.UpdatePreview(At(10f, 10f));

            Assert.That(preview.IsVisible, Is.True);
            Assert.That(preview.Info, Is.EqualTo("eerste"));
            Assert.That(tool.Name, Is.EqualTo("Inspecteren"));
        }

        [Test]
        public void NothingToSeeHidesThePreviewAndApplyChangesNothing()
        {
            var tool = new InspectTool(new Fixed(null));

            Assert.That(tool.UpdatePreview(At(10f, 10f)).IsVisible, Is.False);
            Assert.That(tool.UpdatePreview(new ToolInput(new PickResult(PickHit.None, PickHit.None))).IsVisible, Is.False);
            Assert.That(tool.Ghost, Is.Null);
            Assert.That(tool.RepeatsWhileHeld, Is.False);
            Assert.That(tool.CancelStep(), Is.False);
            Assert.DoesNotThrow(() =>
            {
                tool.Activate();
                tool.Apply(default);
                tool.Deactivate();
            });
            Assert.Throws<System.ArgumentNullException>(() => new InspectTool(null));
        }

        [Test]
        public void AReportListsItsDetailsAfterTheTitle()
        {
            Assert.That(new InspectionReport(float3.zero, "Weg", "100 m", "50 km/u").ToString(), Is.EqualTo("Weg · 100 m · 50 km/u"));
            Assert.That(new InspectionReport(float3.zero, "Weg").ToString(), Is.EqualTo("Weg"));
        }

        [Test]
        public void ABuildingIsFoundByItsFootprint()
        {
            var occupancy = new FootprintOccupancy();
            occupancy.Add(new Footprint(new CellCoord(10, 10), 3, 2, 1));
            var source = new BuildingInspection(occupancy, World);

            Assert.That(source.TryInspect(new float3(10 * 8f + 4f, 0f, 10 * 8f + 20f), out var report), Is.True);
            Assert.That(report.Title, Is.EqualTo("Gebouw"));
            Assert.That(report.ToString(), Does.Contain("16 × 24 m").And.Contain("6 cellen").And.Contain("90°"));

            Assert.That(source.TryInspect(new float3(5f, 0f, 5f), out _), Is.False);
            Assert.Throws<System.ArgumentNullException>(() => new BuildingInspection(null, World));
        }
    }
}
