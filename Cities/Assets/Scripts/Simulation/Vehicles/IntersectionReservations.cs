using System;
using System.Collections.Generic;
using System.Linq;

namespace Cities.Simulation.Vehicles
{
    /// <summary>
    /// Reserveringen van kruispuntdelen (E08-03). Een voertuig reserveert alle delen van zijn route door het kruispunt in één keer of niets:
    /// wie niet alles kan krijgen houdt niets vast en wacht voor het kruispunt (<see cref="LaneTraffic.Step"/> met een gesloten einde). Daarmee
    /// bestaat "vasthouden en wachten" niet, en dus ook geen deadlock tussen voertuigen die elkaars deel nodig hebben.
    /// </summary>
    public sealed class IntersectionReservations
    {
        private readonly Dictionary<int, int> _owners = new Dictionary<int, int>();

        public int Count => _owners.Count;

        public bool IsReserved(int part) => _owners.ContainsKey(part);

        /// <summary>Reserveert alle <paramref name="parts"/> voor <paramref name="vehicle"/> of geen enkele; delen die het voertuig al heeft tellen mee.</summary>
        public bool TryReserve(int vehicle, IReadOnlyCollection<int> parts)
        {
            if (parts is null)
            {
                throw new ArgumentNullException(nameof(parts));
            }

            if (parts.Any(p => _owners.TryGetValue(p, out var owner) && owner != vehicle))
            {
                return false;
            }

            foreach (var part in parts)
            {
                _owners[part] = vehicle;
            }

            return true;
        }

        /// <summary>Geeft alles vrij wat <paramref name="vehicle"/> vasthoudt, bijvoorbeeld als het het kruispunt uit is.</summary>
        public void Release(int vehicle)
        {
            foreach (var part in _owners.Where(p => p.Value == vehicle).Select(p => p.Key).ToList())
            {
                _owners.Remove(part);
            }
        }
    }
}
