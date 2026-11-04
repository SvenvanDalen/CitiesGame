using System;

namespace Cities.Simulation.Fields
{
    /// <summary>De milieulagen waar een bos op werkt.</summary>
    public enum ForestPollutant : byte
    {
        Air,
        Soil,
    }

    /// <summary>
    /// Bossen als put voor vervuiling (E12-09): per cel haalt een bos een deel van de concentratie uit de lucht (stofdeeltjes op blad, CO2-opname) of
    /// uit de grond (planten en bodemleven, veel trager). Het deel groeit met de bosbedekking (0 tot 1) maar verzadigt: het eerste bos doet meer
    /// dan het laatste, <see cref="HalfCover"/> is de bedekking waarbij de helft van het maximum wordt gehaald. De put werkt naast de diffusiestap
    /// (<see cref="DiffusionStep"/>): eerst waait de vervuiling uit, daarna absorbeert elke cel zijn deel, dus een bos tussen fabriek en woonwijk
    /// vangt wat er overheen waait. Geluid dempt een bos ook (<see cref="NoiseFactor"/>). De waarden zijn schattingen.
    /// </summary>
    public static class ForestSink
    {
        /// <summary>Bedekking waarbij een bos ongeveer de helft van zijn maximale absorptie haalt.</summary>
        public const float HalfCover = 0.35f;

        /// <summary>Deel van de luchtvervuiling dat een volledig bos per stap opneemt.</summary>
        public const float MaxAirAbsorption = 0.04f;

        /// <summary>Deel van de grondvervuiling dat een volledig bos per dag opneemt (fytoremediatie is traag).</summary>
        public const float MaxSoilAbsorption = 0.002f;

        /// <summary>Hoeveel van het geluid een volledig bos wegneemt (0,3 is ongeveer 3 dB).</summary>
        public const float MaxNoiseReduction = 0.3f;

        /// <summary>Verzadigende respons van 0 tot 1: 0 zonder bos, 1 bij volle bedekking.</summary>
        public static float Response(float cover)
        {
            var c = Math.Min(Math.Max(cover, 0f), 1f);
            return c * (1f + HalfCover) / (c + HalfCover);
        }

        /// <summary>Bosdichtheid van de wereldlaag (0 tot 255) naar bedekking van 0 tot 1.</summary>
        public static float Cover(byte density) => density / 255f;

        /// <summary>Deel van de concentratie dat een cel met deze bedekking per stap opneemt.</summary>
        public static float Absorption(ForestPollutant pollutant, float cover) => pollutant switch
        {
            ForestPollutant.Air => MaxAirAbsorption * Response(cover),
            ForestPollutant.Soil => MaxSoilAbsorption * Response(cover),
            _ => throw new ArgumentOutOfRangeException(nameof(pollutant), pollutant, "Onbekende vervuiling."),
        };

        /// <summary>Factor (1 tot <c>1 - MaxNoiseReduction</c>) waarmee een cel met deze bedekking het geluid verzwakt.</summary>
        public static float NoiseFactor(float cover) => 1f - (MaxNoiseReduction * Response(cover));

        /// <summary>
        /// Laat elke cel zijn deel opnemen; <paramref name="values"/> en <paramref name="cover"/> zijn even lang. Geeft de totale opgenomen hoeveelheid,
        /// voor de statistieken.
        /// </summary>
        public static float Absorb(float[] values, float[] cover, ForestPollutant pollutant)
        {
            if (values is null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            if (cover is null)
            {
                throw new ArgumentNullException(nameof(cover));
            }

            if (values.Length != cover.Length)
            {
                throw new ArgumentException("De bedekkingskaart past niet op de waarden.", nameof(cover));
            }

            var absorbed = 0f;
            for (var i = 0; i < values.Length; i++)
            {
                var taken = values[i] * Absorption(pollutant, cover[i]);
                values[i] -= taken;
                absorbed += taken;
            }

            return absorbed;
        }

        /// <summary>Als de variant voor arrays, voor het veld van <see cref="DiffusionField"/> (lucht).</summary>
        public static float Absorb(DiffusionField field, float[] cover, ForestPollutant pollutant)
        {
            if (field is null)
            {
                throw new ArgumentNullException(nameof(field));
            }

            if (cover is null)
            {
                throw new ArgumentNullException(nameof(cover));
            }

            var values = field.Values;
            if (values.Length != cover.Length)
            {
                throw new ArgumentException("De bedekkingskaart past niet op het veld.", nameof(cover));
            }

            var absorbed = 0f;
            for (var i = 0; i < cover.Length; i++)
            {
                var taken = values[i] * Absorption(pollutant, cover[i]);
                values[i] -= taken;
                absorbed += taken;
            }

            return absorbed;
        }

        /// <summary>Laat het bos grondvervuiling opnemen; geeft de opgenomen hoeveelheid.</summary>
        public static float Absorb(SoilPollution soil, float[] cover)
        {
            if (soil is null)
            {
                throw new ArgumentNullException(nameof(soil));
            }

            if (cover is null)
            {
                throw new ArgumentNullException(nameof(cover));
            }

            if (soil.Width * soil.Height != cover.Length)
            {
                throw new ArgumentException("De bedekkingskaart past niet op de grondvervuiling.", nameof(cover));
            }

            var absorbed = 0f;
            for (var z = 0; z < soil.Height; z++)
            {
                for (var x = 0; x < soil.Width; x++)
                {
                    var before = soil[x, z];
                    var taken = before * Absorption(ForestPollutant.Soil, cover[(z * soil.Width) + x]);
                    soil.SetConcentration(x, z, before - taken);
                    absorbed += taken;
                }
            }

            return absorbed;
        }

        /// <summary>
        /// Rekent de bosdichtheid van een fijne kaart (<paramref name="srcWidth"/> x <paramref name="srcHeight"/>, rij voor rij) om naar de bedekking van een
        /// grovere kaart van <paramref name="width"/> x <paramref name="height"/> cellen: het gemiddelde van de fijne cellen in elk blok.
        /// </summary>
        public static float[] CoverMap(byte[] density, int srcWidth, int srcHeight, int width, int height)
        {
            if (density is null)
            {
                throw new ArgumentNullException(nameof(density));
            }

            if (width < 1 || height < 1 || srcWidth < width || srcHeight < height || density.Length != srcWidth * srcHeight)
            {
                throw new ArgumentException("De afmetingen van de kaarten kloppen niet.", nameof(density));
            }

            var cover = new float[width * height];
            var counts = new int[width * height];
            for (var z = 0; z < srcHeight; z++)
            {
                var cz = Math.Min(z * height / srcHeight, height - 1);
                for (var x = 0; x < srcWidth; x++)
                {
                    var i = (cz * width) + Math.Min(x * width / srcWidth, width - 1);
                    cover[i] += Cover(density[(z * srcWidth) + x]);
                    counts[i]++;
                }
            }

            for (var i = 0; i < cover.Length; i++)
            {
                cover[i] = counts[i] > 0 ? cover[i] / counts[i] : 0f;
            }

            return cover;
        }
    }
}
