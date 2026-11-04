using System;
using System.Collections.Generic;

namespace Cities.Simulation.Milestones
{
    /// <summary>
    /// Houdt bij welke bevolkingsmijlpalen zijn gehaald (E16-01). Een mijlpaal blijft gehaald als de
    /// bevolking later krimpt; de beloning wordt dus maar één keer uitgekeerd.
    /// </summary>
    public sealed class MilestoneTracker
    {
        private readonly IReadOnlyList<PopulationMilestone> _catalog;
        private readonly List<MilestoneAchievement> _achieved = new List<MilestoneAchievement>();
        private readonly HashSet<string> _achievedIds = new HashSet<string>();

        public MilestoneTracker()
            : this(MilestoneCatalog.All)
        {
        }

        public MilestoneTracker(IReadOnlyList<PopulationMilestone> catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public IReadOnlyList<PopulationMilestone> Catalog => _catalog;

        /// <summary>Gehaalde mijlpalen op volgorde van behalen.</summary>
        public IReadOnlyList<MilestoneAchievement> Achieved => _achieved;

        /// <summary>De eerstvolgende mijlpaal, of <c>null</c> als alles gehaald is.</summary>
        public PopulationMilestone Next
        {
            get
            {
                foreach (var m in _catalog)
                {
                    if (!_achievedIds.Contains(m.Id))
                    {
                        return m;
                    }
                }

                return null;
            }
        }

        public bool IsAchieved(string id) => _achievedIds.Contains(id);

        /// <summary>Voortgang (0 tot 1) richting de volgende mijlpaal.</summary>
        public float ProgressToNext(int population)
        {
            var next = Next;
            if (next == null)
            {
                return 1f;
            }

            return Math.Max(0f, Math.Min(1f, population / (float)next.Population));
        }

        /// <summary>Kent alle nieuw gehaalde mijlpalen toe en geeft ze terug; de aanroeper keert de beloning uit.</summary>
        public IReadOnlyList<MilestoneAchievement> Update(int population, long tick)
        {
            var fresh = new List<MilestoneAchievement>();
            foreach (var m in _catalog)
            {
                if (population >= m.Population && !_achievedIds.Contains(m.Id))
                {
                    var a = new MilestoneAchievement(m, tick);
                    _achieved.Add(a);
                    _achievedIds.Add(m.Id);
                    fresh.Add(a);
                }
            }

            return fresh;
        }

        internal void Restore(IEnumerable<MilestoneAchievement> achieved)
        {
            _achieved.Clear();
            _achievedIds.Clear();
            foreach (var a in achieved)
            {
                _achieved.Add(a);
                _achievedIds.Add(a.Milestone.Id);
            }
        }
    }
}
