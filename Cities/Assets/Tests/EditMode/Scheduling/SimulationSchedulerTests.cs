using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Simulation.Scheduling;
using Cities.Simulation.Timing;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.TestTools;

namespace Cities.Tests.EditMode.Scheduling
{
    /// <summary>Plant echte Burst-jobs in en draait daarom alleen in Unity.</summary>
    [Category("UnityRuntime")]
    public class SimulationSchedulerTests
    {
        /// <summary>Schrijft per tick een getal naar een gedeeld logboek; de volgorde in het logboek is de uitvoervolgorde.</summary>
        private sealed class LoggingSystem : ISimulationSystem
        {
            private readonly NativeList<int> _log;
            private readonly int _id;

            public LoggingSystem(string name, int id, NativeList<int> log)
            {
                Name = name;
                _id = id;
                _log = log;
            }

            public string Name { get; }

            public int ScheduleCount { get; private set; }

            public JobHandle Schedule(in SimulationTick tick, JobHandle dependency)
            {
                ScheduleCount++;
                return new AppendJob { Log = _log, Value = _id }.Schedule(dependency);
            }
        }

        private sealed class ThrowingSystem : ISimulationSystem
        {
            public string Name => "Kapot";

            public JobHandle Schedule(in SimulationTick tick, JobHandle dependency) => throw new InvalidOperationException("stuk");
        }

        private sealed class DisposableSystem : ISimulationSystem, IDisposable
        {
            private readonly List<string> _disposed;

            public DisposableSystem(string name, List<string> disposed)
            {
                Name = name;
                _disposed = disposed;
            }

            public string Name { get; }

            public JobHandle Schedule(in SimulationTick tick, JobHandle dependency) => dependency;

            public void Dispose() => _disposed.Add(Name);
        }

        [BurstCompile]
        private struct AppendJob : IJob
        {
            public NativeList<int> Log;
            public int Value;

            public void Execute() => Log.Add(Value);
        }

        private static readonly SimulationTick Tick = new SimulationTick(1, 0.1f, new GameDate(2000, 1, 1), 0f);

        private NativeList<int> _log;
        private SimulationScheduler _scheduler;

        [SetUp]
        public void SetUp()
        {
            _log = new NativeList<int>(64, Allocator.Persistent);
            _scheduler = new SimulationScheduler();
        }

        [TearDown]
        public void TearDown()
        {
            _scheduler.Dispose();
            _log.Dispose();
        }

        [Test]
        public void SystemsRunInDependencyOrder()
        {
            var terrain = new LoggingSystem("Terrein", 1, _log);
            var water = new LoggingSystem("Water", 2, _log);
            var pollution = new LoggingSystem("Vervuiling", 3, _log);
            _scheduler.Register(pollution, water);
            _scheduler.Register(water, terrain);
            _scheduler.Register(terrain);

            _scheduler.ScheduleTick(Tick);
            _scheduler.Complete();

            Assert.That(_scheduler.Systems.Select(s => s.Name), Is.EqualTo(new[] { "Terrein", "Water", "Vervuiling" }));
            Assert.That(_log.AsArray().ToArray(), Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void ConsecutiveTicksDoNotOverlap()
        {
            var a = new LoggingSystem("A", 1, _log);
            var b = new LoggingSystem("B", 2, _log);
            _scheduler.Register(a);
            _scheduler.Register(b, a);

            for (var i = 0; i < 5; i++)
            {
                _scheduler.ScheduleTick(Tick);
            }

            _scheduler.Complete();

            Assert.That(_log.AsArray().ToArray(), Is.EqualTo(new[] { 1, 2, 1, 2, 1, 2, 1, 2, 1, 2 }));
            Assert.That(a.ScheduleCount, Is.EqualTo(5));
        }

        [Test]
        public void SchedulingDoesNotWaitOnJobs()
        {
            _scheduler.Register(new LoggingSystem("A", 1, _log));

            for (var i = 0; i < 10; i++)
            {
                _scheduler.ScheduleTick(Tick);
            }

            var syncPoints = _scheduler.SyncPointCount;
            _scheduler.Complete();

            Assert.That(syncPoints, Is.EqualTo(0));
            Assert.That(_log.Length, Is.EqualTo(10));
            Assert.That(_scheduler.LastTickHandle.IsCompleted, Is.True);
        }

        [Test]
        public void CompleteCountsSyncPointOnlyWhenWorkIsPending()
        {
            _scheduler.Complete();
            Assert.That(_scheduler.SyncPointCount, Is.EqualTo(0));

            _scheduler.Register(new LoggingSystem("A", 1, _log));
            _scheduler.ScheduleTick(Tick);
            _scheduler.Complete();
            _scheduler.Complete();

            Assert.That(_scheduler.SyncPointCount, Is.LessThanOrEqualTo(1));
        }

        [Test]
        public void FailingSystemIsNamedAndEarlierJobsFinish()
        {
            var ok = new LoggingSystem("Goed", 1, _log);
            var broken = new ThrowingSystem();
            _scheduler.Register(ok);
            _scheduler.Register(broken, ok);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"\[Scheduler\] Systeem Kapot faalde"));
            var error = Assert.Throws<InvalidOperationException>(() => _scheduler.ScheduleTick(Tick));

            Assert.That(error.Message, Does.Contain("Kapot"));
            Assert.That(error.InnerException?.Message, Is.EqualTo("stuk"));
            Assert.That(_log.Length, Is.EqualTo(1), "Het eerdere systeem is afgerond");
        }

        [Test]
        public void CycleIsRejectedWhenOrderIsNeeded()
        {
            var a = new LoggingSystem("A", 1, _log);
            var b = new LoggingSystem("B", 2, _log);
            _scheduler.Register(a, b);
            _scheduler.Register(b, a);

            var error = Assert.Throws<InvalidOperationException>(() => _scheduler.ScheduleTick(Tick));
            Assert.That(error.Message, Does.Contain("A, B"), "Systeemnamen in de melding");
        }

        [Test]
        public void DisposeDisposesSystemsInReverseOrder()
        {
            var disposed = new List<string>();
            var scheduler = new SimulationScheduler();
            var first = new DisposableSystem("Eerste", disposed);
            scheduler.Register(first);
            scheduler.Register(new DisposableSystem("Tweede", disposed), first);

            scheduler.Dispose();
            scheduler.Dispose();

            Assert.That(disposed, Is.EqualTo(new[] { "Tweede", "Eerste" }));
            Assert.Throws<ObjectDisposedException>(() => scheduler.Register(new DisposableSystem("Derde", disposed)));
        }
    }
}
