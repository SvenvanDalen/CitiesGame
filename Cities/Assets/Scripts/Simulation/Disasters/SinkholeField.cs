using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Cities.Simulation.Disasters
{
    /// <summary>De zinkgaten in de stad (E15-07): opent ze, schuift ze elke dag op en bewaart ze voor de save.</summary>
    public sealed class SinkholeField
    {
        public const int MaxHoles = 64;

        private readonly List<Sinkhole> _holes = new List<Sinkhole>();

        public int NextId { get; private set; } = 1;

        public IReadOnlyList<Sinkhole> Holes => _holes;

        /// <summary>Opent een nieuw zinkgat; het is dan nog klein (dag nul).</summary>
        public Sinkhole Open(float2 center, float maxRadius, float growthPerDay)
        {
            if (_holes.Count >= MaxHoles)
            {
                throw new InvalidOperationException("Er zijn al te veel zinkgaten.");
            }

            var hole = new Sinkhole(NextId++, center, maxRadius, growthPerDay);
            _holes.Add(hole);
            return hole;
        }

        /// <summary>Eén dag voor alle gaten; geeft per gat de uitkomst van vandaag.</summary>
        public IReadOnlyList<SinkholeOutcome> Step(IReadOnlyList<SinkholeBuilding> buildings, IReadOnlyList<SinkholeSegment> segments)
        {
            var outcomes = new List<SinkholeOutcome>(_holes.Count);
            foreach (var hole in _holes)
            {
                outcomes.Add(hole.Advance(buildings, segments));
            }

            return outcomes;
        }

        internal void Restore(int nextId, IEnumerable<Sinkhole> holes)
        {
            NextId = nextId;
            _holes.Clear();
            _holes.AddRange(holes);
        }

        internal static Sinkhole Create(int id, float2 center, float maxRadius, float growthPerDay, int ageDays, IEnumerable<int> lostBuildings, IEnumerable<int> lostSegments)
        {
            var hole = new Sinkhole(id, center, maxRadius, growthPerDay);
            hole.Restore(ageDays, lostBuildings, lostSegments);
            return hole;
        }
    }
}
