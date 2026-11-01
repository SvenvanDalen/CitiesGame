using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Randomness;
using Unity.Mathematics;

namespace Cities.Simulation.Disasters
{
    /// <summary>
    /// Het gedeelde rampenframework (E15-01). Rampentypen worden geregistreerd als <see cref="DisasterSpec"/>; een ramp ontstaat vanzelf
    /// (kans per dag, deterministisch uit de meegegeven random) of handmatig via <see cref="Trigger"/>. Elke dag schuift <see cref="Step"/>
    /// alle rampen een fase op en geeft de kosten terug. Rampen zijn los van de kern uit te zetten, in zijn geheel of per type; een
    /// handmatige trigger werkt ook als het willekeurig optreden uit staat. Effecten hangen aan <see cref="PhaseChanged"/>.
    /// </summary>
    public sealed class DisasterManager
    {
        private readonly List<DisasterSpec> _specs = new List<DisasterSpec>();
        private readonly HashSet<string> _disabled = new HashSet<string>();
        private readonly List<Disaster> _running = new List<Disaster>();

        /// <summary>Of rampen vanzelf mogen optreden.</summary>
        public bool RandomEnabled { get; set; } = true;

        public IReadOnlyList<DisasterSpec> Specs => _specs;

        public IReadOnlyList<Disaster> Running => _running;

        /// <summary>Een ramp kwam in een nieuwe fase (waarschuwing, effect, nasleep of voorbij); ook bij het ontstaan.</summary>
        public event Action<Disaster> PhaseChanged;

        public void Register(DisasterSpec spec)
        {
            if (spec is null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (_specs.Any(s => s.Name == spec.Name))
            {
                throw new ArgumentException($"Ramp '{spec.Name}' is al geregistreerd.", nameof(spec));
            }

            _specs.Add(spec);
        }

        public bool IsEnabled(string name) => !_disabled.Contains(name);

        /// <summary>Zet het willekeurig optreden van één type aan of uit.</summary>
        public void SetEnabled(string name, bool enabled)
        {
            if (enabled)
            {
                _disabled.Remove(name);
            }
            else
            {
                _disabled.Add(name);
            }
        }

        /// <summary>Laat een ramp van het gegeven type nu beginnen.</summary>
        public Disaster Trigger(string name, float2 position)
        {
            var spec = _specs.FirstOrDefault(s => s.Name == name) ?? throw new ArgumentException($"Onbekende ramp '{name}'.", nameof(name));
            var disaster = new Disaster(spec, position);
            _running.Add(disaster);
            PhaseChanged?.Invoke(disaster);
            return disaster;
        }

        /// <summary>
        /// Eén dag: rekent de kosten van de lopende rampen af, schuift ze op en laat daarna nieuwe vanzelf ontstaan op een willekeurige
        /// plek in een gebied van <paramref name="area"/> meter. Geeft de totale kosten van vandaag.
        /// </summary>
        public long Step(ref DeterministicRandom random, float2 area)
        {
            var cost = 0L;
            foreach (var disaster in _running.ToArray())
            {
                cost += disaster.CostToday;
                if (disaster.Advance())
                {
                    PhaseChanged?.Invoke(disaster);
                }
            }

            _running.RemoveAll(d => d.Phase == DisasterPhase.Ended);
            if (RandomEnabled)
            {
                foreach (var spec in _specs.Where(s => IsEnabled(s.Name) && _running.All(d => d.Spec != s)).ToArray())
                {
                    if (random.NextFloat() < spec.ChancePerDay)
                    {
                        Trigger(spec.Name, new float2(random.NextFloat(0f, area.x), random.NextFloat(0f, area.y)));
                    }
                }
            }

            return cost;
        }
    }
}
