using System.Collections.Generic;
using Unity.Mathematics;

namespace Cities.Simulation.Citizens
{
    /// <summary>
    /// Detailniveau van burgers (E07-07). Voetgangers binnen <see cref="NearRadius"/> van het brandpunt (de camera) worden agents die
    /// getekend worden; een agent blijft tot hij voorbij <see cref="FarRadius"/> is (hysterese, zodat er niets flikkert aan de rand). Alle
    /// anderen blijven statistisch: ze bestaan alleen als getal. Een agent volgt dezelfde <see cref="CitizenTrajectory"/>, dus wisselen geeft
    /// geen sprong. Het geselecteerde zit niet in de save: het volgt elke verversing uit de plaatsen en het brandpunt.
    /// </summary>
    public sealed class AgentLod
    {
        private readonly List<int> _detailed = new List<int>();
        private readonly List<KeyValuePair<float, int>> _candidates = new List<KeyValuePair<float, int>>();
        private readonly HashSet<int> _previous = new HashSet<int>();

        public AgentLod(float nearRadius, float farRadius, int maxAgents)
        {
            NearRadius = nearRadius;
            FarRadius = math.max(nearRadius, farRadius);
            MaxAgents = math.max(0, maxAgents);
        }

        public float NearRadius { get; }

        public float FarRadius { get; }

        public int MaxAgents { get; }

        /// <summary>Burgers (index in de bevolking) die nu als agent getekend worden, oplopend gesorteerd.</summary>
        public IReadOnlyList<int> Detailed => _detailed;

        /// <summary>Aantal voetgangers dat niet als agent getekend wordt.</summary>
        public int StatisticalWalkers { get; private set; }

        public void Refresh(float2 focus, IReadOnlyList<Placement> placements)
        {
            _previous.Clear();
            _previous.UnionWith(_detailed);
            _candidates.Clear();
            var walkers = 0;
            for (var i = 0; i < placements.Count; i++)
            {
                var p = placements[i];
                if (!p.Valid || !p.Walking)
                {
                    continue;
                }

                walkers++;
                var distance = math.distance(focus, p.Position);
                if (distance <= NearRadius || (distance <= FarRadius && _previous.Contains(i)))
                {
                    _candidates.Add(new KeyValuePair<float, int>(distance, i));
                }
            }

            // Dichtstbij eerst, bij gelijke afstand de laagste index; bij een tekort vallen de verste af.
            _candidates.Sort((a, b) => a.Key.CompareTo(b.Key) != 0 ? a.Key.CompareTo(b.Key) : a.Value.CompareTo(b.Value));
            var take = math.min(MaxAgents, _candidates.Count);
            _detailed.Clear();
            for (var i = 0; i < take; i++)
            {
                _detailed.Add(_candidates[i].Value);
            }

            _detailed.Sort();
            StatisticalWalkers = walkers - take;
        }
    }
}
