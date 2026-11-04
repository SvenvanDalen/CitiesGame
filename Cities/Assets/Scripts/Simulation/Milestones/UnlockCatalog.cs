using System.Collections.Generic;

namespace Cities.Simulation.Milestones
{
    /// <summary>
    /// De standaardregels: welke gebouwen, tools, wegen, voertuigen en beleid bij welke bevolkingsmijlpaal beschikbaar komen
    /// (E16-02). Volgt de meldingstekst van <see cref="MilestoneCatalog"/>. Wat hier niet staat is vanaf het begin open.
    /// </summary>
    public static class UnlockCatalog
    {
        public static UnlockTable CreateDefault() => new UnlockTable(Rules(), MilestoneCatalog.All);

        private static IEnumerable<UnlockRule> Rules()
        {
            // Gehucht: eenvoudige voorzieningen.
            yield return new UnlockRule("building.commercial-low-shop-house", "milestone.hamlet");
            yield return new UnlockRule("policy.recycling", "milestone.hamlet");
            yield return new UnlockRule("road.footpath", "milestone.hamlet");

            // Dorp: basisonderwijs en brandweer.
            yield return new UnlockRule("tool.fire-station", "milestone.village");
            yield return new UnlockRule("tool.primary-school", "milestone.village");
            yield return new UnlockRule("vehicle.emergency-van", "milestone.village");
            yield return new UnlockRule("policy.school-bonus", "milestone.village");

            // Groot dorp: politie en gezondheidszorg.
            yield return new UnlockRule("tool.police-station", "milestone.large-village");
            yield return new UnlockRule("tool.clinic", "milestone.large-village");
            yield return new UnlockRule("policy.neighbourhood-watch", "milestone.large-village");
            yield return new UnlockRule("policy.free-healthcare", "milestone.large-village");

            // Kleine stad: busnetwerk en beleid.
            yield return new UnlockRule("tool.bus-line", "milestone.small-town");
            yield return new UnlockRule("vehicle.city-bus", "milestone.small-town");
            yield return new UnlockRule("policy.free-transit", "milestone.small-town");
            yield return new UnlockRule("policy.bike-incentive", "milestone.small-town");
            yield return new UnlockRule("policy.parking-fees", "milestone.small-town");
            yield return new UnlockRule("policy.cheap-parking", "milestone.small-town");
            yield return new UnlockRule("policy.small-business-aid", "milestone.small-town");
            yield return new UnlockRule("road.four-lane", "milestone.small-town");

            // Stad: middelbaar onderwijs en parken.
            yield return new UnlockRule("tool.secondary-school", "milestone.town");
            yield return new UnlockRule("tool.park", "milestone.town");
            yield return new UnlockRule("tourist-site.walking-route.park", "milestone.town");
            yield return new UnlockRule("building.office-high-block", "milestone.town");
            yield return new UnlockRule("policy.smoke-free", "milestone.town");
            yield return new UnlockRule("policy.green-energy", "milestone.town");
            yield return new UnlockRule("road.avenue", "milestone.town");

            // Grote stad: tram en gespecialiseerde industrie.
            yield return new UnlockRule("tool.tram-line", "milestone.large-town");
            yield return new UnlockRule("vehicle.city-tram", "milestone.large-town");
            yield return new UnlockRule("building.industrial-high-chemical-plant", "milestone.large-town");
            yield return new UnlockRule("building.industrial-high-factory", "milestone.large-town");
            yield return new UnlockRule("policy.industry-tax", "milestone.large-town");
            yield return new UnlockRule("policy.truck-ban", "milestone.large-town");

            // Stad van betekenis: metro en hoger onderwijs.
            yield return new UnlockRule("tool.metro-line", "milestone.city");
            yield return new UnlockRule("tool.college", "milestone.city");
            yield return new UnlockRule("building.office-high-tower", "milestone.city");
            yield return new UnlockRule("building.residential-high-tower", "milestone.city");
            yield return new UnlockRule("road.highway", "milestone.city");

            // Regionale stad: buitenverbindingen en toerisme.
            yield return new UnlockRule("tool.outside-connection", "milestone.regional-city");
            yield return new UnlockRule("tourist-site.hotel.city", "milestone.regional-city");
            yield return new UnlockRule("tourist-site.beach.public", "milestone.regional-city");
            yield return new UnlockRule("building.commercial-high-shopping-centre", "milestone.regional-city");

            // Grote regionale stad: luchthaven en universiteit.
            yield return new UnlockRule("tool.airport", "milestone.large-city");
            yield return new UnlockRule("tool.university", "milestone.large-city");

            // Metropool: monumenten.
            yield return new UnlockRule("tourist-site.attraction.monument", "milestone.metropolis");
        }
    }
}
