using System;
using System.Collections.Generic;
using Cities.Gameplay.Services;
using Cities.Simulation.Fields;
using Unity.Mathematics;

namespace Cities.Gameplay.Environment
{
    /// <summary>
    /// Milieu en landwaarde van de stad (E26-09). Industrie veroorzaakt lucht- en geluidsoverlast, wegen geluid; de landwaarde volgt uit
    /// stadsdiensten, nabijheid van haltes en vervuiling via <see cref="LandValueModel"/>.
    /// </summary>
    public sealed class CityEnvironment
    {
        /// <summary>Afstand (m) waarop luchtvervuiling van een fabriek is uitgedoofd.</summary>
        public const float AirRadius = 400f;

        /// <summary>Afstand (m) waarop een halte nog meetelt voor de landwaarde.</summary>
        public const float TransitRadius = 250f;

        private readonly List<float2> _industry = new List<float2>();
        private readonly List<float2> _homes = new List<float2>();
        private readonly List<float2> _stops = new List<float2>();
        private NoiseSource[] _noise = Array.Empty<NoiseSource>();
        private bool _noiseDirty;

        public int IndustryCount => _industry.Count;

        public void AddIndustry(float2 position)
        {
            _industry.Add(position);
            _noiseDirty = true;
        }

        public void AddHome(float2 position) => _homes.Add(position);

        public void AddStop(float2 position) => _stops.Add(position);

        public void SetStops(IEnumerable<float2> positions)
        {
            _stops.Clear();
            _stops.AddRange(positions);
        }

        public IReadOnlyList<float2> Homes => _homes;

        public IReadOnlyList<float2> Industry => _industry;

        /// <summary>Luchtvervuiling op een punt, 0 tot 1.</summary>
        public float AirAt(float2 point)
        {
            var total = 0f;
            foreach (var source in _industry)
            {
                var d = math.distance(point, source);
                if (d < AirRadius)
                {
                    total += 1f - (d / AirRadius);
                }
            }

            return math.saturate(total);
        }

        /// <summary>Geluidsniveau in dB op een punt.</summary>
        public float NoiseAt(float2 point)
        {
            if (_noiseDirty)
            {
                _noise = new NoiseSource[_industry.Count];
                for (var i = 0; i < _noise.Length; i++)
                {
                    _noise[i] = new NoiseSource(_industry[i], NoiseKind.Industry);
                }

                _noiseDirty = false;
            }

            return NoiseModel.LevelAt(point, _noise);
        }

        /// <summary>Landwaarde op een punt, 0 tot 1.</summary>
        public float LandValueAt(float2 point, CityServices services)
        {
            var near = 0f;
            foreach (var stop in _stops)
            {
                near = Math.Max(near, 1f - math.saturate(math.distance(point, stop) / TransitRadius));
            }

            var coverage = services?.CoverageAt(point) ?? 0.5f;
            return LandValueModel.Value(new LandValueFactors(coverage, 0.3f, 0f, near, AirAt(point)));
        }

        /// <summary>Gemiddelde landwaarde onder de woningen; 0,5 (neutraal) zonder woningen.</summary>
        public float HomeAppeal(CityServices services)
        {
            if (_homes.Count == 0)
            {
                return 0.5f;
            }

            var sum = 0f;
            foreach (var home in _homes)
            {
                sum += LandValueAt(home, services);
            }

            return sum / _homes.Count;
        }
    }
}
