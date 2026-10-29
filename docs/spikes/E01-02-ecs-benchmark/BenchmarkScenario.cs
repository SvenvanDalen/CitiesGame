using Unity.Collections;
using Unity.Mathematics;

namespace Cities.Spikes.EcsBenchmark
{
    /// <summary>
    /// Gedeeld scenario voor beide varianten: agents rijden over een rasterwegennet van knooppunten
    /// en kiezen bij elk knooppunt een willekeurige buur, zoals een voertuig dat afslaat.
    /// </summary>
    public static class BenchmarkScenario
    {
        public static readonly int[] AgentCounts = { 50_000, 250_000 };
        public const int ChurnPerTick = 500;
        public const int GridSize = 64;
        public const float NodeSpacing = 32f;
        public const float TickSeconds = 0.1f;
        public const int WarmupTicks = 60;
        public const int MeasuredTicks = 600;

        public static NativeArray<float3> CreateNodes(Allocator allocator)
        {
            var nodes = new NativeArray<float3>(GridSize * GridSize, allocator);
            for (var y = 0; y < GridSize; y++)
            {
                for (var x = 0; x < GridSize; x++)
                {
                    nodes[(y * GridSize) + x] = new float3(x * NodeSpacing, 0f, y * NodeSpacing);
                }
            }

            return nodes;
        }

        public static int NextNode(int current, ref Random random)
        {
            var x = current % GridSize;
            var y = current / GridSize;
            switch (random.NextInt(4))
            {
                case 0: x = math.min(x + 1, GridSize - 1); break;
                case 1: x = math.max(x - 1, 0); break;
                case 2: y = math.min(y + 1, GridSize - 1); break;
                default: y = math.max(y - 1, 0); break;
            }

            return (y * GridSize) + x;
        }

        /// <summary>Beweegt één agent een tick en geeft terug of hij een knooppunt bereikte.</summary>
        public static void Step(ref float3 position, ref int targetNode, float speed, ref Random random, NativeArray<float3> nodes)
        {
            var target = nodes[targetNode];
            var toTarget = target - position;
            var distance = math.length(toTarget);
            var travel = speed * TickSeconds;
            if (distance <= travel)
            {
                position = target;
                targetNode = NextNode(targetNode, ref random);
            }
            else
            {
                position += toTarget * (travel / distance);
            }
        }
    }

    public struct BenchmarkResult
    {
        public string Name;
        public int AgentCount;
        public double MedianMs;
        public double P95Ms;
        public double MaxMs;
        public double ChurnMedianMs;
        public double SetupMs;
        public long MemoryBytes;
        public double Checksum;
    }
}
