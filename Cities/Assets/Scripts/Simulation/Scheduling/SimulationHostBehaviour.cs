using Cities.Core.Diagnostics;
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

        public SimulationClock Clock { get; private set; }

        public CommandBus Commands { get; private set; }

        public SimulationScheduler Scheduler { get; private set; }

        public SimulationRunner Runner { get; private set; }

        private void Awake()
        {
            ApplyLogLevels();
            Clock = new SimulationClock(_settings);
            Clock.SetSpeed(_initialSpeed);
            Commands = new CommandBus();
            Scheduler = new SimulationScheduler();
            Runner = new SimulationRunner(Clock, Commands, Scheduler);
        }

        private void Update()
        {
            Runner.Update(UnityEngine.Time.unscaledDeltaTime);
        }

        private void OnDestroy()
        {
            Runner?.Dispose();
            Scheduler?.Dispose();
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
