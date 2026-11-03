using System;

namespace Cities.Gameplay.Overlays
{
    /// <summary>
    /// Bakt een celoverlay in stukken naar een kleurenbuffer (één pixel per cel). Het werk loopt over meerdere
    /// frames (<see cref="Step"/> met een celbudget), terwijl de vorige overlay in <see cref="Front"/> zichtbaar blijft; pas als alles klaar is
    /// wisselen voor- en achterbuffer. Zo haperen wisselen tussen overlays en verversen niet, ook niet op de volle kaart.
    /// </summary>
    public sealed class CellOverlayBaker
    {
        private uint[] _back = Array.Empty<uint>();
        private uint[] _front = Array.Empty<uint>();
        private ICellOverlaySource _source;
        private int _cursor;
        private int _completed;

        public int Width { get; private set; }

        public int Height { get; private set; }

        /// <summary>De laatst volledig gebakken buffer (RGBA32, rij na rij, <see cref="OverlayColor.Pack"/>).</summary>
        public uint[] Front => _front;

        public bool IsBaking => _source != null;

        /// <summary>Aandeel gebakken van de lopende bake, 0 tot 1.</summary>
        public float Progress => _source == null ? 1f : (float)_cursor / Math.Max(1, Width * Height);

        /// <summary>Teller die oploopt bij elke voltooide bake; de weergave uploadt <see cref="Front"/> als hij verandert.</summary>
        public int CompletedCount => _completed;

        /// <summary>Begint een nieuwe bake en laat een lopende vallen.</summary>
        public void Begin(ICellOverlaySource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            var size = source.CellsX * source.CellsZ;
            if (_back.Length != size)
            {
                _back = new uint[size];
            }

            Width = source.CellsX;
            Height = source.CellsZ;
            _cursor = 0;
        }

        /// <summary>Zet de weergave terug op niets; <see cref="Front"/> blijft staan tot de volgende bake klaar is.</summary>
        public void Cancel() => _source = null;

        /// <summary>Bakt hoogstens <paramref name="maxCells"/> cellen. Geeft <c>true</c> als de bake nu klaar is.</summary>
        public bool Step(int maxCells)
        {
            if (_source == null)
            {
                return false;
            }

            var legend = _source.Legend;
            var total = Width * Height;
            var end = (int)Math.Min((long)total, (long)_cursor + Math.Max(1, maxCells));
            for (var i = _cursor; i < end; i++)
            {
                var z = Math.DivRem(i, Width, out var x);
                _back[i] = legend.ColorOf(_source.Value(x, z)).Pack();
            }

            _cursor = end;
            if (_cursor < total)
            {
                return false;
            }

            (_front, _back) = (_back, _front);
            _source = null;
            _completed++;
            return true;
        }
    }
}
