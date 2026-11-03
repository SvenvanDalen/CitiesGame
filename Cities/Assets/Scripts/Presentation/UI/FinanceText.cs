using System.Globalization;
using Cities.Simulation.Economy;
using Cities.Simulation.Services;

namespace Cities.Presentation.UI
{
    /// <summary>Nederlandse teksten van de budget- en beleidspanelen (E18-06).</summary>
    public static class FinanceText
    {
        public const string BudgetTitle = "Budget";
        public const string PolicyTitle = "Beleid";
        public const string TaxSection = "Belastingen";
        public const string ServiceSection = "Diensten";
        public const string LoanSection = "Lening";
        public const string ForecastSection = "Weekprognose";
        public const string Income = "Inkomsten";
        public const string Expenses = "Uitgaven";
        public const string Net = "Saldo per week";
        public const string Debt = "Schuld";
        public const string Borrow = "Lenen";
        public const string Repay = "Aflossen";
        public const string CityPolicies = "Stad";
        public const string DistrictPolicies = "District";
        public const string NotLosingMoney = "De stad loopt niet leeg.";

        public static string Percent(float fraction) =>
            (fraction * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";

        public static string Zone(TaxZone zone)
        {
            switch (zone)
            {
                case TaxZone.Residential: return "Wonen";
                case TaxZone.Commercial: return "Handel";
                case TaxZone.Industrial: return "Industrie";
                default: return "Kantoor";
            }
        }

        public static string Density(TaxDensity density) => density == TaxDensity.Low ? "lage dichtheid" : "hoge dichtheid";

        public static string Service(ServiceKind kind)
        {
            switch (kind)
            {
                case ServiceKind.Fire: return "Brandweer";
                case ServiceKind.Police: return "Politie";
                case ServiceKind.Health: return "Gezondheidszorg";
                default: return "Begraafplaatsen";
            }
        }

        public static string WeeksOfMoney(int? weeks) => weeks.HasValue ? $"Geld voor {weeks.Value} weken" : NotLosingMoney;

        public static string PerMonth(long amount) => HudText.Money(amount) + " per maand";

        public static string Locked(int population) => "Vanaf " + HudText.Population(population) + " inwoners";

        public static string Result(PolicyToggleResult result, string name)
        {
            switch (result)
            {
                case PolicyToggleResult.Activated: return name + " is ingevoerd.";
                case PolicyToggleResult.Deactivated: return name + " is ingetrokken.";
                case PolicyToggleResult.Locked: return name + " is nog niet beschikbaar.";
                case PolicyToggleResult.CannotAfford: return "Niet genoeg geld voor " + name + ".";
                default: return "Onbekend beleid.";
            }
        }
    }
}
