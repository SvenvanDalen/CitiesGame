using System.Collections;
using Cities.Simulation.Scheduling;
using Cities.Simulation.Timing;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.TestTools;

namespace Cities.Tests.PlayMode.Timing
{
    public class SimulationHostBehaviourTests
    {
        private GameObject _host;

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_host);
        }

        [UnityTest]
        public IEnumerator ClockAdvancesWithRealTimeAndIgnoresTimeScale()
        {
            _host = new GameObject("Simulation");
            var behaviour = _host.AddComponent<SimulationHostBehaviour>();
            var previousScale = UnityEngine.Time.timeScale;
            UnityEngine.Time.timeScale = 0f;
            try
            {
                yield return new WaitForSecondsRealtime(0.5f);
            }
            finally
            {
                UnityEngine.Time.timeScale = previousScale;
            }

            Assert.That(behaviour.Clock.Tick, Is.InRange(3, 7));
        }

        [UnityTest]
        public IEnumerator PausedClockDoesNotTick()
        {
            _host = new GameObject("Simulation");
            var behaviour = _host.AddComponent<SimulationHostBehaviour>();
            behaviour.Clock.SetSpeed(SimulationSpeed.Paused);

            yield return new WaitForSecondsRealtime(0.3f);

            Assert.That(behaviour.Clock.Tick, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator SystemsRunEveryTickWithoutSyncPoints()
        {
            _host = new GameObject("Simulation");
            var behaviour = _host.AddComponent<SimulationHostBehaviour>();
            behaviour.Clock.SetSpeed(SimulationSpeed.Fastest);
            var counter = new CountingSystem();
            behaviour.Scheduler.Register(counter);

            yield return new WaitForSecondsRealtime(0.5f);
            var syncPointsWhileRunning = behaviour.Scheduler.SyncPointCount;
            behaviour.Scheduler.Complete();

            Assert.That(syncPointsWhileRunning, Is.EqualTo(0), "Zonder commands wacht de main thread nooit op de simulatie");
            Assert.That(counter.Count, Is.EqualTo(behaviour.Runner.ScheduledTicks));
            Assert.That(counter.Count, Is.GreaterThanOrEqualTo(10));
        }

        private sealed class CountingSystem : ISimulationSystem, System.IDisposable
        {
            private NativeArray<long> _count = new NativeArray<long>(1, Allocator.Persistent);

            public string Name => "Teller";

            public long Count => _count[0];

            public JobHandle Schedule(in SimulationTick tick, JobHandle dependency) =>
                new IncrementJob { Count = _count }.Schedule(dependency);

            public void Dispose() => _count.Dispose();
        }

        [BurstCompile]
        private struct IncrementJob : IJob
        {
            public NativeArray<long> Count;

            public void Execute() => Count[0]++;
        }
    }
}
