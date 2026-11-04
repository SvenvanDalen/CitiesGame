using System;

namespace Cities.Simulation.Milestones
{
    /// <summary>Een definitie (gebouw, tool, beleid, voertuig) die pas beschikbaar komt na een mijlpaal (E16-02).</summary>
    public readonly struct UnlockRule
    {
        public UnlockRule(string definitionId, string milestoneId)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                throw new ArgumentException("Definitie-ID ontbreekt", nameof(definitionId));
            }

            if (string.IsNullOrWhiteSpace(milestoneId))
            {
                throw new ArgumentException("Mijlpaal-ID ontbreekt", nameof(milestoneId));
            }

            DefinitionId = definitionId;
            MilestoneId = milestoneId;
        }

        public string DefinitionId { get; }

        public string MilestoneId { get; }
    }
}
