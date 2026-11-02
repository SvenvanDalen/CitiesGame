using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;

namespace Cities.Simulation.Services
{
    /// <summary>
    /// Overlijden (E10-05), bovenop de dekking en uitruk van <see cref="ServiceDispatcher"/>. Een overledene (zie <see cref="HealthModel.Step"/>)
    /// wordt gemeld bij een lijkwagen van een begraafplaats of crematorium. Is de melding afgehandeld, dan krijgt hij een graf als er een vrij
    /// is, anders wordt hij gecremeerd als de stad een crematorium heeft. Is geen van beide mogelijk, dan blijft hij liggen en wordt hij
    /// opnieuw gemeld. Elke wachtende overledene geeft ongenoegen (<see cref="Unhappiness"/>).
    /// </summary>
    public sealed class DeathCare
    {
        /// <summary>Ongenoegen (0 tot 1) per overledene die nog niet is afgehandeld.</summary>
        public const float UnhappinessPerBody = 0.02f;

        private readonly ServiceDispatcher _dispatcher;
        private readonly Dictionary<int, (float2 Position, Incident Incident)> _waiting = new Dictionary<int, (float2, Incident)>();
        private int _plots;
        private int _crematoria;

        public DeathCare(ServiceDispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public int Buried { get; private set; }

        public int Cremated { get; private set; }

        /// <summary>Overledenen die wachten op een lijkwagen of een plek.</summary>
        public int Unburied => _waiting.Count;

        public int FreePlots => _plots;

        public float Unhappiness => math.saturate(Unburied * UnhappinessPerBody);

        public ServiceStation AddCemetery(float2 position, int plots)
        {
            if (plots < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(plots), plots, "Een begraafplaats heeft minstens één graf nodig.");
            }

            _plots += plots;
            return Add(new ServiceStation(ServiceKind.Death, position, 600f, 2, 15000));
        }

        public ServiceStation AddCrematorium(float2 position)
        {
            _crematoria++;
            return Add(new ServiceStation(ServiceKind.Death, position, 800f, 3, 40000));
        }

        /// <summary>Meldt een overledene bij de lijkwagens; een dubbele melding voor dezelfde burger wordt genegeerd.</summary>
        public void Register(int id, float2 position)
        {
            if (!_waiting.ContainsKey(id))
            {
                _waiting[id] = (position, _dispatcher.Report(ServiceKind.Death, position));
            }
        }

        /// <summary>Eén dag: afgehandelde overledenen krijgen een graf of worden gecremeerd; zonder plek gaan ze weer in de wachtrij.</summary>
        public void Step()
        {
            foreach (var done in _waiting.Where(p => !_dispatcher.Open.Contains(p.Value.Incident)).ToArray())
            {
                if (_plots > 0)
                {
                    _plots--;
                    Buried++;
                    _waiting.Remove(done.Key);
                }
                else if (_crematoria > 0)
                {
                    Cremated++;
                    _waiting.Remove(done.Key);
                }
                else
                {
                    _waiting[done.Key] = (done.Value.Position, _dispatcher.Report(ServiceKind.Death, done.Value.Position));
                }
            }
        }

        private ServiceStation Add(ServiceStation station)
        {
            _dispatcher.Add(station);
            return station;
        }
    }
}
