using System;
using System.Linq;
using Cities.World.Grid;
using Unity.Mathematics;

namespace Cities.World.Districts
{
    /// <summary>
    /// Welk district waar ligt (E13-01): een byte per blok van <see cref="BlockCells"/>×<see cref="BlockCells"/> cellen,
    /// 0 voor geen district. Samen met het <see cref="DistrictBook"/> met namen en kleuren. Een blok hoort bij hoogstens
    /// één district; opnieuw schilderen vervangt het vorige.
    /// </summary>
    public sealed class DistrictMap
    {
        /// <summary>Cellen per blokzijde; bij 8 m cellen is een blok 32 m.</summary>
        public const int BlockCells = 4;

        private readonly byte[] _blocks;
        private readonly int[] _counts = new int[DistrictBook.MaxDistricts + 1];

        public DistrictMap(WorldDimensions dimensions, DistrictBook book = null)
        {
            Dimensions = dimensions;
            Book = book ?? new DistrictBook();
            BlocksX = (dimensions.CellsX + BlockCells - 1) / BlockCells;
            BlocksZ = (dimensions.CellsZ + BlockCells - 1) / BlockCells;
            _blocks = new byte[BlocksX * BlocksZ];
            _counts[0] = _blocks.Length;
        }

        public WorldDimensions Dimensions { get; }

        public DistrictBook Book { get; }

        public int BlocksX { get; }

        public int BlocksZ { get; }

        /// <summary>Zijde van een blok in meters.</summary>
        public float BlockSize => BlockCells * Dimensions.CellSize;

        /// <summary>Loopt op als de verdeling over de kaart verandert.</summary>
        public long Revision { get; private set; }

        public ReadOnlySpan<byte> Blocks => _blocks;

        /// <summary>Het district van een blok, of 0.</summary>
        public int At(int blockX, int blockZ) => _blocks[(blockZ * BlocksX) + blockX];

        /// <summary>Het district op een wereldpositie; buiten de kaart is 0.</summary>
        public int AtPosition(float x, float z)
        {
            var bx = (int)math.floor(x / BlockSize);
            var bz = (int)math.floor(z / BlockSize);
            return bx < 0 || bz < 0 || bx >= BlocksX || bz >= BlocksZ ? 0 : At(bx, bz);
        }

        /// <summary>Aantal blokken van een district (0 telt de blokken zonder district).</summary>
        public int BlockCount(int id) => id < 0 || id > DistrictBook.MaxDistricts ? 0 : _counts[id];

        /// <summary>Oppervlak van een district in vierkante meters.</summary>
        public float Area(int id) => BlockCount(id) * BlockSize * BlockSize;

        /// <summary>
        /// Schildert een cirkel van <paramref name="radius"/> meter rond <paramref name="center"/> met district
        /// <paramref name="id"/> (0 wist). Geeft het aantal gewijzigde blokken.
        /// </summary>
        public int Paint(float2 center, float radius, int id)
        {
            if (id != DistrictBook.WholeCity && !Book.Contains(id))
            {
                throw new ArgumentException($"Onbekend district {id}.", nameof(id));
            }

            if (!(radius > 0f))
            {
                return 0;
            }

            var size = BlockSize;
            var min = math.max((int2)math.floor((center - radius) / size), 0);
            var max = math.min((int2)math.ceil((center + radius) / size), new int2(BlocksX, BlocksZ));
            var changed = 0;
            for (var z = min.y; z < max.y; z++)
            {
                for (var x = min.x; x < max.x; x++)
                {
                    var blockCenter = (new float2(x, z) + 0.5f) * size;
                    if (math.distance(blockCenter, center) <= radius && Set(x, z, (byte)id))
                    {
                        changed++;
                    }
                }
            }

            if (changed > 0)
            {
                Revision++;
            }

            return changed;
        }

        /// <summary>Haalt een district uit de lijst en maakt zijn blokken weer 'geen district'.</summary>
        public bool RemoveDistrict(int id)
        {
            if (!Book.Remove(id))
            {
                return false;
            }

            for (var i = 0; i < _blocks.Length; i++)
            {
                if (_blocks[i] == id)
                {
                    _blocks[i] = 0;
                }
            }

            _counts[0] += _counts[id];
            _counts[id] = 0;
            Revision++;
            return true;
        }

        /// <summary>Controleert opgeslagen blokken: juiste lengte en alleen districten waarvoor <paramref name="known"/> waar geeft.</summary>
        public bool IsValid(byte[] blocks, Func<int, bool> known)
        {
            return blocks != null && blocks.Length == _blocks.Length && blocks.All(b => b == 0 || known(b));
        }

        /// <summary>Vervangt alle blokken; roep eerst <see cref="IsValid"/> aan.</summary>
        public void Restore(byte[] blocks)
        {
            Array.Copy(blocks, _blocks, _blocks.Length);
            Array.Clear(_counts, 0, _counts.Length);
            foreach (var b in _blocks)
            {
                _counts[b]++;
            }

            Revision++;
        }

        private bool Set(int x, int z, byte id)
        {
            var index = (z * BlocksX) + x;
            var old = _blocks[index];
            if (old == id)
            {
                return false;
            }

            _counts[old]--;
            _counts[id]++;
            _blocks[index] = id;
            return true;
        }
    }
}
