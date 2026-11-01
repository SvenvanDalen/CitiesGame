using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;

namespace Cities.Simulation.Services
{
    /// <summary>Een melding (brand, misdrijf, zieke, overledene) die wacht op of wordt afgehandeld door een voertuig.</summary>
    public sealed class Incident
    {
        internal Incident(int id, ServiceKind kind, float2 position)
        {
            Id = id;
            Kind = kind;
            Position = position;
        }

        public int Id { get; }

        public ServiceKind Kind { get; }

        public float2 Position { get; }

        /// <summary>Het dienstgebouw waarvan een voertuig is uitgerukt, of null zolang de melding wacht.</summary>
        public ServiceStation Handler { get; internal set; }

        internal float Remaining { get; set; }
    }

    /// <summary>
    /// Dekking en uitruk (E10-01) voor alle diensten: een punt is gedekt als een gebouw van die soort het in bereik heeft. Een melding krijgt
    /// het dichtstbijzijnde gebouw dat het punt dekt en een vrij voertuig heeft; zijn er geen, dan wacht ze in volgorde van binnenkomst.
    /// Het voertuig is bezet voor heen- en terugrit plus <see cref="HandlingSeconds"/>; daarna is de melding afgehandeld.
    /// </summary>
    public sealed class ServiceDispatcher
    {
        /// <summary>Tijd ter plaatse om de melding af te handelen.</summary>
        public const float HandlingSeconds = 120f;

        private readonly List<ServiceStation> _stations = new List<ServiceStation>();
        private readonly List<Incident> _open = new List<Incident>();
        private int _nextId = 1;

        public ServiceDispatcher(float speedMetersPerSecond)
        {
            if (!(speedMetersPerSecond > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(speedMetersPerSecond), speedMetersPerSecond, "De uitrukkende voertuigen hebben een snelheid nodig.");
            }

            Speed = speedMetersPerSecond;
        }

        public float Speed { get; }

        public IReadOnlyList<Incident> Open => _open;

        /// <summary>Totale maandelijkse onderhoudskosten van alle dienstgebouwen.</summary>
        public long UpkeepPerMonth => _stations.Sum(s => s.UpkeepPerMonth);

        public void Add(ServiceStation station) => _stations.Add(station ?? throw new ArgumentNullException(nameof(station)));

        /// <summary>Sloopt een gebouw; meldingen waar zijn voertuig naartoe reed gaan weer in de wachtrij.</summary>
        public bool Remove(ServiceStation station)
        {
            foreach (var incident in _open.Where(i => i.Handler == station))
            {
                incident.Handler = null;
            }

            return _stations.Remove(station);
        }

        public bool IsCovered(ServiceKind kind, float2 point) => _stations.Any(s => s.Kind == kind && s.Covers(point));

        /// <summary>Deel van <paramref name="points"/> dat gedekt is, voor de dekkingsstatistiek; 1 bij een lege lijst.</summary>
        public float Coverage(ServiceKind kind, IReadOnlyCollection<float2> points) =>
            points.Count == 0 ? 1f : points.Count(p => IsCovered(kind, p)) / (float)points.Count;

        public Incident Report(ServiceKind kind, float2 position)
        {
            var incident = new Incident(_nextId++, kind, position);
            _open.Add(incident);
            Assign();
            return incident;
        }

        /// <summary>Trekt een melding in (bijvoorbeeld een afgebrand gebouw); een onderweg zijnd voertuig is meteen weer vrij.</summary>
        public bool Cancel(Incident incident)
        {
            if (!_open.Remove(incident))
            {
                return false;
            }

            if (incident.Handler != null)
            {
                incident.Handler.FreeVehicles++;
                incident.Handler = null;
            }

            Assign();
            return true;
        }

        /// <summary>Laat <paramref name="seconds"/> verstrijken; geeft de meldingen die nu zijn afgehandeld.</summary>
        public IReadOnlyList<Incident> Tick(float seconds)
        {
            var done = new List<Incident>();
            foreach (var incident in _open.Where(i => i.Handler != null))
            {
                incident.Remaining -= seconds;
                if (incident.Remaining <= 0f)
                {
                    incident.Handler.FreeVehicles++;
                    done.Add(incident);
                }
            }

            _open.RemoveAll(done.Contains);
            Assign();
            return done;
        }

        // shortcut: hemelsbrede afstand en vaste snelheid, niet de route over het wegennet; upgraden bij E08 (pathfinding).
        private void Assign()
        {
            foreach (var incident in _open.Where(i => i.Handler == null))
            {
                var position = incident.Position;
                var station = _stations
                    .Where(s => s.Kind == incident.Kind && s.FreeVehicles > 0 && s.Covers(position))
                    .OrderBy(s => math.lengthsq(s.Position - position))
                    .FirstOrDefault();
                if (station == null)
                {
                    continue;
                }

                station.FreeVehicles--;
                incident.Handler = station;
                incident.Remaining = (2f * math.distance(station.Position, position) / Speed) + HandlingSeconds;
            }
        }
    }
}
