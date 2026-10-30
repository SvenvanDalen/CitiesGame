using Cities.Core.Randomness;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Randomness
{
    /// <summary>Burst-jobs over meerdere threads; draait daarom alleen in Unity.</summary>
    [Category("UnityRuntime")]
    public class RandomJobTests
    {
        private const int Count = 20_000;

        [BurstCompile]
        private struct SampleJob : IJobParallelFor
        {
            public RandomSource Source;
            public long Tick;

            [WriteOnly] public NativeArray<float4> Results;

            public void Execute(int index)
            {
                var random = Source.At(Tick, index);
                var circle = random.NextInsideUnitCircle();
                Results[index] = new float4(circle, random.NextFloat(), random.NextInt(-1000, 1000));
            }
        }

        [Test]
        public void BurstJobsMatchManagedCodeInAnyOrder()
        {
            var source = new SimulationRandom(31337).Source(RandomStreamKey.Of("Test.Jobs"));
            var results = new NativeArray<float4>(Count, Allocator.TempJob);
            try
            {
                // Batches van 1: indices worden over alle werkthreads verdeeld en in onvoorspelbare volgorde uitgevoerd.
                new SampleJob { Source = source, Tick = 4242, Results = results }.Schedule(Count, 1).Complete();

                for (var i = Count - 1; i >= 0; i--)
                {
                    var random = source.At(4242, i);
                    var expected = new float4(random.NextInsideUnitCircle(), random.NextFloat(), random.NextInt(-1000, 1000));
                    Assert.That(results[i], Is.EqualTo(expected), $"Index {i}");
                }
            }
            finally
            {
                results.Dispose();
            }
        }
    }
}
