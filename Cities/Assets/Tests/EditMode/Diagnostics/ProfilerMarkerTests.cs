using Cities.Simulation.Scheduling;
using Cities.Simulation.Timing;
using NUnit.Framework;
using Unity.Jobs;
using Unity.Profiling;

namespace Cities.Tests.EditMode.Diagnostics
{
    /// <summary>Gebruikt de Unity-profiler en draait daarom alleen in Unity.</summary>
    [Category("UnityRuntime")]
    public class ProfilerMarkerTests
    {
        private sealed class IdleSystem : ISimulationSystem
        {
            public IdleSystem(string name) => Name = name;

            public string Name { get; }

            public JobHandle Schedule(in SimulationTick tick, JobHandle dependency) => dependency;
        }

        [Test]
        public void EverySystemGetsItsOwnMarker()
        {
            using var scheduler = new SimulationScheduler();
            var traffic = new IdleSystem("TrafficTest");
            var water = new IdleSystem("WaterTest");
            scheduler.Register(traffic);
            scheduler.Register(water, traffic);

            scheduler.ScheduleTick(new SimulationTick(1, 0.1f, new GameDate(2000, 1, 1), 0f));

            foreach (var name in new[] { "Sim.TrafficTest", "Sim.WaterTest", "Sim.Tick", "Sim.Commands" })
            {
                using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, name);
                Assert.That(recorder.Valid, Is.True, $"Marker {name} bestaat");
            }

            Assert.That(SimulationScheduler.MarkerNameOf(traffic), Is.EqualTo("Sim.TrafficTest"));
        }

        [Test]
        public void ExecuteOnMainThreadRunsWork()
        {
            using var scheduler = new SimulationScheduler();
            var ran = false;

            scheduler.ExecuteOnMainThread(() => ran = true);

            Assert.That(ran, Is.True);
            Assert.Throws<System.ArgumentNullException>(() => scheduler.ExecuteOnMainThread(null));
        }
    }
}
