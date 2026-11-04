using System;

namespace Cities.Simulation.Milestones
{
    /// <summary>Een bevolkingsmijlpaal: drempel, geldbeloning en de melding aan de speler (E16-01).</summary>
    public sealed class PopulationMilestone
    {
        public PopulationMilestone(string id, string title, int population, int rewardCash, string unlockText)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("ID ontbreekt", nameof(id));
            }

            if (population < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(population));
            }

            if (rewardCash < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rewardCash));
            }

            Id = id;
            Title = title ?? string.Empty;
            Population = population;
            RewardCash = rewardCash;
            UnlockText = unlockText ?? string.Empty;
        }

        /// <summary>Stabiel ID (<c>milestone.naam</c>), komt in de save.</summary>
        public string Id { get; }

        public string Title { get; }

        /// <summary>Aantal inwoners dat nodig is.</summary>
        public int Population { get; }

        /// <summary>Eenmalige uitkering in euro.</summary>
        public int RewardCash { get; }

        /// <summary>Wat er met de mijlpaal beschikbaar komt, voor de melding.</summary>
        public string UnlockText { get; }

        /// <summary>Meldingstekst voor de speler.</summary>
        public string Message => $"{Title}: {Population:N0} inwoners bereikt. Beloning: € {RewardCash:N0}"
            + (UnlockText.Length > 0 ? $". {UnlockText}" : ".");
    }

    /// <summary>Een mijlpaal die op een bepaald moment is gehaald.</summary>
    public readonly struct MilestoneAchievement
    {
        public MilestoneAchievement(PopulationMilestone milestone, long tick)
        {
            Milestone = milestone;
            Tick = tick;
        }

        public PopulationMilestone Milestone { get; }

        public long Tick { get; }
    }
}
