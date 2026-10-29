using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Cities.Spikes.EcsBenchmark
{
    /// <summary>
    /// Variant A: Unity Entities. Elke agent is een entity met dezelfde data als variant B;
    /// beweging loopt via een Burst-IJobEntity, verloop via een EntityCommandBuffer.
    /// </summary>
    public static partial class EntitiesBenchmark
    {
        public struct AgentMotion : IComponentData
        {
            public float3 Position;
            public int TargetNode;
            public float Speed;
            public Random Random;
        }

        public struct RoadNodes : IComponentData
        {
            public NativeArray<float3> Nodes;
        }

        [BurstCompile(CompileSynchronously = true)]
        private partial struct MoveJob : IJobEntity
        {
            [ReadOnly] public NativeArray<float3> Nodes;

            private void Execute(ref AgentMotion agent)
            {
                BenchmarkScenario.Step(ref agent.Position, ref agent.TargetNode, agent.Speed, ref agent.Random, Nodes);
            }
        }

        [DisableAutoCreation]
        [BurstCompile(CompileSynchronously = true)]
        private partial struct MoveSystem : ISystem
        {
            [BurstCompile(CompileSynchronously = true)]
            public void OnCreate(ref SystemState state)
            {
                state.RequireForUpdate<RoadNodes>();
            }

            [BurstCompile(CompileSynchronously = true)]
            public void OnUpdate(ref SystemState state)
            {
                var nodes = SystemAPI.GetSingleton<RoadNodes>().Nodes;
                state.Dependency = new MoveJob { Nodes = nodes }.ScheduleParallel(state.Dependency);
            }
        }

        public static BenchmarkResult Run(int agentCount)
        {
            var nodes = BenchmarkScenario.CreateNodes(Allocator.Persistent);
            var random = new Random(1234);
            using var world = new World("EcsBenchmark");
            var entityManager = world.EntityManager;
            var nodesEntity = entityManager.CreateEntity(typeof(RoadNodes));
            entityManager.SetComponentData(nodesEntity, new RoadNodes { Nodes = nodes });
            var archetype = entityManager.CreateArchetype(typeof(AgentMotion));

            var setup = Stopwatch.StartNew();
            using (var created = entityManager.CreateEntity(archetype, agentCount, Allocator.Temp))
            {
                foreach (var entity in created)
                {
                    entityManager.SetComponentData(entity, CreateAgent(ref random, nodes.Length));
                }
            }

            setup.Stop();

            var moveSystem = world.CreateSystem<MoveSystem>();
            var agentQuery = entityManager.CreateEntityQuery(typeof(AgentMotion));
            var total = BenchmarkScenario.WarmupTicks + BenchmarkScenario.MeasuredTicks;
            var ticks = new double[BenchmarkScenario.MeasuredTicks];
            var churn = new double[BenchmarkScenario.MeasuredTicks];
            var watch = new Stopwatch();
            for (var tick = 0; tick < total; tick++)
            {
                watch.Restart();
                moveSystem.Update(world.Unmanaged);
                entityManager.CompleteAllTrackedJobs();
                var moveTicks = watch.Elapsed.TotalMilliseconds;

                // Verloop zoals een echte simulatie het doet: verzamelen in een command buffer, daarna afspelen.
                using (var entities = agentQuery.ToEntityArray(Allocator.TempJob))
                {
                    var commands = new EntityCommandBuffer(Allocator.TempJob);
                    for (var i = 0; i < BenchmarkScenario.ChurnPerTick; i++)
                    {
                        commands.DestroyEntity(entities[random.NextInt(entities.Length)]);
                    }

                    for (var i = 0; i < BenchmarkScenario.ChurnPerTick; i++)
                    {
                        var entity = commands.CreateEntity(archetype);
                        commands.SetComponent(entity, CreateAgent(ref random, nodes.Length));
                    }

                    commands.Playback(entityManager);
                    commands.Dispose();
                }

                watch.Stop();
                if (tick >= BenchmarkScenario.WarmupTicks)
                {
                    ticks[tick - BenchmarkScenario.WarmupTicks] = watch.Elapsed.TotalMilliseconds;
                    churn[tick - BenchmarkScenario.WarmupTicks] = watch.Elapsed.TotalMilliseconds - moveTicks;
                }
            }

            double checksum = 0;
            using (var agents = agentQuery.ToComponentDataArray<AgentMotion>(Allocator.Temp))
            {
                foreach (var agent in agents)
                {
                    checksum += agent.Position.x + agent.Position.z;
                }
            }

            var result = BenchmarkStatistics.Summarize("Unity Entities 6.5 (IJobEntity + EntityCommandBuffer)", agentCount, ticks, churn);
            result.SetupMs = setup.Elapsed.TotalMilliseconds;
            result.MemoryBytes = (long)agentQuery.CalculateChunkCount() * 16 * 1024;
            result.Checksum = checksum;
            nodes.Dispose();
            return result;
        }

        private static AgentMotion CreateAgent(ref Random random, int nodeCount)
        {
            var start = random.NextInt(nodeCount);
            return new AgentMotion
            {
                Position = new float3(start % BenchmarkScenario.GridSize, 0f, start / BenchmarkScenario.GridSize) * BenchmarkScenario.NodeSpacing,
                TargetNode = start,
                Speed = random.NextFloat(8f, 16f),
                Random = new Random(random.NextUInt(1, uint.MaxValue)),
            };
        }
    }
}
