using System;

namespace Cities.World.Water
{
    /// <summary>Randen van de kaart waar de zee ligt: daar is de waterstand vast.</summary>
    [Flags]
    public enum SeaEdges : byte
    {
        None = 0,
        West = 1,
        East = 2,
        South = 4,
        North = 8,
    }

    /// <summary>Instellingen van de watersimulatie.</summary>
    public struct WaterSettings
    {
        /// <summary>Vaste stap in seconden; groter dan dit wordt in meerdere stappen gerekend (stabiliteit).</summary>
        public float StepSeconds { get; set; }

        public float Gravity { get; set; }

        /// <summary>Fractie van de stroom die een stap overleeft; houdt golven onder controle.</summary>
        public float Damping { get; set; }

        /// <summary>Hoogte van de zee in meters.</summary>
        public float SeaLevel { get; set; }

        public SeaEdges Sea { get; set; }

        /// <summary>Hoe snel de zee een randcel naar zeeniveau trekt, per seconde.</summary>
        public float SeaRelaxation { get; set; }

        public static WaterSettings Default => new WaterSettings
        {
            StepSeconds = 0.1f,
            Gravity = 9.81f,
            Damping = 0.995f,
            SeaLevel = 0f,
            Sea = SeaEdges.West,
            SeaRelaxation = 0.5f,
        };
    }

    /// <summary>Afmetingen van het grove raster en de lengte van één stap.</summary>
    public readonly struct WaterStep
    {
        public WaterStep(int width, int depth, float cellSize, float dt)
        {
            Width = width;
            Depth = depth;
            CellSize = cellSize;
            Dt = dt;
        }

        public int Width { get; }

        public int Depth { get; }

        /// <summary>Zijde van een cel in meters.</summary>
        public float CellSize { get; }

        /// <summary>Stap in seconden.</summary>
        public float Dt { get; }
    }

    /// <summary>Verhouding tussen het grove watergrid en het fijne terreinraster, dat op dezelfde hoekpunten begint.</summary>
    public readonly struct WaterResample
    {
        public WaterResample(int coarseWidth, int coarseDepth, int ratio, int fineWidth)
        {
            CoarseWidth = coarseWidth;
            CoarseDepth = coarseDepth;
            Ratio = ratio;
            FineWidth = fineWidth;
        }

        public int CoarseWidth { get; }

        public int CoarseDepth { get; }

        /// <summary>Fijne cellen per grove cel in elke richting.</summary>
        public int Ratio { get; }

        public int FineWidth { get; }
    }

    /// <summary>
    /// Ondiepwatermodel met virtuele pijpen (Mei e.a.): elke cel heeft vier stromen naar zijn buren, aangedreven door het
    /// verschil in waterspiegel. Pure rekenkern op rijen, zodat dezelfde code in Burst-jobs draait en in CI te testen is.
    /// Stroom is volume per seconde (m³/s) en staat per cel als vier waarden: −x, +x, −z, +z.
    /// </summary>
    public static class ShallowWater
    {
        public const int Directions = 4;

        /// <summary>Diepte in meters vanaf waar een grof hoekpunt als nat telt.</summary>
        private const float WetThreshold = 0.01f;

        /// <summary>Werkt de stromen van één rij bij uit de waterspiegels.</summary>
        public static void UpdateFlux(int row, in WaterStep step, in WaterSettings settings,
            ReadOnlySpan<float> ground, ReadOnlySpan<float> water, Span<float> flux)
        {
            var (width, depth, cellSize, dt) = (step.Width, step.Depth, step.CellSize, step.Dt);
            var area = cellSize * cellSize;
            for (var x = 0; x < width; x++)
            {
                var i = (row * width) + x;
                var surface = ground[i] + water[i];
                var drive = settings.Gravity * dt * cellSize;
                var left = Next(flux[i * Directions], surface, Surface(ground, water, width, depth, x - 1, row, surface), drive, settings.Damping);
                var right = Next(flux[(i * Directions) + 1], surface, Surface(ground, water, width, depth, x + 1, row, surface), drive, settings.Damping);
                var down = Next(flux[(i * Directions) + 2], surface, Surface(ground, water, width, depth, x, row - 1, surface), drive, settings.Damping);
                var up = Next(flux[(i * Directions) + 3], surface, Surface(ground, water, width, depth, x, row + 1, surface), drive, settings.Damping);

                // Een cel kan niet meer water afgeven dan er staat.
                var outflow = (left + right + down + up) * dt;
                var scale = outflow > 0f ? Math.Min(1f, water[i] * area / outflow) : 1f;
                flux[i * Directions] = left * scale;
                flux[(i * Directions) + 1] = right * scale;
                flux[(i * Directions) + 2] = down * scale;
                flux[(i * Directions) + 3] = up * scale;
            }
        }

        /// <summary>Werkt de waterdiepte van één rij bij uit de stromen van de cel en zijn buren, plus de bronnen (m³/s).</summary>
        public static void UpdateDepth(int row, in WaterStep step, in WaterSettings settings,
            ReadOnlySpan<float> ground, ReadOnlySpan<float> flux, ReadOnlySpan<float> source, Span<float> water)
        {
            var (width, depth, cellSize, dt) = (step.Width, step.Depth, step.CellSize, step.Dt);
            var area = cellSize * cellSize;
            for (var x = 0; x < width; x++)
            {
                var i = (row * width) + x;
                var net = source[i];
                net -= flux[i * Directions] + flux[(i * Directions) + 1] + flux[(i * Directions) + 2] + flux[(i * Directions) + 3];
                if (x > 0)
                {
                    net += flux[((i - 1) * Directions) + 1];
                }

                if (x < width - 1)
                {
                    net += flux[(i + 1) * Directions];
                }

                if (row > 0)
                {
                    net += flux[((i - width) * Directions) + 3];
                }

                if (row < depth - 1)
                {
                    net += flux[((i + width) * Directions) + 2];
                }

                net += SeaInflow(x, row, width, depth, area, ground[i] + water[i], settings);
                water[i] = Math.Max(0f, water[i] + (net * dt / area));
            }
        }

        /// <summary>Water dat de zee een randcel in trekt (negatief: eruit) richting het zeeniveau, in m³/s.</summary>
        private static float SeaInflow(int x, int row, int width, int depth, float area, float surface, in WaterSettings settings)
        {
            var edges = 0;
            edges += (settings.Sea & SeaEdges.West) != 0 && x == 0 ? 1 : 0;
            edges += (settings.Sea & SeaEdges.East) != 0 && x == width - 1 ? 1 : 0;
            edges += (settings.Sea & SeaEdges.South) != 0 && row == 0 ? 1 : 0;
            edges += (settings.Sea & SeaEdges.North) != 0 && row == depth - 1 ? 1 : 0;
            return edges == 0 ? 0f : edges * (settings.SeaLevel - surface) * area * settings.SeaRelaxation;
        }

        private static float Next(float previous, float surface, float neighbour, float drive, float damping) =>
            Math.Max(0f, (previous * damping) + (drive * (surface - neighbour)));

        /// <summary>Waterspiegel van een buurcel; buiten de kaart is dat een muur (de eigen spiegel). De zee werkt via <see cref="SeaInflow"/>.</summary>
        private static float Surface(ReadOnlySpan<float> ground, ReadOnlySpan<float> water, int width, int depth, int x, int z, float own)
        {
            if (x >= 0 && x < width && z >= 0 && z < depth)
            {
                var i = (z * width) + x;
                return ground[i] + water[i];
            }

            return own;
        }

        /// <summary>Neemt elke <paramref name="ratio"/>-de waarde van een fijn raster over naar één rij van een grof raster dat op dezelfde hoekpunten begint.</summary>
        public static void DownsampleRow(int row, in WaterResample grid, ReadOnlySpan<float> fine, Span<float> coarse)
        {
            for (var x = 0; x < grid.CoarseWidth; x++)
            {
                coarse[(row * grid.CoarseWidth) + x] = fine[(row * grid.Ratio * grid.FineWidth) + (x * grid.Ratio)];
            }
        }

        /// <summary>
        /// Zet het grove water om in waterdiepte op één rij van het fijne raster: de bilineair verdeelde waterspiegel minus
        /// het fijne terrein. Zo volgt de oever het terrein op 8 m, ook al rekent de simulatie op 32 m. Waar alle vier de
        /// grove hoekpunten droog zijn, blijft het droog.
        /// </summary>
        public static void UpsampleRow(int z, in WaterResample grid, ReadOnlySpan<float> coarseGround, ReadOnlySpan<float> coarseWater,
            ReadOnlySpan<float> fineGround, Span<float> fineWater)
        {
            var (coarseWidth, coarseDepth, ratio, fineWidth) = (grid.CoarseWidth, grid.CoarseDepth, grid.Ratio, grid.FineWidth);
            var cz = Math.Min(z / ratio, coarseDepth - 2);
            var tz = (z - (cz * ratio)) / (float)ratio;
            for (var x = 0; x < fineWidth; x++)
            {
                var cx = Math.Min(x / ratio, coarseWidth - 2);
                var tx = (x - (cx * ratio)) / (float)ratio;
                var i00 = (cz * coarseWidth) + cx;
                var i10 = i00 + 1;
                var i01 = i00 + coarseWidth;
                var i11 = i01 + 1;
                var wet = coarseWater[i00] > WetThreshold || coarseWater[i10] > WetThreshold ||
                          coarseWater[i01] > WetThreshold || coarseWater[i11] > WetThreshold;
                var surface = Lerp(
                    Lerp(coarseGround[i00] + coarseWater[i00], coarseGround[i10] + coarseWater[i10], tx),
                    Lerp(coarseGround[i01] + coarseWater[i01], coarseGround[i11] + coarseWater[i11], tx), tz);
                var f = (z * fineWidth) + x;
                fineWater[f] = wet ? Math.Max(0f, surface - fineGround[f]) : 0f;
            }
        }

        private static float Lerp(float a, float b, float t) => a + ((b - a) * t);
    }
}
