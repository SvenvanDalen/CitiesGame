using System;

namespace Cities.Simulation.Demand
{
    /// <summary>Wat de stad nu telt; de bewoners-, gebouw- en economie-systemen vullen dit per rekenronde in.</summary>
    public readonly struct DemandInputs
    {
        public DemandInputs(int homes, int vacantHomes, int unemployed, int openJobs, float potentialSpending, float shopCapacity)
        {
            Homes = homes;
            VacantHomes = vacantHomes;
            Unemployed = unemployed;
            OpenJobs = openJobs;
            PotentialSpending = potentialSpending;
            ShopCapacity = shopCapacity;
        }

        /// <summary>Alle woningen, bewoond of niet.</summary>
        public int Homes { get; }

        /// <summary>Woningen zonder bewoners.</summary>
        public int VacantHomes { get; }

        /// <summary>Inwoners die kunnen en willen werken maar geen baan hebben.</summary>
        public int Unemployed { get; }

        /// <summary>Banen waar nog niemand op zit.</summary>
        public int OpenJobs { get; }

        /// <summary>Wat de inwoners per dag aan winkels willen uitgeven (inkomen maal bestedingsaandeel).</summary>
        public float PotentialSpending { get; }

        /// <summary>Wat de winkels per dag kunnen verkopen.</summary>
        public float ShopCapacity { get; }
    }

    /// <summary>Vraagmeters van -1 (te veel aanbod) tot 1 (grote vraag).</summary>
    public readonly struct Demand
    {
        public Demand(float residential, float work, float commerce)
        {
            Residential = residential;
            Work = work;
            Commerce = commerce;
        }

        public float Residential { get; }

        public float Work { get; }

        public float Commerce { get; }
    }

    /// <summary>
    /// Het vraagmodel (RCI) van E05-03: hoeveel de stad wil bijbouwen aan wonen, werk en winkels, uit leegstand, werkloosheid
    /// en openstaande banen, en uit wat inwoners willen uitgeven tegenover wat winkels kunnen verkopen. De zonering en de
    /// gebouwspawner (E05-04) lezen de meters; ze lopen met <see cref="Approach"/> zachtjes mee, zodat de stad niet knippert.
    /// </summary>
    public static class DemandModel
    {
        /// <summary>Deel van de woningen dat leeg hoort te staan zodat mensen kunnen verhuizen.</summary>
        public const float TargetVacancy = 0.05f;

        private const float VacancyWeight = 0.6f;

        /// <summary>Een lege stad vraagt alleen om woningen; werk en winkels volgen pas als er bewoners zijn.</summary>
        public static Demand Compute(in DemandInputs city)
        {
            if (city.Homes < 0 || city.VacantHomes < 0 || city.Unemployed < 0 || city.OpenJobs < 0 || city.PotentialSpending < 0f || city.ShopCapacity < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(city), "Aantallen kunnen niet negatief zijn.");
            }

            if (city.VacantHomes > city.Homes)
            {
                throw new ArgumentException("Er kunnen niet meer lege woningen zijn dan woningen.", nameof(city));
            }

            var housing = city.Homes == 0 ? 1f : Pressure(city.VacantHomes, TargetVacancy * city.Homes);
            var jobsPull = Pressure(city.Unemployed, city.OpenJobs);
            var residential = (VacancyWeight * housing) + ((1f - VacancyWeight) * jobsPull);
            return new Demand(residential, Pressure(city.OpenJobs, city.Unemployed), Pressure(city.ShopCapacity, city.PotentialSpending));
        }

        /// <summary>Beweegt <paramref name="current"/> hoogstens <paramref name="rate"/> per seconde naar <paramref name="target"/>.</summary>
        public static Demand Approach(in Demand current, in Demand target, float rate, float deltaSeconds)
        {
            var step = Math.Max(0f, rate * deltaSeconds);
            return new Demand(
                Toward(current.Residential, target.Residential, step),
                Toward(current.Work, target.Work, step),
                Toward(current.Commerce, target.Commerce, step));
        }

        /// <summary>Verschil tussen vraag en aanbod als deel van het grootste van de twee, begrensd op -1 tot 1; 0 als beide 0 zijn.</summary>
        private static float Pressure(float supply, float demand)
        {
            var scale = Math.Max(supply, demand);
            return scale <= 0f ? 0f : Math.Max(-1f, Math.Min(1f, (demand - supply) / scale));
        }

        private static float Toward(float value, float target, float step) =>
            Math.Abs(target - value) <= step ? target : value + (Math.Sign(target - value) * step);
    }
}
