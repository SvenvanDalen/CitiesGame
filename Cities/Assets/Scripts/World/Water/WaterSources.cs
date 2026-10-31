using System;
using System.Collections.Generic;

namespace Cities.World.Water
{
    /// <summary>Een bron (positief debiet) of afvoerpunt (negatief) van water op een plek in de wereld.</summary>
    public readonly struct WaterSource
    {
        public WaterSource(float x, float z, float discharge)
        {
            X = x;
            Z = z;
            Discharge = discharge;
        }

        public float X { get; }

        public float Z { get; }

        /// <summary>Debiet in m³/s: positief laat water toestromen, negatief laat het wegstromen.</summary>
        public float Discharge { get; }

        public bool IsDrain => Discharge < 0f;
    }

    /// <summary>
    /// De bronnen en afvoerpunten van de wereld. Pure lijst, los van het watergeheugen; de simulatie zet haar om in
    /// een debietlaag zodra <see cref="Version"/> verandert.
    /// </summary>
    public sealed class WaterSources
    {
        /// <summary>Ruime grens tegen kapotte saves en een speler die de kaart volklikt.</summary>
        public const int MaxCount = 10_000;

        private readonly List<WaterSource> _items = new List<WaterSource>();

        public IReadOnlyList<WaterSource> Items => _items;

        public int Count => _items.Count;

        /// <summary>Telt op bij elke wijziging.</summary>
        public int Version { get; private set; }

        /// <summary>Voegt een bron toe; <c>false</c> als de lijst vol is of het debiet 0 of geen getal is.</summary>
        public bool Add(float x, float z, float discharge)
        {
            if (_items.Count >= MaxCount || discharge == 0f || !float.IsFinite(discharge))
            {
                return false;
            }

            _items.Add(new WaterSource(x, z, discharge));
            Version++;
            return true;
        }

        /// <summary>De bron die het dichtst bij (x, z) ligt, binnen <paramref name="radius"/> meter.</summary>
        public WaterSource? Nearest(float x, float z, float radius)
        {
            var index = IndexOfNearest(x, z, radius);
            return index < 0 ? (WaterSource?)null : _items[index];
        }

        /// <summary>Verwijdert de bron die het dichtst bij (x, z) ligt, binnen <paramref name="radius"/> meter.</summary>
        public bool RemoveNearest(float x, float z, float radius)
        {
            var index = IndexOfNearest(x, z, radius);
            if (index < 0)
            {
                return false;
            }

            _items.RemoveAt(index);
            Version++;
            return true;
        }

        /// <summary>Vervangt alle bronnen, bijvoorbeeld bij het laden van een save.</summary>
        public void Replace(IEnumerable<WaterSource> sources)
        {
            _items.Clear();
            _items.AddRange(sources);
            Version++;
        }

        private int IndexOfNearest(float x, float z, float radius)
        {
            var best = -1;
            var bestDistance = radius * radius;
            for (var i = 0; i < _items.Count; i++)
            {
                var dx = _items[i].X - x;
                var dz = _items[i].Z - z;
                var distance = (dx * dx) + (dz * dz);
                if (distance <= bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            return best;
        }
    }
}
