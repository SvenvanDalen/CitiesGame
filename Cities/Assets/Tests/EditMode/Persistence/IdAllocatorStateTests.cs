using Cities.Core.Ids;
using Cities.Core.Persistence;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Persistence
{
    public class IdAllocatorStateTests
    {
        [Test]
        public void CountsAliveAndRetiredSlots()
        {
            // Plek 0 leeft, 1 is vrij (in de wachtrij), 2 is uitgeput, 3 leeft in generatie 5.
            var state = IdAllocatorState.Read(Content(new[] { 1u, 2u, uint.MaxValue - 1, 5u }, new[] { 1 }), 100);

            Assert.That(state.AliveCount, Is.EqualTo(2));
            Assert.That(state.RetiredCount, Is.EqualTo(1));
            Assert.That(state.Free, Is.EqualTo(new[] { 1 }));
            Assert.That(state.Generations.Length, Is.EqualTo(4));
        }

        [TestCase(new[] { 1u, 2u }, new[] { 0 }, TestName = "Vrije plek leeft nog")]
        [TestCase(new[] { 1u, 2u }, new[] { 5 }, TestName = "Vrije plek bestaat niet")]
        [TestCase(new[] { 2u, 2u }, new[] { 0, 0, 1 }, TestName = "Vrije plek dubbel")]
        [TestCase(new[] { 1u, 0u }, new int[0], TestName = "Generatie nul")]
        [TestCase(new[] { 1u, 4u }, new int[0], TestName = "Vrij maar niet in de wachtrij")]
        public void InconsistentStateIsRejected(uint[] generations, int[] free)
        {
            Assert.Throws<SaveFormatException>(() => IdAllocatorState.Read(Content(generations, free), 100));
        }

        [Test]
        public void SlotLimitIsEnforced()
        {
            Assert.Throws<SaveFormatException>(() => IdAllocatorState.Read(Content(new[] { 1u, 1u, 1u }, new int[0]), 2));
        }

        private static SaveReader Content(uint[] generations, int[] free) => SaveBytes.ReaderFor(w =>
        {
            w.WriteArray<uint>(generations);
            w.WriteArray<int>(free);
        });
    }
}
