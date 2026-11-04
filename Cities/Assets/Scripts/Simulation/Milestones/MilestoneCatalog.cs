using System.Collections.Generic;

namespace Cities.Simulation.Milestones
{
    /// <summary>De twaalf bevolkingsmijlpalen, oplopend op drempel (E16-01).</summary>
    public static class MilestoneCatalog
    {
        public static readonly IReadOnlyList<PopulationMilestone> All = new[]
        {
            new PopulationMilestone("milestone.hamlet", "Gehucht", 100, 5000, "Eenvoudige voorzieningen komen beschikbaar."),
            new PopulationMilestone("milestone.village", "Dorp", 500, 15000, "Basisonderwijs en brandweer worden vrijgegeven."),
            new PopulationMilestone("milestone.large-village", "Groot dorp", 1000, 30000, "Politie en gezondheidszorg worden vrijgegeven."),
            new PopulationMilestone("milestone.small-town", "Kleine stad", 2500, 60000, "Busnetwerk en beleid worden vrijgegeven."),
            new PopulationMilestone("milestone.town", "Stad", 5000, 100000, "Middelbaar onderwijs en parken worden vrijgegeven."),
            new PopulationMilestone("milestone.large-town", "Grote stad", 10000, 200000, "Tram en gespecialiseerde industrie worden vrijgegeven."),
            new PopulationMilestone("milestone.city", "Stad van betekenis", 25000, 400000, "Metro en hoger onderwijs worden vrijgegeven."),
            new PopulationMilestone("milestone.regional-city", "Regionale stad", 50000, 750000, "Buitenverbindingen en toerisme worden uitgebreid."),
            new PopulationMilestone("milestone.large-city", "Grote regionale stad", 100000, 1500000, "Luchthaven en universiteit worden vrijgegeven."),
            new PopulationMilestone("milestone.metropolis", "Metropool", 250000, 3000000, "Monumenten worden vrijgegeven."),
            new PopulationMilestone("milestone.large-metropolis", "Grote metropool", 500000, 6000000, "Alle gebouwen zijn beschikbaar."),
            new PopulationMilestone("milestone.megacity", "Megastad", 1000000, 12000000, "Je stad is een wereldstad."),
        };

        public static PopulationMilestone Find(string id)
        {
            foreach (var m in All)
            {
                if (m.Id == id)
                {
                    return m;
                }
            }

            return null;
        }
    }
}
