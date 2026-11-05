using Cities.Gameplay.Economy;
using Cities.Gameplay.Tools;
using Cities.Simulation.Economy;
using Cities.Simulation.Industry;
using Cities.Simulation.Scheduling;
using Cities.Simulation.Timing;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Gameplay.Industry
{
    /// <summary>Soort plaatsbaar bedrijf van de industrie- en toerismehost.</summary>
    public enum IndustryPlacementKind
    {
        Agriculture,
        Forestry,
        Ore,
        Oil,
        Attraction,
        Hotel,
    }

    /// <summary>
    /// Industrie, handel en toerisme in de scene (E26-11): winningen, trekpleisters en hotels plaatsen met de werkbalk; elke spelweek gaan
    /// export en toerisme naar het saldo.
    /// </summary>
    public sealed class IndustryHostBehaviour : MonoBehaviour
    {
        [System.Serializable]
        public struct Placement
        {
            public IndustryPlacementKind Kind;
            public PrefabPlacementBehaviour Source;
        }

        [SerializeField] private SimulationHostBehaviour _simulation;
        [SerializeField] private EconomyHostBehaviour _economy;
        [SerializeField] private Placement[] _placements = System.Array.Empty<Placement>();

        public CityIndustry Industry { get; } = new CityIndustry();

        /// <summary>De laatst afgesloten week, voor het scherm.</summary>
        public CityIndustryWeek LastWeek { get; private set; }

        private void Start()
        {
            foreach (var placement in _placements)
            {
                if (placement.Source != null)
                {
                    var kind = placement.Kind;
                    placement.Source.Placed += (go, position) => OnPlaced(kind, position);
                }
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

        private void OnPlaced(IndustryPlacementKind kind, float3 position)
        {
            switch (kind)
            {
                case IndustryPlacementKind.Agriculture: Industry.AddSite(IndustryKind.Agriculture); break;
                case IndustryPlacementKind.Forestry: Industry.AddSite(IndustryKind.Forestry); break;
                case IndustryPlacementKind.Ore: Industry.AddSite(IndustryKind.Ore); break;
                case IndustryPlacementKind.Oil: Industry.AddSite(IndustryKind.Oil); break;
                case IndustryPlacementKind.Attraction: Industry.AddAttraction("Trekpleister " + Industry.Tourism.NextId, 60, 8, 400); break;
                default: Industry.AddHotel(); break;
            }
        }

        private void OnDay(GameDate date)
        {
            var days = _simulation.Clock.Calendar.DaysElapsed;
            if (days > 0 && days % Ledger.DaysPerWeek == 0)
            {
                LastWeek = Industry.EndWeek(Ledger.WeekOf(days), _economy.Economy.Ledger);
            }
        }
    }
}
