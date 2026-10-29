using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;

namespace Cities.Simulation.Scheduling
{
    /// <summary>
    /// Plant de simulatiesystemen per tick in vaste volgorde in. Elk systeem wacht alleen op de vorige tick en op de
    /// systemen waarna het is geregistreerd; onafhankelijke systemen draaien zo parallel. De scheduler roept zelf
    /// nooit <c>Complete</c> aan tijdens een tick: lezers op de main thread (rendering, opslaan, commands) doen dat via
    /// <see cref="Complete"/>, en dat telt als synchronisatiepunt.
    /// </summary>
    public sealed class SimulationScheduler : ITickScheduler, IDisposable
    {
        private readonly SystemGraph<ISimulationSystem> _graph = new SystemGraph<ISimulationSystem>(system => system.Name);
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

        /// <summary>Aantal keren dat de main thread op jobs moest wachten; voor diagnose (E24-02) en tests.</summary>
        public int SyncPointCount { get; private set; }

        /// <summary>Handle van alle jobs van de laatst ingeplande tick.</summary>
        public JobHandle LastTickHandle => _lastTick;

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
            var tickStart = _lastTick;
            for (var i = 0; i < _order.Length; i++)
            {
                _combine.Clear();
                _combine.Add(tickStart);
                foreach (var predecessor in _predecessorSlots[i])
                {
                    _combine.Add(_handles[predecessor]);
                }

                var dependency = JobHandle.CombineDependencies(_combine.AsArray());
                _handles[i] = ScheduleSystem(_order[i], tick, dependency);
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

        /// <summary>Wacht tot alle ingeplande simulatiejobs klaar zijn. Telt als synchronisatiepunt als er nog werk liep.</summary>
        public void Complete()
        {
            if (!_lastTick.IsCompleted)
            {
                SyncPointCount++;
            }

            _lastTick.Complete();
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

        private static JobHandle ScheduleSystem(ISimulationSystem system, in SimulationTick tick, JobHandle dependency)
        {
            try
            {
                return system.Schedule(tick, dependency);
            }
            catch (Exception exception)
            {
                // Laat de rest van de tick niet zonder afhankelijkheden achter: wacht op wat er al liep en meld wie faalde.
                dependency.Complete();
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
