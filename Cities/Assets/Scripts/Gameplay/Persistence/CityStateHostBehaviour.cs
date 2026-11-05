using System.Collections.Generic;
using System.Linq;
using Cities.Gameplay.Economy;
using Cities.Gameplay.Industry;
using Cities.Gameplay.Population;
using Cities.Gameplay.Services;
using Cities.Gameplay.Transit;
using Cities.Gameplay.Zoning;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Gameplay.Persistence
{
    /// <summary>
    /// Verzamelt en herstelt de toestand van de stadsmodellen voor het opslaan (E26-14). Laden werkt in een verse scène: gegroeide
    /// gebouwen, haltes, lijnen, diensten, winningen en trekpleisters worden erbij gezet; objecten die met de werkbalk zijn geplaatst
    /// (diensten, haltes, bedrijven) komen alleen als model terug, niet als zichtbaar gebouw.
    /// </summary>
    public sealed class CityStateHostBehaviour : MonoBehaviour
    {
        [SerializeField] private BuildingSpawnerBehaviour _spawner;
        [SerializeField] private EconomyHostBehaviour _economy;
        [SerializeField] private PopulationHostBehaviour _population;
        [SerializeField] private TransitHostBehaviour _transit;
        [SerializeField] private ServicesHostBehaviour _services;
        [SerializeField] private IndustryHostBehaviour _industry;

        public CityState Capture()
        {
            var state = new CityState
            {
                Balance = _economy != null ? _economy.Economy.Ledger.Balance : 0,
                Population = _population != null ? _population.Population.Population : 0,
            };

            if (_spawner != null)
            {
                state.Buildings.AddRange(_spawner.Built);
            }

            if (_transit != null)
            {
                var network = _transit.Transit.Network;
                var indexOf = new Dictionary<int, int>();
                foreach (var stop in network.Stops)
                {
                    indexOf[stop.Key] = state.TransitStops.Count;
                    state.TransitStops.Add(stop.Value);
                }

                foreach (var line in network.Lines)
                {
                    state.Lines.Add(new SavedLine(line.Name, line.Stops.Select(id => indexOf[id]).ToArray(), line.IsLoop, line.Vehicles, line.Headway));
                }
            }

            if (_services != null)
            {
                foreach (var (kind, position) in _services.Services.Placed)
                {
                    state.Services.Add(new SavedService(kind, position));
                }
            }

            if (_industry != null)
            {
                foreach (var site in _industry.Industry.Sites.Sites)
                {
                    state.Sites.Add(new SavedSite(site.Kind, site.Level, site.Richness));
                }

                foreach (var a in _industry.Industry.Tourism.Attractions)
                {
                    state.Attractions.Add(new SavedAttraction(a.Name, a.TouristsPerDay, a.TicketPrice, a.UpkeepPerWeek));
                }

                state.Beds = _industry.Industry.Beds;
            }

            return state;
        }

        public void Apply(CityState state)
        {
            if (_spawner != null)
            {
                foreach (var building in state.Buildings)
                {
                    _spawner.Restore(building);
                }
            }

            if (_population != null)
            {
                _population.Population.Restore(state.Population);
            }

            if (_transit != null)
            {
                var ids = new List<int>();
                foreach (var stop in state.TransitStops)
                {
                    ids.Add(_transit.RestoreStop(stop));
                }

                foreach (var line in state.Lines)
                {
                    _transit.Transit.Network.AddLine(line.Name, line.StopIndices.Select(i => ids[i]).ToList(), line.IsLoop, line.Vehicles, line.Headway);
                }
            }

            if (_services != null)
            {
                foreach (var service in state.Services)
                {
                    _services.Restore(service.Kind, service.Position);
                }
            }

            if (_industry != null)
            {
                foreach (var site in state.Sites)
                {
                    _industry.Industry.AddSite(site.Kind, site.Level, site.Richness);
                }

                foreach (var a in state.Attractions)
                {
                    _industry.Industry.AddAttraction(a.Name, a.TouristsPerDay, a.TicketPrice, a.UpkeepPerWeek);
                }

                _industry.Industry.RestoreBeds(state.Beds);
            }

            // Het saldo als laatste: het terugzetten van diensten en gebouwen boekt zelf niets, maar zo staat het zeker op de opgeslagen waarde.
            if (_economy != null)
            {
                _economy.Economy.Ledger.RestoreBalance(state.Balance);
            }
        }
    }
}
