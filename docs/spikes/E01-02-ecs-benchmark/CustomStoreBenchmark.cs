using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Cities.Spikes.EcsBenchmark
{
    /// <summary>
    /// Variant B: eigen lichte ECS. Agents staan dicht opeengepakt in een NativeList; een sparse tabel
    /// vertaalt stabiele ID's (index + generatie) naar de dichte positie. Verwijderen is swap-back.
    /// </summary>
    public static class CustomStoreBenchmark
    {
        public struct Agent
        {
            public float3 Position;
            public int TargetNode;
            public float Speed;
            public Random Random;
            public int Id;
        }

        private struct Slot
        {
            public int DenseIndex;
            public int Generation;
        }

        [BurstCompile(CompileSynchronously = true)]
        private struct MoveJob : IJobParallelFor
        {
            public NativeArray<Agent> Agents;
            [ReadOnly] public NativeArray<float3> Nodes;

            public void Execute(int index)
            {
                var agent = Agents[index];
                BenchmarkScenario.Step(ref agent.Position, ref agent.TargetNode, agent.Speed, ref agent.Random, Nodes);
                Agents[index] = agent;
            }
        }

        private sealed class Store : System.IDisposable
        {
            public NativeList<Agent> Agents;
            private NativeList<Slot> _slots;
            private NativeList<int> _freeSlots;

            public Store(int capacity)
            {
                Agents = new NativeList<Agent>(capacity, Allocator.Persistent);
                _slots = new NativeList<Slot>(capacity, Allocator.Persistent);
                _freeSlots = new NativeList<int>(capacity, Allocator.Persistent);
            }

            public long MemoryBytes =>
                ((long)Agents.Capacity * UnsafeUtility.SizeOf<Agent>()) +
                ((long)_slots.Capacity * UnsafeUtility.SizeOf<Slot>()) +
                ((long)_freeSlots.Capacity * sizeof(int));

            public void Add(Agent agent)
            {
                int slot;
                if (_freeSlots.Length > 0)
                {
                    slot = _freeSlots[_freeSlots.Length - 1];
                    _freeSlots.RemoveAt(_freeSlots.Length - 1);
                }
                else
                {
                    slot = _slots.Length;
                    _slots.Add(default);
                }

                var entry = _slots[slot];
                entry.DenseIndex = Agents.Length;
                _slots[slot] = entry;
                agent.Id = slot;
                Agents.Add(agent);
            }

            public void RemoveAt(int denseIndex)
            {
                var removedSlot = Agents[denseIndex].Id;
                var last = Agents.Length - 1;
                if (denseIndex != last)
                {
                    var moved = Agents[last];
                    Agents[denseIndex] = moved;
                    var movedEntry = _slots[moved.Id];
                    movedEntry.DenseIndex = denseIndex;
                    _slots[moved.Id] = movedEntry;
                }

                Agents.RemoveAt(last);
                var entry = _slots[removedSlot];
                entry.Generation++;
                entry.DenseIndex = -1;
                _slots[removedSlot] = entry;
                _freeSlots.Add(removedSlot);
            }

            public void Dispose()
            {
                Agents.Dispose();
                _slots.Dispose();
                _freeSlots.Dispose();
            }
        }

        public static BenchmarkResult Run(int agentCount)
        {
            var nodes = BenchmarkScenario.CreateNodes(Allocator.Persistent);
            var random = new Random(1234);
            var setup = Stopwatch.StartNew();
            var store = new Store(agentCount + BenchmarkScenario.ChurnPerTick);
            for (var i = 0; i < agentCount; i++)
            {
                store.Add(CreateAgent(ref random, nodes.Length));
            }

            setup.Stop();

            var total = BenchmarkScenario.WarmupTicks + BenchmarkScenario.MeasuredTicks;
            var ticks = new double[BenchmarkScenario.MeasuredTicks];
            var churn = new double[BenchmarkScenario.MeasuredTicks];
            var watch = new Stopwatch();
            for (var tick = 0; tick < total; tick++)
            {
                watch.Restart();
                new MoveJob { Agents = store.Agents.AsArray(), Nodes = nodes }
                    .Schedule(store.Agents.Length, 256).Complete();
                var moveTicks = watch.Elapsed.TotalMilliseconds;

                for (var i = 0; i < BenchmarkScenario.ChurnPerTick; i++)
                {
                    store.RemoveAt(random.NextInt(store.Agents.Length));
                }

                for (var i = 0; i < BenchmarkScenario.ChurnPerTick; i++)
                {
                    store.Add(CreateAgent(ref random, nodes.Length));
                }

                watch.Stop();
                if (tick >= BenchmarkScenario.WarmupTicks)
                {
                    ticks[tick - BenchmarkScenario.WarmupTicks] = watch.Elapsed.TotalMilliseconds;
                    churn[tick - BenchmarkScenario.WarmupTicks] = watch.Elapsed.TotalMilliseconds - moveTicks;
                }
            }

            double checksum = 0;
            foreach (var agent in store.Agents)
            {
                checksum += agent.Position.x + agent.Position.z;
            }

            var result = BenchmarkStatistics.Summarize("Eigen lichte ECS (NativeList + IJobParallelFor)", agentCount, ticks, churn);
            result.SetupMs = setup.Elapsed.TotalMilliseconds;
            result.MemoryBytes = store.MemoryBytes;
            result.Checksum = checksum;
            store.Dispose();
            nodes.Dispose();
            return result;
        }

        private static Agent CreateAgent(ref Random random, int nodeCount)
        {
            var start = random.NextInt(nodeCount);
            return new Agent
            {
                Position = new float3(start % BenchmarkScenario.GridSize, 0f, start / BenchmarkScenario.GridSize) * BenchmarkScenario.NodeSpacing,
                TargetNode = start,
                Speed = random.NextFloat(8f, 16f),
                Random = new Random(random.NextUInt(1, uint.MaxValue)),
            };
        }
    }
}
