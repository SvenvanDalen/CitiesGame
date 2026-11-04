using System;
using System.Collections.Generic;

namespace Cities.Simulation.Milestones
{
    /// <summary>
    /// De data achter het ontgrendelsysteem (E16-02): per definitie-ID de mijlpaal die hem vrijgeeft. Een definitie
    /// zonder regel is vanaf het begin beschikbaar. Valideert dat elke mijlpaal bestaat en dat een ID maar één regel heeft.
    /// </summary>
    public sealed class UnlockTable
    {
        private readonly Dictionary<string, string> _byDefinition = new Dictionary<string, string>();
        private readonly List<UnlockRule> _rules = new List<UnlockRule>();

        public UnlockTable(IEnumerable<UnlockRule> rules, IReadOnlyList<PopulationMilestone> milestones)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            if (milestones == null)
            {
                throw new ArgumentNullException(nameof(milestones));
            }

            var known = new HashSet<string>();
            foreach (var m in milestones)
            {
                known.Add(m.Id);
            }

            foreach (var rule in rules)
            {
                if (!known.Contains(rule.MilestoneId))
                {
                    throw new ArgumentException($"Onbekende mijlpaal '{rule.MilestoneId}' voor '{rule.DefinitionId}'", nameof(rules));
                }

                if (_byDefinition.ContainsKey(rule.DefinitionId))
                {
                    throw new ArgumentException($"Dubbele regel voor '{rule.DefinitionId}'", nameof(rules));
                }

                _byDefinition.Add(rule.DefinitionId, rule.MilestoneId);
                _rules.Add(rule);
            }
        }

        /// <summary>Alle regels in de volgorde waarin ze zijn opgegeven.</summary>
        public IReadOnlyList<UnlockRule> Rules => _rules;

        /// <summary>De mijlpaal die <paramref name="definitionId"/> vrijgeeft, of <c>null</c> als hij altijd beschikbaar is.</summary>
        public string MilestoneFor(string definitionId)
        {
            return definitionId != null && _byDefinition.TryGetValue(definitionId, out var milestone) ? milestone : null;
        }

        /// <summary>De definities die <paramref name="milestoneId"/> vrijgeeft, in regelvolgorde.</summary>
        public IReadOnlyList<string> DefinitionsFor(string milestoneId)
        {
            var result = new List<string>();
            foreach (var rule in _rules)
            {
                if (rule.MilestoneId == milestoneId)
                {
                    result.Add(rule.DefinitionId);
                }
            }

            return result;
        }
    }
}
