using System;
using System.Collections.Generic;

namespace Cities.Gameplay.Overlays
{
    /// <summary>
    /// Kleurverloop over 0 tot 1 met twee of meer ankers. Waarden buiten het bereik krijgen de kleur van het
    /// dichtstbijzijnde anker en een niet-getal (<see cref="float.NaN"/>) krijgt <see cref="Missing"/>.
    /// </summary>
    public sealed class OverlayColorScale
    {
        private static readonly OverlayColor NoData = new OverlayColor(90, 90, 90, 0);

        private readonly OverlayColor[] _stops;

        public OverlayColorScale(IReadOnlyList<OverlayColor> stops, OverlayColor missing)
        {
            if (stops is null)
            {
                throw new ArgumentNullException(nameof(stops));
            }

            if (stops.Count < 2)
            {
                throw new ArgumentException("Een kleurverloop heeft minstens twee ankers.", nameof(stops));
            }

            _stops = new OverlayColor[stops.Count];
            for (var i = 0; i < _stops.Length; i++)
            {
                _stops[i] = stops[i];
            }

            Missing = missing;
        }

        /// <summary>Groen via geel naar rood: hoog is slecht (vervuiling, criminaliteit).</summary>
        public static OverlayColorScale Hot { get; } = new OverlayColorScale(
            new[] { new OverlayColor(46, 160, 67), new OverlayColor(230, 200, 40), new OverlayColor(215, 48, 39) },
            NoData);

        /// <summary>Rood via geel naar groen: hoog is goed (landwaarde, geluk, dekking).</summary>
        public static OverlayColorScale Good { get; } = new OverlayColorScale(
            new[] { new OverlayColor(215, 48, 39), new OverlayColor(230, 200, 40), new OverlayColor(46, 160, 67) },
            NoData);

        /// <summary>Donkerblauw via lichtblauw en groen naar bruin en wit: hoogtekaart.</summary>
        public static OverlayColorScale Terrain { get; } = new OverlayColorScale(
            new[]
            {
                new OverlayColor(30, 70, 140), new OverlayColor(70, 160, 200), new OverlayColor(110, 170, 80),
                new OverlayColor(150, 120, 80), new OverlayColor(245, 245, 245),
            },
            NoData);

        public int StopCount => _stops.Length;

        /// <summary>Kleur voor waarden zonder betekenis (geen gegevens); standaard doorzichtig.</summary>
        public OverlayColor Missing { get; }

        public OverlayColor Stop(int index) => _stops[index];

        /// <summary>Kleur bij <paramref name="t"/> (0 tot 1).</summary>
        public OverlayColor Evaluate(float t)
        {
            if (float.IsNaN(t))
            {
                return Missing;
            }

            if (t <= 0f)
            {
                return _stops[0];
            }

            if (t >= 1f)
            {
                return _stops[_stops.Length - 1];
            }

            var scaled = t * (_stops.Length - 1);
            var index = (int)scaled;
            return OverlayColor.Lerp(_stops[index], _stops[index + 1], scaled - index);
        }
    }
}
