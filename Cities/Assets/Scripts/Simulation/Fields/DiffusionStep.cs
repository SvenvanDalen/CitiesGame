using Unity.Mathematics;

namespace Cities.Simulation.Fields
{
    /// <summary>
    /// De rekenregel van één diffusiestap voor één cel (E12-01), los van de opslag zodat de Burst-job in <see cref="DiffusionField"/> en
    /// de tests precies dezelfde code gebruiken.
    /// </summary>
    public static class DiffusionStep
    {
        /// <summary>
        /// Indices van de west-, oost-, zuid- en noordbuur van <paramref name="index"/> in een rij-voor-rij kaart. Een ontbrekende buur
        /// is de cel zelf, zodat er niets over de rand wordt uitgewisseld.
        /// </summary>
        public static int4 Neighbours(int index, int width, int height)
        {
            var x = index % width;
            var z = index / width;
            return new int4(
                x > 0 ? index - 1 : index,
                x < width - 1 ? index + 1 : index,
                z > 0 ? index - width : index,
                z < height - 1 ? index + width : index);
        }

        /// <summary>De nieuwe waarde van een cel, met de waarden van de buren in dezelfde volgorde als <see cref="Neighbours"/>.</summary>
        public static float Next(float value, float4 neighbours, in DiffusionSettings settings)
        {
            var wind = settings.Wind;
            var result = value + (settings.Spread * (math.csum(neighbours) - (4f * value)));
            // De bovenwindse buur schuift mee: wind in +x haalt uit het westen, wind in -x uit het oosten.
            result += wind.x >= 0f ? wind.x * (neighbours.x - value) : -wind.x * (neighbours.y - value);
            result += wind.y >= 0f ? wind.y * (neighbours.z - value) : -wind.y * (neighbours.w - value);
            return result * (1f - settings.Decay);
        }
    }
}
