using System;
using System.Collections.Generic;
using Cities.Core.Diagnostics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Profiling;

namespace Cities.Simulation.Scheduling
{
    /// <summary>
    /// Plant de simulatiesystemen per tick in vaste volgorde in. Elk systeem wacht alleen op de vorige tick en op de
    /// systemen waarna het is geregistreerd; onafhankelijke systemen draaien zo parallel. De scheduler roept zelf
    /// nooit <c>Complete</c> aan tijdens een tick: lezers op de main thread (rendering, opslaan, commands) doen dat via
    /// <see cref="Complete"/>, en dat telt als synchronisatiepunt.
    /// </summary>
    /// <remarks>
    /// Elk systeem krijgt een eigen profiler-marker (<c>Sim.&lt;naam&gt;</c>) rond het inplannen; daarnaast meten
    /// <c>Sim.Tick</c> de hele tick en <c>Sim.Commands</c> het werk op de main thread. Jobs die een systeem inplant,
    /// verschijnen in de profiler onder hun eigen naam op de worker threads.
    /// </remarks>
    public sealed class SimulationScheduler : ITickScheduler, IDisposable
    {
        public const string MarkerPrefix = "Sim.";

        private static readonly ProfilerMarker TickMarker = new ProfilerMarker(ProfilerCategory.Scripts, MarkerPrefix + "Tick");
        private static readonly ProfilerMarker CommandsMarker = new ProfilerMarker(ProfilerCategory.Scripts, MarkerPrefix + "Commands");

        private readonly SystemGraph<ISimulationSystem> _graph = new SystemGraph<ISimulationSystem>(system => system.Name);
        private readonly SystemLog _log = new SystemLog("Scheduler");
        private ProfilerMarker[] _markers = Array.Empty<ProfilerMarker>();
        private readonly Dictionary<ISimulationSystem, int> _slot = new Dictionary<ISimulationSystem, int>();
        private ISimulationSystem[] _order = Array.Empty<ISimulationSystem>();
        private int[][] _predecessorSlots = Array.Empty<int[]>();
        private JobHandle[] _handles = Array.Empty<JobHandle>();
        private NativeList<JobHandle> _combine;
        private JobHandle _lastTick;
        private bool _orderDirty;
        private bool _disposed;

        public SimulationScheduler()
        {
            _combine = new NativeList<JobHandle>(16, Allocator.Persistent);
        }

        /// <summary>Systemen in uitvoervolgorde.</summary>
        public IReadOnlyList<ISimulationSystem> Systems
        {
            get
            {
                EnsureOrder();
                return _order;
            }
        }

        /// <summary>
        /// Meetmodus voor het debugpaneel (E24-02): elk systeem wordt direct na het inplannen afgewacht en gemeten, zodat
        /// <see cref="Timings"/> de echte tijd per systeem bevat. De systemen draaien dan na elkaar en de main thread wacht;
        /// alleen voor diagnose, niet in normaal spel.
        /// </summary>
        public bool MeasureSystems { get; set; }

        /// <summary>Gemeten tijden per systeem en per tick zolang <see cref="MeasureSystems"/> aan staat.</summary>
        public SystemTimings Timings { get; } = new SystemTimings();

        /// <summary>Aantal keren dat de main thread op jobs moest wachten; voor diagnose (E24-02) en tests.</summary>
        public int SyncPointCount { get; private set; }

        /// <summary>Handle van alle jobs van de laatst ingeplande tick.</summary>
        public JobHandle LastTickHandle => _lastTick;

        /// <summary>Naam van de profiler-marker van een systeem.</summary>
        public static string MarkerNameOf(ISimulationSystem system) => MarkerPrefix + system.Name;

        /// <summary>Registreert een systeem dat na <paramref name="after"/> draait.</summary>
        public void Register(ISimulationSystem system, params ISimulationSystem[] after)
        {
            ThrowIfDisposed();
            _graph.Add(system, after);
            _orderDirty = true;
        }

        /// <summary>Plant alle systemen voor één tick in en start de workers, zonder te wachten.</summary>
        public JobHandle ScheduleTick(in SimulationTick tick) => ScheduleTickInternal(tick);

        void ITickScheduler.ScheduleTick(in SimulationTick tick) => ScheduleTickInternal(tick);

        private JobHandle ScheduleTickInternal(in SimulationTick tick)
        {
            ThrowIfDisposed();
            EnsureOrder();
            using var tickScope = TickMarker.Auto();
            var tickStart = _lastTick;
            var measure = MeasureSystems;
            if (measure)
            {
                tickStart.Complete();
            }

            var tickWatch = measure ? System.Diagnostics.Stopwatch.StartNew() : null;
            for (var i = 0; i < _order.Length; i++)
            {
                _combine.Clear();
                _combine.Add(tickStart);
                foreach (var predecessor in _predecessorSlots[i])
                {
                    _combine.Add(_handles[predecessor]);
                }

                var dependency = JobHandle.CombineDependencies(_combine.AsArray());
                if (measure)
                {
                    _handles[i] = ScheduleMeasured(i, tick, dependency);
                    continue;
                }

                using (_markers[i].Auto())
                {
                    _handles[i] = ScheduleSystem(_order[i], tick, dependency);
                }
            }

            if (measure)
            {
                Timings.Record(SystemTimings.TickName, tickWatch.Elapsed.TotalMilliseconds);
            }

            _combine.Clear();
            _combine.Add(tickStart);
            foreach (var handle in _handles)
            {
                _combine.Add(handle);
            }

            _lastTick = JobHandle.CombineDependencies(_combine.AsArray());
            JobHandle.ScheduleBatchedJobs();
            return _lastTick;
        }

        private JobHandle ScheduleMeasured(int slot, in SimulationTick tick, JobHandle dependency)
        {
            dependency.Complete();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            JobHandle handle;
            using (_markers[slot].Auto())
            {
                handle = ScheduleSystem(_order[slot], tick, dependency);
            }

            handle.Complete();
            Timings.Record(_order[slot].Name, watch.Elapsed.TotalMilliseconds);
            return handle;
        }

        /// <summary>Wacht tot alle ingeplande simulatiejobs klaar zijn. Telt als synchronisatiepunt als er nog werk liep.</summary>
        public void Complete()
        {
            if (!_lastTick.IsCompleted)
            {
                SyncPointCount++;
            }

            _lastTick.Complete();
        }

        public void ExecuteOnMainThread(Action work)
        {
            if (work == null)
            {
                throw new ArgumentNullException(nameof(work));
            }

            Complete();
            using (CommandsMarker.Auto())
            {
                work();
            }
        }

        /// <summary>Wacht op alle jobs en ruimt systemen op die <see cref="IDisposable"/> zijn, in omgekeerde volgorde.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _lastTick.Complete();

            // Niet opnieuw sorteren: bij een ongeldige registratie (cyclus) moet opruimen nog steeds lukken.
            IReadOnlyList<ISimulationSystem> systems = _orderDirty ? _graph.Nodes : _order;
            for (var i = systems.Count - 1; i >= 0; i--)
            {
                (systems[i] as IDisposable)?.Dispose();
            }

            _combine.Dispose();
            _disposed = true;
        }

        private JobHandle ScheduleSystem(ISimulationSystem system, in SimulationTick tick, JobHandle dependency)
        {
            try
            {
                return system.Schedule(tick, dependency);
            }
            catch (Exception exception)
            {
                // Laat de rest van de tick niet zonder afhankelijkheden achter: wacht op wat er al liep en meld wie faalde.
                dependency.Complete();
                _log.Error(exception, $"Systeem {system.Name} faalde bij inplannen van tick {tick.Tick}");
                throw new InvalidOperationException($"Systeem {system.Name} faalde bij inplannen van tick {tick.Tick}.", exception);
            }
        }

        private void EnsureOrder()
        {
            if (!_orderDirty)
            {
                return;
            }

            // Pas de volgorde alleen aan tussen ticks: wacht eerst op lopende jobs.
            _lastTick.Complete();
            var order = _graph.Sort();
            _slot.Clear();
            for (var i = 0; i < order.Count; i++)
            {
                _slot.Add(order[i], i);
            }

            _order = order.ToArray();
            _predecessorSlots = new int[_order.Length][];
            for (var i = 0; i < _order.Length; i++)
            {
                var predecessors = _graph.PredecessorsOf(_order[i]);
                _predecessorSlots[i] = new int[predecessors.Count];
                for (var p = 0; p < predecessors.Count; p++)
                {
                    _predecessorSlots[i][p] = _slot[predecessors[p]];
                }
            }

            _handles = new JobHandle[_order.Length];
            _markers = new ProfilerMarker[_order.Length];
            for (var i = 0; i < _order.Length; i++)
            {
                _markers[i] = new ProfilerMarker(ProfilerCategory.Scripts, MarkerNameOf(_order[i]));
            }

            _orderDirty = false;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SimulationScheduler));
            }
        }
    }
}
