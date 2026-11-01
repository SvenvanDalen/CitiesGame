using System;
using System.Collections.Generic;
using Cities.Core.Randomness;
using Unity.Mathematics;

namespace Cities.Simulation.Disasters
{
    /// <summary>
    /// Verspreiding van een brand over een raster van cellen (E15-02), voor gebouwen en bossen. Elke cel heeft brandstof (0 tot 1: de
    /// bebouwings- of boomdichtheid). Een brandende cel brandt <see cref="BurnDays"/> dagen en laat dan as achter (geen brandstof meer).
    /// Per dag slaat het vuur over naar de vier buren met kans <see cref="SpreadChance"/> maal hun brandstof, versterkt aan de lijzijde
    /// van de wind en verzwakt tegen de wind in. Dichte bebouwing brandt dus sneller door dan een dunne rand.
    /// </summary>
    public sealed class FireSpread
    {
        /// <summary>Kans per dag dat een brandende cel een buur met volle brandstof zonder wind aansteekt.</summary>
        public const float SpreadChance = 0.35f;

        public const int BurnDays = 3;

        private static readonly int2[] Neighbours = { new int2(1, 0), new int2(-1, 0), new int2(0, 1), new int2(0, -1) };

        private readonly float[] _fuel;
        private readonly int[] _burning;

        public FireSpread(int width, int height)
        {
            if (width < 1 || height < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Het raster heeft minstens één cel nodig.");
            }

            Width = width;
            Height = height;
            _fuel = new float[width * height];
            _burning = new int[width * height];
        }

        public int Width { get; }

        public int Height { get; }

        /// <summary>Aantal cellen dat nu brandt.</summary>
        public int BurningCount { get; private set; }

        /// <summary>Aantal cellen dat sinds het begin is uitgebrand.</summary>
        public int BurnedCount { get; private set; }

        public float FuelAt(int x, int y) => _fuel[Index(x, y)];

        public bool IsBurning(int x, int y) => _burning[Index(x, y)] > 0;

        public void SetFuel(int x, int y, float fuel) => _fuel[Index(x, y)] = math.clamp(fuel, 0f, 1f);

        /// <summary>Steekt een cel aan; zonder brandstof gebeurt er niets.</summary>
        public void Ignite(int x, int y)
        {
            var i = Index(x, y);
            if (_fuel[i] > 0f && _burning[i] == 0)
            {
                _burning[i] = BurnDays;
                BurningCount++;
            }
        }

        /// <summary>Blust een cel (brandweer, E10-02); de brandstof blijft staan.</summary>
        public void Extinguish(int x, int y)
        {
            var i = Index(x, y);
            if (_burning[i] > 0)
            {
                _burning[i] = 0;
                BurningCount--;
            }
        }

        /// <summary>
        /// Eén dag: het vuur slaat over naar de buren, daarna brandt elke brandende cel een dag op. <paramref name="wind"/> is de
        /// windrichting met kracht (lengte 0 is windstil, 1 is een stevige wind); het vuur trekt mee met de wind.
        /// </summary>
        public void Step(ref DeterministicRandom random, float2 wind)
        {
            var ignitions = new List<int>();
            for (var i = 0; i < _burning.Length; i++)
            {
                if (_burning[i] > 0)
                {
                    Spread(ref random, wind, i % Width, i / Width, ignitions);
                }
            }

            for (var i = 0; i < _burning.Length; i++)
            {
                if (_burning[i] > 0 && --_burning[i] == 0)
                {
                    _fuel[i] = 0f;
                    BurningCount--;
                    BurnedCount++;
                }
            }

            foreach (var i in ignitions)
            {
                Ignite(i % Width, i / Width);
            }
        }

        private void Spread(ref DeterministicRandom random, float2 wind, int x, int y, List<int> ignitions)
        {
            foreach (var step in Neighbours)
            {
                var nx = x + step.x;
                var ny = y + step.y;
                if (nx >= 0 && ny >= 0 && nx < Width && ny < Height && _burning[Index(nx, ny)] == 0)
                {
                    var chance = SpreadChance * _fuel[Index(nx, ny)] * math.max(0f, 1f + math.dot(wind, new float2(step.x, step.y)));
                    if (random.NextFloat() < chance)
                    {
                        ignitions.Add(Index(nx, ny));
                    }
                }
            }
        }

        private int Index(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
            {
                throw new ArgumentOutOfRangeException(nameof(x), $"Cel ({x}, {y}) ligt buiten het raster.");
            }

            return (y * Width) + x;
        }
    }
}
