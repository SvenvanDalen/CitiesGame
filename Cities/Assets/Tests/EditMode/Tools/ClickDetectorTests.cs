using Cities.Gameplay.Tools;
using NUnit.Framework;
using UnityEngine;

namespace Cities.Tests.EditMode.Tools
{
    public class ClickDetectorTests
    {
        [Test]
        public void AShortStillPressIsAClick()
            => Assert.That(ClickDetector.IsClick(new Vector2(10, 10), 1f, new Vector2(12, 11), 1.1f), Is.True);

        [Test]
        public void DraggingIsNotAClick()
            => Assert.That(ClickDetector.IsClick(new Vector2(10, 10), 1f, new Vector2(60, 10), 1.1f), Is.False);

        [Test]
        public void HoldingIsNotAClick()
            => Assert.That(ClickDetector.IsClick(new Vector2(10, 10), 1f, new Vector2(10, 10), 2f), Is.False);
    }
}
