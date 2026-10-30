using Cities.Core.Diagnostics;
using Cities.Core.Randomness;
using Cities.Simulation.Commands;
using Cities.Simulation.Timing;
using UnityEngine;

namespace Cities.Simulation.Scheduling
{
    /// <summary>
    /// Eigenaar van de simulatie in een scene: klok, command-bus, scheduler en runner. Gebruikt ongeschaalde tijd,
    /// zodat <c>Time.timeScale</c> de simulatie niet beïnvloedt. Systemen registreren zich in hun eigen
    /// <c>Awake</c>/<c>Start</c> via <see cref="Scheduler"/>; deze component draait als een van de eersten.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public sealed class SimulationHostBehaviour : MonoBehaviour
    {
        [SerializeField] private SimulationClockSettings _settings = SimulationClockSettings.Default;
        [SerializeField] private SimulationSpeed _initialSpeed = SimulationSpeed.Normal;

        [Tooltip("Logniveaus per systeem; opstartargument -loglevel gaat hier overheen.")]
        [SerializeField] private LogConfiguration _logConfiguration;

        [Tooltip("Wereldseed (decimaal of 0x-hex). Leeg = elke start een nieuwe; opstartargument -seed gaat hier overheen.")]
        [SerializeField] private string _seed = string.Empty;

        public SimulationClock Clock { get; private set; }

        public CommandBus Commands { get; private set; }

        public SimulationScheduler Scheduler { get; private set; }

        public SimulationRunner Runner { get; private set; }

        public SimulationRandom Random { get; private set; }

        private readonly System.Collections.Generic.List<System.IDisposable> _debugCounters = new System.Collections.Generic.List<System.IDisposable>();

        private void RegisterDebugCounters(DebugRegistry registry)
        {
            const string category = "Simulatie";
            _debugCounters.Add(registry.AddCounter(category, "Tick", () => Clock.Tick));
            _debugCounters.Add(registry.AddCounter(category, "Snelheid", () => (long)Clock.Speed));
            _debugCounters.Add(registry.AddCounter(category, "Overgeslagen ticks", () => Clock.DroppedTicks));
            _debugCounters.Add(registry.AddCounter(category, "Systemen", () => Scheduler.Systems.Count));
            _debugCounters.Add(registry.AddCounter(category, "Synchronisatiepunten", () => Scheduler.SyncPointCount));
            _debugCounters.Add(registry.AddCounter(category, "Openstaande commands", () => Commands.PendingCount));
        }

        private void Awake()
        {
            ApplyLogLevels();
            Random = new SimulationRandom(ChooseSeed());
            Clock = new SimulationClock(_settings);
            Clock.SetSpeed(_initialSpeed);
            Commands = new CommandBus();
            Scheduler = new SimulationScheduler();
            Runner = new SimulationRunner(Clock, Commands, Scheduler, Random);
            RegisterDebugCounters(DebugRegistry.Shared);
        }

        private void Update()
        {
            Runner.Update(UnityEngine.Time.unscaledDeltaTime);
        }

        private void OnDestroy()
        {
            foreach (var registration in _debugCounters)
            {
                registration.Dispose();
            }

            _debugCounters.Clear();
            Runner?.Dispose();
            Scheduler?.Dispose();
        }

        /// <summary>Opstartargument, dan de inspector, anders een nieuwe seed; altijd gelogd, zodat een bug na te spelen is.</summary>
        private ulong ChooseSeed()
        {
            ulong seed;
            string origin;
            if (SimulationRandom.TryReadCommandLine(System.Environment.GetCommandLineArgs(), out seed, out var error))
            {
                origin = "opstartargument";
            }
            else if (SimulationRandom.TryParse(_seed, out seed))
            {
                origin = "inspector";
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(_seed))
                {
                    Debug.LogWarning($"Seed '{_seed}' is geen getal; er wordt een nieuwe gekozen.");
                }

                seed = SeedHash.NewSeed();
                origin = "nieuw";
            }

            if (error != null)
            {
                Debug.LogWarning(error);
            }

            Debug.Log($"Wereldseed {SimulationRandom.Format(seed)} ({origin}); start opnieuw met -seed {SimulationRandom.Format(seed)}.");
            return seed;
        }

        private void ApplyLogLevels()
        {
            if (_logConfiguration != null)
            {
                _logConfiguration.Apply();
            }

            var errors = new System.Collections.Generic.List<string>();
            LogLevels.ApplyCommandLine(System.Environment.GetCommandLineArgs(), errors);
            foreach (var error in errors)
            {
                Debug.LogWarning(error);
            }
        }

        private void OnValidate()
        {
            _settings.TicksPerSecond = Mathf.Max(1, _settings.TicksPerSecond);
            _settings.MaxTicksPerUpdate = Mathf.Max(1, _settings.MaxTicksPerUpdate);
            _settings.MaxFrameSeconds = Mathf.Max(0.01f, _settings.MaxFrameSeconds);
            _settings.TicksPerDay = Mathf.Max(1, _settings.TicksPerDay);
            _settings.StartYear = Mathf.Clamp(_settings.StartYear, 1, 9000);
            _settings.StartMonth = Mathf.Clamp(_settings.StartMonth, 1, 12);
            _settings.StartDay = Mathf.Clamp(_settings.StartDay, 1, System.DateTime.DaysInMonth(_settings.StartYear, _settings.StartMonth));
        }
    }
}
