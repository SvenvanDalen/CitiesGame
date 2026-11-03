using System;
using Cities.Core.Persistence;
using Cities.World.Grid;

namespace Cities.World.Tiles
{
    /// <summary>Sectie <c>TILE</c>: welke tegels ontgrendeld zijn. Een save met een andere kaartgrootte wordt geweigerd.</summary>
    public sealed class TileUnlockSection : ISaveSection
    {
        public const string SectionId = "TILE";

        private readonly TileUnlocks _unlocks;

        public TileUnlockSection(TileUnlocks unlocks) => _unlocks = unlocks ?? throw new ArgumentNullException(nameof(unlocks));

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            var dimensions = _unlocks.Dimensions;
            writer.Write(dimensions.TilesX);
            writer.Write(dimensions.TilesZ);
            for (var z = 0; z < dimensions.TilesZ; z++)
            {
                for (var x = 0; x < dimensions.TilesX; x++)
                {
                    writer.Write(_unlocks.IsUnlocked(new TileCoord(x, z)));
                }
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var dimensions = _unlocks.Dimensions;
            var tilesX = reader.ReadInt32();
            var tilesZ = reader.ReadInt32();
            if (tilesX != dimensions.TilesX || tilesZ != dimensions.TilesZ)
            {
                throw reader.Corrupt($"{tilesX}×{tilesZ} tegels, deze scene heeft {dimensions.TilesX}×{dimensions.TilesZ}");
            }

            var flags = new bool[dimensions.TileCount];
            var any = false;
            for (var i = 0; i < flags.Length; i++)
            {
                flags[i] = reader.ReadBool();
                any |= flags[i];
            }

            reader.ExpectEnd();
            if (!any)
            {
                throw reader.Corrupt("geen enkele tegel ontgrendeld");
            }

            return () =>
            {
                for (var i = 0; i < flags.Length; i++)
                {
                    _unlocks.Set(new TileCoord(i % dimensions.TilesX, i / dimensions.TilesX), flags[i]);
                }
            };
        }
    }
}
