using UnityEngine;

namespace Cities.Simulation.Timing
{
    /// <summary>
    /// Koppelt de <see cref="SimulationClock"/> aan de Unity-speellus. Gebruikt ongeschaalde tijd,
    /// zodat <c>Time.timeScale</c> de simulatie niet beïnvloedt.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SimulationClockBehaviour : MonoBehaviour
    {
        [SerializeField] private SimulationClockSettings _settings = SimulationClockSettings.Default;
        [SerializeField] private SimulationSpeed _initialSpeed = SimulationSpeed.Normal;

        public SimulationClock Clock { get; private set; }

        private void Awake()
        {
            Clock = new SimulationClock(_settings);
            Clock.SetSpeed(_initialSpeed);
        }

        private void Update()
        {
            Clock.Advance(UnityEngine.Time.unscaledDeltaTime);
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
