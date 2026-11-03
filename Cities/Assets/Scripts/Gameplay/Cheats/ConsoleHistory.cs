using System;
using System.Collections.Generic;

namespace Cities.Gameplay.Cheats
{
    /// <summary>Eerder ingevoerde regels, om met pijltje omhoog en omlaag terug te halen.</summary>
    public sealed class ConsoleHistory
    {
        private readonly List<string> _lines = new List<string>();
        private readonly int _capacity;
        private int _cursor;

        public ConsoleHistory(int capacity = 50)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _capacity = capacity;
        }

        public int Count => _lines.Count;

        /// <summary>Bewaart een regel; lege regels en een herhaling van de vorige tellen niet mee.</summary>
        public void Add(string line)
        {
            if (!string.IsNullOrWhiteSpace(line) && (_lines.Count == 0 || _lines[_lines.Count - 1] != line))
            {
                _lines.Add(line);
                if (_lines.Count > _capacity)
                {
                    _lines.RemoveAt(0);
                }
            }

            _cursor = _lines.Count;
        }

        /// <summary>De volgende oudere regel; blijft bij de oudste staan. <c>null</c> als er niets is.</summary>
        public string Previous()
        {
            if (_lines.Count == 0)
            {
                return null;
            }

            _cursor = Math.Max(0, _cursor - 1);
            return _lines[_cursor];
        }

        /// <summary>De volgende nieuwere regel; voorbij de nieuwste een lege regel.</summary>
        public string Next()
        {
            if (_lines.Count == 0)
            {
                return null;
            }

            _cursor = Math.Min(_lines.Count, _cursor + 1);
            return _cursor == _lines.Count ? string.Empty : _lines[_cursor];
        }
    }
}
