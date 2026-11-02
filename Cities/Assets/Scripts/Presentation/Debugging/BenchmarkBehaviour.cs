using Cities.Simulation.Benchmark;
using Cities.Simulation.Scheduling;
using UnityEngine;

namespace Cities.Presentation.Debugging
{
    /// <summary>
    /// Meetscene van E23-02: zet het meten per systeem aan, wacht <see cref="_seconds"/> en schrijft het <see cref="BenchmarkReport"/> naar de
    /// log. Het aantal inwoners komt van de bevolkingssimulatie en wordt nu met de hand ingevuld (<see cref="_population"/>).
    /// </summary>
    public sealed class BenchmarkBehaviour : MonoBehaviour
    {
        [SerializeField] private SimulationHostBehaviour _simulation;
        [SerializeField] private int _population = 10_000;
        [SerializeField] private float _seconds = 30f;

        private float _elapsed;
        private bool _done;

        private void Start() => _simulation.Scheduler.MeasureSystems = true;

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
            if (_done || _elapsed < _seconds)
            {
                return;
            }

            _done = true;
            var report = BenchmarkReport.Evaluate(_population, _simulation.Scheduler.Timings);
            if (report.Passed)
            {
                Debug.Log(report.Text);
            }
            else
            {
                Debug.LogError(report.Text);
            }
        }
    }
}
