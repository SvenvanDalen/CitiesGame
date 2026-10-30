using System;
using System.Globalization;
using Cities.Presentation.UI;

namespace Cities.Presentation.Debugging
{
    /// <summary>Teksten en drempels van het debugpaneel.</summary>
    public static class DebugPanelText
    {
        public const string Title = "Debug";
        public const string SectionTick = "Tick per systeem";
        public const string SectionCounters = "Tellers";
        public const string SectionOverlays = "Overlays";
        public const string NoSystems = "Nog geen systemen gemeten.";
        public const string MeasureHint = "Meetmodus: de systemen draaien na elkaar zolang dit paneel open is.";

        /// <summary>Boven dit deel van het budget wordt een waarde oranje, boven het hele budget rood.</summary>
        public const double WarningFraction = 0.75;

        /// <summary>Milliseconden met komma, bijvoorbeeld <c>0,42 ms</c>.</summary>
        public static string Milliseconds(double milliseconds)
        {
            var format = milliseconds < 10d ? "0.00" : "0.0";
            return milliseconds.ToString(format, CultureInfo.InvariantCulture).Replace('.', ',') + " ms";
        }

        /// <summary>Gehele getallen met punten als duizendtalscheiding, bijvoorbeeld <c>1.250.000</c>.</summary>
        public static string Count(long value) =>
            value.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.');

        /// <summary>Deel van het budget, tussen 0 en 1 voor de balk.</summary>
        public static float BudgetFraction(double milliseconds, double budgetMs)
        {
            if (budgetMs <= 0d || double.IsNaN(milliseconds))
            {
                return 0f;
            }

            return (float)Math.Min(1d, Math.Max(0d, milliseconds / budgetMs));
        }

        /// <summary>Kleur van een tijd ten opzichte van het budget.</summary>
        public static MeterColor BudgetColor(double milliseconds, double budgetMs)
        {
            if (budgetMs <= 0d || milliseconds <= budgetMs * WarningFraction)
            {
                return MeterColor.Success;
            }

            return milliseconds <= budgetMs ? MeterColor.Warning : MeterColor.Danger;
        }

        public static string TickHeader(long population, double budgetMs) =>
            $"Budget {Milliseconds(budgetMs)} per tick bij {Count(population)} inwoners";
    }
}
