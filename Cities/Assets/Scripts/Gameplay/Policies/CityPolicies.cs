using System;
using System.Collections.Generic;
using Cities.Simulation.Economy;
using Cities.Simulation.Policies;
using Cities.World.Districts;
using Cities.World.Grid;

namespace Cities.Gameplay.Policies
{
    /// <summary>
    /// Beleid en districten van de stad (E26-10): het beleidsboek, de districtenkaart, de inwoners per district en de wekelijkse
    /// kosten van het ingevoerde beleid.
    /// </summary>
    public sealed class CityPolicies
    {
        /// <summary>Een maand telt in het spel 30 dagen; de kosten worden per week verrekend.</summary>
        public const int DaysPerMonth = 30;

        public CityPolicies(IReadOnlyDictionary<string, PolicySpec> specs, WorldDimensions dimensions)
        {
            Specs = specs ?? throw new ArgumentNullException(nameof(specs));
            Book = new PolicyBook(specs);
            Districts = new DistrictMap(dimensions);
        }

        public IReadOnlyDictionary<string, PolicySpec> Specs { get; }

        public PolicyBook Book { get; }

        public DistrictMap Districts { get; }

        /// <summary>
        /// Inwoners van een gebied: de hele stad, of het aandeel van een district naar de verhouding van zijn oppervlak tot alle
        /// geschilderde districten.
        /// </summary>
        public int PopulationIn(int district, int cityPopulation)
        {
            if (district == PolicyBook.CityWide)
            {
                return cityPopulation;
            }

            var own = Districts.BlockCount(district);
            if (own == 0)
            {
                return 0;
            }

            var painted = 0;
            foreach (var info in Districts.Book.Districts)
            {
                painted += Districts.BlockCount(info.Id);
            }

            return painted == 0 ? 0 : (int)((long)cityPopulation * own / painted);
        }

        /// <summary>Boekt de kosten van een week beleid; geeft het bedrag terug.</summary>
        public long ChargeWeek(Ledger ledger, int week, int cityPopulation)
        {
            if (ledger is null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            var monthly = Book.MonthlyCost(district => PopulationIn(district, cityPopulation));
            var weekly = monthly * Ledger.DaysPerWeek / DaysPerMonth;
            if (weekly > 0)
            {
                ledger.Force(week, LedgerCategory.Other, weekly);
            }

            return weekly;
        }
    }
}
