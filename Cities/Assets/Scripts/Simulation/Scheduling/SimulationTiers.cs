using System;
using Unity.Mathematics;

namespace Cities.Simulation.Scheduling
{
    /// <summary>Hoe gedetailleerd een gebied gesimuleerd wordt (E23-04).</summary>
    public enum SimulationTier : byte
    {
        /// <summary>Verre of onbezochte delen: statistisch, niet elke tick en met een gegroepeerde tijdstap.</summary>
        Statistical,

        /// <summary>Nabije of recent bezochte delen: elke tick volledig.</summary>
        Full,
    }

    /// <summary>
    /// Gelaagde simulatie (E23-04). De kaart wordt in vierkante gebieden verdeeld. Een gebied binnen <see cref="FullRadius"/> van het
    /// brandpunt (de camera) is <see cref="SimulationTier.Full"/> en blijft dat nog <see cref="LingerTicks"/> ticks nadat de camera
    /// weg is, zodat er niets inklapt terwijl de speler even wegkijkt. Alle andere gebieden zijn statistisch: een systeem stapt ze maar
    /// één keer per <see cref="StatisticalInterval"/> ticks en krijgt dan het aantal verstreken ticks mee om met een grotere
    /// tijdstap te rekenen. De start van elk gebied is verspreid over de interval, zodat de last gelijk verdeeld is. De bezoekstand wordt
    /// niet opgeslagen; na laden begint alles statistisch en volgt de laag direct uit het brandpunt.
    /// </summary>
    public sealed class SimulationTiers
    {
        private readonly long[] _lastVisit;
        private readonly long[] _lastStep;

        public SimulationTiers(int regionsX, int regionsZ, float regionSize, float fullRadius, int statisticalInterval = 10, int lingerTicks = 600)
        {
            if (regionsX < 1 || regionsZ < 1)
            {
                throw new ArgumentOutOfRangeException(regionsX < 1 ? nameof(regionsX) : nameof(regionsZ), "Minstens één gebied per richting.");
            }

            if (regionSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(regionSize), regionSize, "Gebieden hebben een afmeting nodig.");
            }

            if (fullRadius < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(fullRadius), fullRadius, "Straal kan niet negatief zijn.");
            }

            if (statisticalInterval < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(statisticalInterval), statisticalInterval, "Minstens één tick.");
            }

            if (lingerTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(lingerTicks), lingerTicks, "Nasleep kan niet negatief zijn.");
            }

            RegionsX = regionsX;
            RegionsZ = regionsZ;
            RegionSize = regionSize;
            FullRadius = fullRadius;
            StatisticalInterval = statisticalInterval;
            LingerTicks = lingerTicks;
            _lastVisit = new long[regionsX * regionsZ];
            _lastStep = new long[regionsX * regionsZ];
            Array.Fill(_lastVisit, long.MinValue);
        }

        public int RegionsX { get; }

        public int RegionsZ { get; }

        public float RegionSize { get; }

        public float FullRadius { get; }

        public int StatisticalInterval { get; }

        public int LingerTicks { get; }

        public int RegionCount => _lastVisit.Length;

        /// <summary>Gebied dat een punt (in meters) dekt; buiten de kaart wordt vastgezet op de rand.</summary>
        public int RegionOf(float2 position)
        {
            var x = math.clamp((int)math.floor(position.x / RegionSize), 0, RegionsX - 1);
            var z = math.clamp((int)math.floor(position.y / RegionSize), 0, RegionsZ - 1);
            return (z * RegionsX) + x;
        }

        /// <summary>Markeert alle gebieden binnen <see cref="FullRadius"/> van <paramref name="focus"/> als bezocht op <paramref name="tick"/>.</summary>
        public void Visit(float2 focus, long tick)
        {
            var minX = math.max(0, (int)math.floor((focus.x - FullRadius) / RegionSize));
            var maxX = math.min(RegionsX - 1, (int)math.floor((focus.x + FullRadius) / RegionSize));
            var minZ = math.max(0, (int)math.floor((focus.y - FullRadius) / RegionSize));
            var maxZ = math.min(RegionsZ - 1, (int)math.floor((focus.y + FullRadius) / RegionSize));
            for (var z = minZ; z <= maxZ; z++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    if (DistanceToRegion(focus, x, z) <= FullRadius)
                    {
                        _lastVisit[(z * RegionsX) + x] = tick;
                    }
                }
            }
        }

        public SimulationTier TierOf(int region, long tick) =>
            _lastVisit[region] != long.MinValue && tick - _lastVisit[region] <= LingerTicks ? SimulationTier.Full : SimulationTier.Statistical;

        public SimulationTier TierAt(float2 position, long tick) => TierOf(RegionOf(position), tick);

        /// <summary>Aantal gebieden dat nu volledig gesimuleerd wordt.</summary>
        public int FullCount(long tick)
        {
            var count = 0;
            for (var i = 0; i < _lastVisit.Length; i++)
            {
                count += TierOf(i, tick) == SimulationTier.Full ? 1 : 0;
            }

            return count;
        }

        /// <summary>
        /// Moet een systeem dit gebied nu stappen? Een volledig gebied altijd (<paramref name="elapsedTicks"/> is 1); een statistisch gebied één keer
        /// per interval, met het aantal ticks sinds de vorige stap.
        /// </summary>
        public bool ShouldStep(int region, long tick, out int elapsedTicks)
        {
            if (TierOf(region, tick) == SimulationTier.Full)
            {
                _lastStep[region] = tick;
                elapsedTicks = 1;
                return true;
            }

            if ((tick + region) % StatisticalInterval != 0)
            {
                elapsedTicks = 0;
                return false;
            }

            elapsedTicks = (int)math.max(1L, tick - _lastStep[region]);
            _lastStep[region] = tick;
            return true;
        }

        private float DistanceToRegion(float2 point, int x, int z)
        {
            var min = new float2(x, z) * RegionSize;
            var nearest = math.clamp(point, min, min + new float2(RegionSize));
            return math.distance(point, nearest);
        }
    }
}
