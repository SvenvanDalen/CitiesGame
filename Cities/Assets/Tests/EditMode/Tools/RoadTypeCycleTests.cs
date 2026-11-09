using Cities.Gameplay.Tools;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Tools
{
    public sealed class RoadTypeCycleTests
    {
        private static readonly string[] Ids = { "a", "b", "c" };

        [Test]
        public void StepsForwardAndWraps()
        {
            Assert.That(RoadTypeCycle.Next(Ids, "a", 1), Is.EqualTo("b"));
            Assert.That(RoadTypeCycle.Next(Ids, "c", 1), Is.EqualTo("a"));
        }

        [Test]
        public void StepsBackwardAndWraps()
        {
            Assert.That(RoadTypeCycle.Next(Ids, "b", -1), Is.EqualTo("a"));
            Assert.That(RoadTypeCycle.Next(Ids, "a", -1), Is.EqualTo("c"));
        }

        [Test]
        public void UnknownCurrentStartsAtFirst() => Assert.That(RoadTypeCycle.Next(Ids, null, 1), Is.EqualTo("a"));

        [Test]
        public void EmptyListGivesNull() => Assert.That(RoadTypeCycle.Next(new string[0], "a", 1), Is.Null);
    }
}
