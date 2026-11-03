using System;
using System.IO;
using Cities.Simulation.Scheduling;
using UnityEngine;

namespace Cities.Gameplay.Diagnostics
{
    /// <summary>
    /// Schrijft tick-tijden per systeem naar <c>persistentDataPath/Telemetry/telemetry-yyyyMMdd-HHmmss.csv</c> (E24-07).
    /// Eén per scene, naast de <see cref="SimulationHostBehaviour"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TelemetryBehaviour : MonoBehaviour
    {
        [SerializeField] private SimulationHostBehaviour _host;
        [SerializeField, Min(1)] private int _intervalTicks = 60;

        private TelemetryWriter _writer;

        public static string Directory => Path.Combine(Application.persistentDataPath, "Telemetry");

        /// <summary>De schrijver, bijvoorbeeld om tellers aan te melden; <c>null</c> zolang er niet gestart is.</summary>
        public TelemetryWriter Writer => _writer;

        private void Start()
        {
            if (_host == null || _host.Scheduler == null || _host.Clock == null)
            {
                enabled = false;
                return;
            }

            System.IO.Directory.CreateDirectory(Directory);
            var path = Path.Combine(Directory, "telemetry-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture) + ".csv");
            _writer = new TelemetryWriter(new StreamWriter(path), _host.Scheduler.Timings, _intervalTicks, true);
            _host.Clock.Ticked += OnTicked;
        }

        private void OnTicked(long tick) => _writer.OnTick(tick);

        private void OnDestroy()
        {
            if (_writer != null)
            {
                if (_host != null && _host.Clock != null)
                {
                    _host.Clock.Ticked -= OnTicked;
                }

                _writer.Dispose();
                _writer = null;
            }
        }
    }
}
