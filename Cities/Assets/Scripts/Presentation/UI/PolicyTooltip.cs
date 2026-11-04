using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Cities.Simulation.Policies;

namespace Cities.Presentation.UI
{
    /// <summary>
    /// De tooltip van een beleidsmaatregel (E13-07): wat het kost (eenmalig en per maand, omgerekend naar een dag), wat elk effect
    /// verandert en, als een systeem de dagwaarde van een effect kent, wat dat per dag oplevert. De netto dagbijdrage is de opbrengst
    /// min de dagelijkse kosten. Alles volgt uit de <see cref="PolicySpec"/>-data, dus een nieuwe regel kost geen code.
    /// </summary>
    public static class PolicyTooltip
    {
        /// <summary>Dagen in een spelmaand, voor het omrekenen van maandkosten naar een dag.</summary>
        public const int DaysPerMonth = 30;

        /// <summary>Dagelijkse kosten van het beleid voor een gebied met <paramref name="citizens"/> inwoners.</summary>
        public static long DailyCost(PolicySpec spec, int citizens) =>
            (long)Math.Round(spec.MonthlyCostFor(citizens) / (double)DaysPerMonth, MidpointRounding.AwayFromZero);

        /// <summary>De dagwaarde die één effect oplevert: <c>basis × (factor − 1)</c> in euro per dag; <c>null</c> als de basis onbekend is.</summary>
        public static long? DailyGain(PolicyEffect effect, Func<string, long?> dailyBaseline)
        {
            var baseline = dailyBaseline?.Invoke(effect.Key);
            return baseline.HasValue ? (long?)Math.Round(baseline.Value * (effect.Multiplier - 1.0), MidpointRounding.AwayFromZero) : null;
        }

        /// <summary>Netto dagbijdrage: de opbrengst van alle effecten met een bekende dagwaarde min de dagelijkse kosten.</summary>
        public static long NetDaily(PolicySpec spec, int citizens, Func<string, long?> dailyBaseline)
        {
            var net = -DailyCost(spec, citizens);
            foreach (var effect in spec.Effects ?? Array.Empty<PolicyEffect>())
            {
                net += DailyGain(effect, dailyBaseline) ?? 0;
            }

            return net;
        }

        /// <param name="effectNames">Weergavenaam per effectsleutel; ontbreekt die, dan wordt de sleutel getoond.</param>
        /// <param name="dailyBaseline">Euro per dag die een systeem nu verwerkt voor een sleutel (bijvoorbeeld de belastinginkomsten), of <c>null</c>.</param>
        public static string Build(
            string name,
            PolicySpec spec,
            int citizens,
            IReadOnlyDictionary<string, string> effectNames = null,
            Func<string, long?> dailyBaseline = null)
        {
            var text = new StringBuilder();
            text.Append(name).Append('\n');
            text.Append("Invoeren: ").Append(HudText.Money(spec.ActivationCost)).Append('\n');
            text.Append("Kosten: ").Append(FinanceText.PerMonth(spec.MonthlyCostFor(citizens)))
                .Append(" (").Append(HudText.Money(DailyCost(spec, citizens))).Append(" per dag)");

            foreach (var effect in spec.Effects ?? Array.Empty<PolicyEffect>())
            {
                var label = effectNames != null && effectNames.TryGetValue(effect.Key, out var named) ? named : effect.Key;
                text.Append('\n').Append(label).Append(": ").Append(Percentage(effect.Multiplier));
                var gain = DailyGain(effect, dailyBaseline);
                if (gain.HasValue)
                {
                    text.Append(" (").Append(Signed(gain.Value)).Append(" per dag)");
                }
            }

            text.Append("\nNetto: ").Append(Signed(NetDaily(spec, citizens, dailyBaseline))).Append(" per dag");
            return text.ToString();
        }

        /// <summary>Een factor als verandering in procenten: 0,9 wordt "−10%", 1,25 wordt "+25%".</summary>
        public static string Percentage(float multiplier)
        {
            var percent = (int)Math.Round((multiplier - 1f) * 100f, MidpointRounding.AwayFromZero);
            if (percent == 0)
            {
                return "geen verandering";
            }

            var sign = percent > 0 ? "+" : "−";
            return sign + Math.Abs(percent).ToString(CultureInfo.CurrentCulture) + "%";
        }

        private static string Signed(long amount) => (amount < 0 ? "−" : "+") + HudText.Money(Math.Abs(amount));
    }
}
