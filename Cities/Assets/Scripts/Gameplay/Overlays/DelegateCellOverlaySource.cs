using System;

namespace Cities.Gameplay.Overlays
{
    /// <summary>Celoverlay op basis van een functie en een versieteller; handig voor lagen van de <c>WorldMap</c>.</summary>
    public sealed class DelegateCellOverlaySource : ICellOverlaySource
    {
        private readonly Func<int, int, float> _value;
        private readonly Func<long> _version;

        public DelegateCellOverlaySource(string id, string name, int cellsX, int cellsZ, OverlayLegend legend, Func<int, int, float> value, Func<long> version = null)
        {
            if (cellsX < 1 || cellsZ < 1)
            {
                throw new ArgumentOutOfRangeException(cellsX < 1 ? nameof(cellsX) : nameof(cellsZ), "Minstens één cel per richting.");
            }

            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            CellsX = cellsX;
            CellsZ = cellsZ;
            Legend = legend ?? throw new ArgumentNullException(nameof(legend));
            _value = value ?? throw new ArgumentNullException(nameof(value));
            _version = version;
        }

        public string Id { get; }

        public string Name { get; }

        public OverlayTarget Target => OverlayTarget.Cell;

        public OverlayLegend Legend { get; }

        public long Version => _version?.Invoke() ?? 0;

        public int CellsX { get; }

        public int CellsZ { get; }

        public float Value(int x, int z) => _value(x, z);
    }
}
