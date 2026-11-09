using Cities.Gameplay.Cameras;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Cameras
{
    public class CameraFocusTests
    {
        [Test]
        public void KeepsTheAngleAndMovesThePivot()
        {
            var focus = CameraFocus.On(new CameraRig(new float3(10f, 0f, 10f), 30f, 50f, 600f), new float2(100f, 200f), 12f, 300f);

            Assert.That(focus.Pivot, Is.EqualTo(new float3(100f, 12f, 200f)));
            Assert.That(focus.Yaw, Is.EqualTo(30f));
            Assert.That(focus.Pitch, Is.EqualTo(50f));
            Assert.That(focus.Distance, Is.EqualTo(300f), "te ver weg: komt dichterbij");
        }

        [Test]
        public void StaysCloseWhenAlreadyCloser() =>
            Assert.That(CameraFocus.On(new CameraRig(float3.zero, 0f, 45f, 80f), float2.zero, 0f, 300f).Distance, Is.EqualTo(80f));
    }
}
