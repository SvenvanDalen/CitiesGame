using Cities.Gameplay.Economy;
using Cities.Gameplay.Events;
using Cities.Gameplay.Population;
using Cities.Gameplay.Statistics;
using Cities.Gameplay.Scenarios;
using Cities.Gameplay.Tools;
using Cities.Gameplay.Traffic;
using Cities.Gameplay.Utilities;
using Cities.Gameplay.Visuals;
using Cities.Gameplay.Zoning;
using Cities.Simulation.Scheduling;
using Cities.Simulation.Timing;
using UnityEngine;

namespace Cities.Gameplay.Expansion
{
    /// <summary>
    /// De M8-uitbreidingen in de scène: laat <see cref="CityExpansion"/> elke dag meedraaien en koppelt ongevallen en wegdek aan het verkeer.
    /// De schakelaars staan standaard uit voor alles wat het spel moeilijker maakt.
    /// </summary>
    public sealed class ExpansionHostBehaviour : MonoBehaviour
    {
        [SerializeField] private SimulationHostBehaviour _simulation;
        [SerializeField] private PopulationHostBehaviour _population;
        [SerializeField] private BuildingSpawnerBehaviour _spawner;
        [SerializeField] private TrafficHostBehaviour _traffic;
        [Tooltip("Component dat het weer levert (IWeatherSource), bijvoorbeeld de dag-en-nacht-host.")]
        [SerializeField] private MonoBehaviour _atmosphere;
        [SerializeField] private EventsHostBehaviour _events;
        [SerializeField] private ToolsBehaviour _tools;
        [SerializeField] private UtilitiesHostBehaviour _utilities;
        [SerializeField] private EconomyHostBehaviour _economy;

        [Header("Weergave (standaard aan)")]
        [SerializeField] private bool _seasons = true;
        [SerializeField] private bool _hints = true;
        [SerializeField] private bool _timelapse = true;

        [Header("Met gevolgen (standaard uit)")]
        [SerializeField] private bool _accidents;
        [SerializeField] private bool _winter;
        [SerializeField] private bool _heatwaves;
        [SerializeField] private bool _climateEffects;

        public CityExpansion Expansion { get; private set; }

        private void Awake()
        {
            Expansion = new CityExpansion(_simulation.Random.Seed);
            Apply();
            Expansion.Hints.Register("road", "Sleep over de grond om een weg te leggen; klik opnieuw om door te trekken.");
            Expansion.Hints.Register("zone", "Schilder een zone naast een weg: daar bouwen de inwoners zelf.");
            Expansion.Hints.Register("bulldoze", "Sloop wat je aanwijst; je krijgt een deel van de kosten terug.");
        }

        private void Start()
        {
            _simulation.Clock.Calendar.DayStarted += OnDay;
            if (_traffic != null && _traffic.Traffic != null)
            {
                _traffic.Traffic.Accidents = Expansion.Accidents;
            }

            if (_tools != null)
            {
                _tools.Controller.ActiveChanged += OnToolChosen;
            }
        }

        private void OnDestroy()
        {
            if (_simulation != null && _simulation.Clock != null)
            {
                _simulation.Clock.Calendar.DayStarted -= OnDay;
            }

            if (_tools != null && _tools.Controller != null)
            {
                _tools.Controller.ActiveChanged -= OnToolChosen;
            }
        }

        private void Apply()
        {
            var settings = Expansion.Settings;
            settings.Seasons = _seasons;
            settings.Hints = _hints;
            settings.Timelapse = _timelapse;
            settings.Accidents = _accidents;
            settings.Winter = _winter;
            settings.Heatwaves = _heatwaves;
            settings.ClimateEffects = _climateEffects;
            Expansion.ApplySettings();
        }

        private void OnDay(GameDate date)
        {
            Apply();
            var kind = _atmosphere is IWeatherSource source ? source.CurrentWeather : WeatherKind.Clear;
            var snowing = kind == WeatherKind.Rain || kind == WeatherKind.Storm;
            var buildings = _spawner != null ? _spawner.Built.Count : 0;
            var days = (int)_simulation.Clock.Calendar.DaysElapsed;
            var wasHeatwave = Expansion.Heatwaves.Current != null;
            Expansion.Daily(days, date.Month, _population.Population.Population, buildings, snowing, sinks: 0);
            if (_traffic != null && _traffic.Traffic != null)
            {
                _traffic.Traffic.Grip = Expansion.TrafficGrip;
            }

            if (_utilities != null)
            {
                _utilities.Utilities.SetDemandFactors(Expansion.EnergyDemandFactor, Expansion.WaterDemandFactor);
            }

            if (_economy != null)
            {
                Expansion.EvaluateGoal(days, _population.Population.Population, _economy.Economy.Ledger.Balance);
                if (Expansion.GoalChanged && Expansion.GoalState == GoalState.Reached && _events != null)
                {
                    _events.Events.Notifications.Post("goal", Severity.Info, "Doel gehaald: je eerste stad is een feit!", days * CityEvents.SecondsPerDay);
                }
            }

            if (!wasHeatwave && Expansion.Heatwaves.Current != null && _events != null)
            {
                _events.Events.Notifications.Post("heatwave", Severity.Warning, "Hittegolf: water- en energievraag stijgen.", days * CityEvents.SecondsPerDay);
            }
        }

        private void OnToolChosen(ITool tool)
        {
            if (tool == null || _events == null)
            {
                return;
            }

            var hint = Expansion.Hints.OnToolChosen(tool.Name != null ? tool.Name.ToLowerInvariant() : null);
            if (hint != null)
            {
                _events.Events.Notifications.Post("hint-" + tool.Name, Severity.Info, hint, Time.realtimeSinceStartupAsDouble);
            }
        }
    }
}
