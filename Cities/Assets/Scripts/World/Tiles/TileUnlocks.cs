using System;
using Cities.Simulation.Economy;
using Cities.World.Grid;

namespace Cities.World.Tiles
{
    /// <summary>
    /// Welke kaarttegels ontgrendeld zijn (E03-09). Een tegel moet grenzen aan een ontgrendelde tegel en wordt ontgrendeld
    /// met geld (<see cref="TryBuy"/>) of gratis zodra de stad groot genoeg is (<see cref="TryUnlockByPopulation"/>).
    /// Prijs en bevolkingsdrempel stijgen met elke ontgrendelde tegel. Bouwen op een vergrendelde tegel mag niet.
    /// </summary>
    public sealed class TileUnlocks
    {
        public const string Locked = "Tegel is nog niet ontgrendeld";
        public const string NotAdjacent = "Grenst niet aan een ontgrendelde tegel";
        public const string AlreadyUnlocked = "Al ontgrendeld";
        public const string OutsideMap = "Buiten de kaart";

        private readonly bool[] _unlocked;

        /// <param name="start">De tegel waar de stad begint; die is altijd ontgrendeld.</param>
        /// <param name="basePrice">Prijs van de eerste extra tegel.</param>
        /// <param name="populationPerTile">Bevolking per al ontgrendelde tegel die nodig is voor een gratis tegel.</param>
        public TileUnlocks(WorldDimensions dimensions, TileCoord start, long basePrice = 100_000, int populationPerTile = 5_000)
        {
            if (!dimensions.Contains(start))
            {
                throw new ArgumentOutOfRangeException(nameof(start), start, "De starttegel valt buiten de kaart.");
            }

            if (basePrice < 0 || populationPerTile < 0)
            {
                throw new ArgumentOutOfRangeException(basePrice < 0 ? nameof(basePrice) : nameof(populationPerTile), "Kan niet negatief zijn.");
            }

            Dimensions = dimensions;
            _unlocked = new bool[dimensions.TileCount];
            BasePrice = basePrice;
            PopulationPerTile = populationPerTile;
            _unlocked[Index(start)] = true;
            UnlockedCount = 1;
        }

        public WorldDimensions Dimensions { get; }

        public long BasePrice { get; }

        public int PopulationPerTile { get; }

        public int UnlockedCount { get; private set; }

        /// <summary>Wordt aangeroepen met de tegel die net is ontgrendeld.</summary>
        public event Action<TileCoord> Unlocked;

        /// <summary>Prijs van de volgende tegel: stijgt lineair met het aantal ontgrendelde tegels.</summary>
        public long NextPrice => BasePrice * UnlockedCount;

        /// <summary>Bevolking die nodig is voor de volgende gratis tegel.</summary>
        public long NextPopulation => (long)PopulationPerTile * UnlockedCount;

        public bool IsUnlocked(TileCoord tile) => Dimensions.Contains(tile) && _unlocked[Index(tile)];

        public bool IsUnlocked(CellCoord cell) => IsUnlocked(Dimensions.CellToTile(cell));

        /// <summary>Waarom er op deze cel niet gebouwd mag worden, of <c>null</c>.</summary>
        public string BuildReason(CellCoord cell)
        {
            if (!Dimensions.Contains(cell))
            {
                return OutsideMap;
            }

            return IsUnlocked(cell) ? null : Locked;
        }

        /// <summary>Waarom deze tegel nu niet ontgrendeld kan worden, of <c>null</c>.</summary>
        public string UnlockReason(TileCoord tile)
        {
            if (!Dimensions.Contains(tile))
            {
                return OutsideMap;
            }

            if (_unlocked[Index(tile)])
            {
                return AlreadyUnlocked;
            }

            return HasUnlockedNeighbour(tile) ? null : NotAdjacent;
        }

        /// <summary>Koopt de tegel met geld uit <paramref name="ledger"/>; <c>false</c> bij een ongeldige tegel of te weinig saldo.</summary>
        public bool TryBuy(TileCoord tile, Ledger ledger, int week)
        {
            if (ledger == null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            if (UnlockReason(tile) != null || !ledger.TrySpend(week, LedgerCategory.Other, NextPrice))
            {
                return false;
            }

            Unlock(tile);
            return true;
        }

        /// <summary>Ontgrendelt de tegel gratis als <paramref name="population"/> de drempel haalt.</summary>
        public bool TryUnlockByPopulation(TileCoord tile, long population)
        {
            if (UnlockReason(tile) != null || population < NextPopulation)
            {
                return false;
            }

            Unlock(tile);
            return true;
        }

        /// <summary>Ontgrendelt alle tegels (sandbox, E22-04); <see cref="Unlocked"/> volgt voor elke tegel die nog dicht zat.</summary>
        public void UnlockAll()
        {
            for (var z = 0; z < Dimensions.TilesZ; z++)
            {
                for (var x = 0; x < Dimensions.TilesX; x++)
                {
                    var tile = new TileCoord(x, z);
                    if (!_unlocked[Index(tile)])
                    {
                        Unlock(tile);
                    }
                }
            }
        }

        /// <summary>Zet een tegel zonder voorwaarden (laden); geen controle op buren.</summary>
        internal void Set(TileCoord tile, bool unlocked)
        {
            var index = Index(tile);
            if (_unlocked[index] == unlocked)
            {
                return;
            }

            _unlocked[index] = unlocked;
            UnlockedCount += unlocked ? 1 : -1;
        }

        private void Unlock(TileCoord tile)
        {
            Set(tile, true);
            Unlocked?.Invoke(tile);
        }

        private bool HasUnlockedNeighbour(TileCoord tile) =>
            IsUnlocked(new TileCoord(tile.X - 1, tile.Z)) || IsUnlocked(new TileCoord(tile.X + 1, tile.Z)) ||
            IsUnlocked(new TileCoord(tile.X, tile.Z - 1)) || IsUnlocked(new TileCoord(tile.X, tile.Z + 1));

        private int Index(TileCoord tile) => (tile.Z * Dimensions.TilesX) + tile.X;
    }
}
