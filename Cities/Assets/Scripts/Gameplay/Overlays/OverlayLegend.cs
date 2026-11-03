using System;
using System.Globalization;

namespace Cities.Gameplay.Overlays
{
    /// <summary>Titel, eenheid, bereik en kleurverloop van een overlay: alles wat de legenda en de schaal nodig hebben.</summary>
    public sealed class OverlayLegend
    {
        // Geen CultureInfo("nl-NL"): die bestaat niet in globalization-invariant mode (CI, containers).
        private static readonly NumberFormatInfo Dutch = new NumberFormatInfo { NumberDecimalSeparator = ",", NumberGroupSeparator = "." };

        public OverlayLegend(string title, string unit, float min, float max, OverlayColorScale scale, string format = "0.#")
        {
            if (float.IsNaN(min) || float.IsNaN(max) || float.IsInfinity(min) || float.IsInfinity(max))
            {
                throw new ArgumentOutOfRangeException(nameof(min), "Het bereik moet eindig zijn.");
            }

            if (!(max > min))
            {
                throw new ArgumentException("Het maximum moet groter zijn dan het minimum.", nameof(max));
            }

            Title = title ?? throw new ArgumentNullException(nameof(title));
            Unit = unit ?? string.Empty;
            Min = min;
            Max = max;
            Scale = scale ?? throw new ArgumentNullException(nameof(scale));
            Format = format ?? throw new ArgumentNullException(nameof(format));
        }

        public string Title { get; }

        public string Unit { get; }

        public float Min { get; }

        public float Max { get; }

        public OverlayColorScale Scale { get; }

        public string Format { get; }

        /// <summary>Waarde naar 0 tot 1 binnen het bereik; een niet-getal blijft een niet-getal.</summary>
        public float Normalize(float value) => float.IsNaN(value) ? float.NaN : (value - Min) / (Max - Min);

        public OverlayColor ColorOf(float value) => Scale.Evaluate(Normalize(value));

        /// <summary>Waarde op een punt van de schaal (0 tot 1).</summary>
        public float ValueAt(float t) => Min + (Math.Max(0f, Math.Min(1f, t)) * (Max - Min));

        /// <summary>Waarde met eenheid, bijvoorbeeld "12,5 m".</summary>
        public string Describe(float value)
        {
            if (float.IsNaN(value))
            {
                return "geen gegevens";
            }

            var text = value.ToString(Format, Dutch);
            return Unit.Length == 0 ? text : text + " " + Unit;
        }
    }
}
