using System;
using System.Collections.Generic;

namespace Cities.Simulation.Milestones
{
    /// <summary>
    /// Beantwoordt of een definitie (gebouw, tool, beleid, voertuig) beschikbaar is (E16-02). De bron is de
    /// <see cref="UnlockTable"/> met de gehaalde mijlpalen uit de <see cref="MilestoneTracker"/>; alleen de sandbox-schakelaar
    /// (<see cref="AllUnlocked"/>, E22-04) is eigen status en gaat in sectie <c>UNLK</c>.
    /// </summary>
    public sealed class UnlockService
    {
        private readonly MilestoneTracker _tracker;
        private readonly UnlockTable _table;
        private readonly Dictionary<string, PopulationMilestone> _milestones = new Dictionary<string, PopulationMilestone>();

        public UnlockService(MilestoneTracker tracker, UnlockTable table)
        {
            _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
            _table = table ?? throw new ArgumentNullException(nameof(table));
            foreach (var m in tracker.Catalog)
            {
                _milestones[m.Id] = m;
            }
        }

        public UnlockService(MilestoneTracker tracker)
            : this(tracker, UnlockCatalog.CreateDefault())
        {
        }

        public UnlockTable Table => _table;

        /// <summary>Als <c>true</c> staat alles open (sandbox of cheat).</summary>
        public bool AllUnlocked { get; private set; }

        /// <summary>Wordt aangeroepen met elke definitie die net beschikbaar is gekomen.</summary>
        public event Action<string> Unlocked;

        /// <summary>Is de definitie beschikbaar? Zonder regel altijd; anders zodra de mijlpaal is gehaald.</summary>
        public bool IsUnlocked(string definitionId)
        {
            var milestone = _table.MilestoneFor(definitionId);
            return milestone == null || AllUnlocked || _tracker.IsAchieved(milestone);
        }

        /// <summary>Waarom de definitie nog dicht zit (voor de werkbalk), of <c>null</c> als hij beschikbaar is.</summary>
        public string LockedReason(string definitionId)
        {
            var milestone = RequiredMilestone(definitionId);
            return milestone == null ? null : $"Beschikbaar vanaf {milestone.Title} ({milestone.Population:N0} inwoners)";
        }

        /// <summary>De mijlpaal die de definitie nog vrijgeeft, of <c>null</c> als hij al open is.</summary>
        public PopulationMilestone RequiredMilestone(string definitionId)
        {
            if (IsUnlocked(definitionId))
            {
                return null;
            }

            _milestones.TryGetValue(_table.MilestoneFor(definitionId), out var milestone);
            return milestone;
        }

        /// <summary>Alle definities met een regel die nu beschikbaar zijn, in regelvolgorde.</summary>
        public IReadOnlyList<string> UnlockedDefinitions()
        {
            var result = new List<string>();
            foreach (var rule in _table.Rules)
            {
                if (IsUnlocked(rule.DefinitionId))
                {
                    result.Add(rule.DefinitionId);
                }
            }

            return result;
        }

        /// <summary>Verwerkt nieuw gehaalde mijlpalen (uitkomst van <see cref="MilestoneTracker.Update"/>) en meldt de vrijgekomen definities.</summary>
        public IReadOnlyList<string> OnMilestonesAchieved(IEnumerable<MilestoneAchievement> fresh)
        {
            var released = new List<string>();
            if (fresh == null || AllUnlocked)
            {
                return released;
            }

            foreach (var a in fresh)
            {
                released.AddRange(_table.DefinitionsFor(a.Milestone.Id));
            }

            Notify(released);
            return released;
        }

        /// <summary>Zet alles open of weer onder de mijlpalen; geeft bij openzetten de definities die daardoor vrijkomen.</summary>
        public IReadOnlyList<string> SetAllUnlocked(bool value)
        {
            var released = new List<string>();
            if (value && !AllUnlocked)
            {
                foreach (var rule in _table.Rules)
                {
                    if (!_tracker.IsAchieved(rule.MilestoneId))
                    {
                        released.Add(rule.DefinitionId);
                    }
                }
            }

            AllUnlocked = value;
            Notify(released);
            return released;
        }

        internal void Restore(bool allUnlocked) => AllUnlocked = allUnlocked;

        private void Notify(List<string> released)
        {
            foreach (var id in released)
            {
                Unlocked?.Invoke(id);
            }
        }
    }
}
