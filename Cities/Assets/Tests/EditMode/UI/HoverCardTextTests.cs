using Cities.Gameplay.Tools;
using Cities.Presentation.UI;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.UI
{
    public class HoverCardTextTests
    {
        [Test]
        public void TitleOnlyHasNoExtraLines() =>
            Assert.That(HoverCardText.Format(new InspectionReport(float3.zero, "Weg")), Is.EqualTo("Weg"));

        [Test]
        public void DetailsFollowTheTitleAndProblemsAreMarked()
        {
            var report = new InspectionReport(InspectionKind.Building, float3.zero, "Woning", new[] { "4 van 6 bewoners" }, new[] { "Geen stroom" });

            Assert.That(HoverCardText.Format(report), Is.EqualTo("Woning\n4 van 6 bewoners\n! Geen stroom"));
        }
    }
}
