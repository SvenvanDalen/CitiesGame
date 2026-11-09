using Cities.Gameplay.Economy;
using Cities.Gameplay.Tools;
using Cities.Simulation.Economy;
using Cities.Simulation.Industry;
using Cities.Simulation.Scheduling;
using Cities.Simulation.Timing;
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

        private readonly System.Collections.Generic.Dictionary<GameObject, System.Action> _undo = new System.Collections.Generic.Dictionary<GameObject, System.Action>();

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
                    placement.Source.Placed += (go, position) => _undo[go] = OnPlaced(kind);
                    placement.Source.Removed += (go, position) =>
                    {
                        if (_undo.TryGetValue(go, out var undo))
                        {
                            undo();
                            _undo.Remove(go);
                        }
                    };
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

        /// <summary>Voegt toe wat bij de plaatsing hoort en geeft terug hoe dat weer weg te halen is (E01-12).</summary>
        private System.Action OnPlaced(IndustryPlacementKind kind)
        {
            switch (kind)
            {
                case IndustryPlacementKind.Agriculture: return Site(IndustryKind.Agriculture);
                case IndustryPlacementKind.Forestry: return Site(IndustryKind.Forestry);
                case IndustryPlacementKind.Ore: return Site(IndustryKind.Ore);
                case IndustryPlacementKind.Oil: return Site(IndustryKind.Oil);
                case IndustryPlacementKind.Attraction:
                    var attraction = Industry.AddAttraction("Trekpleister " + Industry.Tourism.NextId, 60, 8, 400);
                    return () => Industry.RemoveAttraction(attraction);
                default:
                    Industry.AddHotel();
                    return Industry.RemoveHotel;
            }
        }

        private System.Action Site(IndustryKind kind)
        {
            var site = Industry.AddSite(kind);
            return () => Industry.RemoveSite(site.Id);
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
