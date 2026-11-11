using System;
using System.Collections.Generic;
using Cities.Gameplay.Economy;
using Cities.Gameplay.Events;
using Cities.Gameplay.Population;
using Cities.Gameplay.Statistics;
using Cities.Gameplay.Tools;
using Cities.Gameplay.Visuals;
using Cities.Simulation.Economy;
using Cities.Simulation.Scheduling;
using Cities.Simulation.Timing;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Gameplay.Expansion
{
    /// <summary>
    /// De plaatsbare M8-voorzieningen in de scène: parken, campus, financieel district, magazijn, haven, luchthaven, promenade, groene stad,
    /// extra vervoer en helikopter. Elke plaatsing telt mee in <see cref="CityVenues"/>; elke dag gaat het saldo naar de bank en elke week
    /// volgt een melding.
    /// </summary>
    public sealed class VenuesHostBehaviour : MonoBehaviour
    {
        [Serializable]
        public struct Placement
        {
            public VenueKind Kind;
            public PrefabPlacementBehaviour Source;
        }

        [SerializeField] private SimulationHostBehaviour _simulation;
        [SerializeField] private PopulationHostBehaviour _population;
        [SerializeField] private EconomyHostBehaviour _economy;
        [SerializeField] private EventsHostBehaviour _events;
        [Tooltip("Component dat het weer levert (IWeatherSource).")]
        [SerializeField] private MonoBehaviour _atmosphere;
        [SerializeField] private Placement[] _placements = Array.Empty<Placement>();

        private readonly Dictionary<GameObject, Action> _undo = new Dictionary<GameObject, Action>();
        private long _weekNet;

        public CityVenues Venues { get; } = new CityVenues();

        private void Start()
        {
            foreach (var placement in _placements)
            {
                if (placement.Source == null)
                {
                    continue;
                }

                var kind = placement.Kind;
                placement.Source.Placed += (go, position) =>
                {
                    var at = new float2(position.x, position.z);
                    Venues.Add(kind, at);
                    _undo[go] = () => Venues.Remove(kind, at);
                };
                placement.Source.Removed += (go, position) =>
                {
                    if (_undo.TryGetValue(go, out var undo))
                    {
                        undo();
                        _undo.Remove(go);
                    }
                };
            }

            _simulation.Clock.Calendar.DayStarted += OnDay;
        }

        private void OnDestroy()
        {
            if (_simulation != null && _simulation.Clock != null)
            {
                _simulation.Clock.Calendar.DayStarted -= OnDay;
            }
        }

        private void OnDay(GameDate date)
        {
            if (Venues.Total == 0)
            {
                return;
            }

            var days = _simulation.Clock.Calendar.DaysElapsed;
            var week = Ledger.WeekOf(days);
            var weather = _atmosphere is IWeatherSource source ? source.CurrentWeather : WeatherKind.Clear;
            var day = Venues.Daily(_population.Population.Population, weather);
            var ledger = _economy.Economy.Ledger;
            ledger.Earn(week, LedgerCategory.Trade, day.Income);
            ledger.Force(week, LedgerCategory.Upkeep, day.Upkeep);
            _weekNet += day.Net;
            if (days > 0 && days % Ledger.DaysPerWeek == 0 && _events != null)
            {
                var text = _weekNet >= 0
                    ? "Voorzieningen: deze week " + _weekNet + " winst."
                    : "Voorzieningen: deze week " + (-_weekNet) + " verlies; overweeg minder onderhoud of meer inwoners.";
                _events.Events.Notifications.Post("venues", _weekNet >= 0 ? Severity.Info : Severity.Warning, text, days * CityEvents.SecondsPerDay);
                _weekNet = 0;
            }
        }
    }
}
