using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;

namespace Cities.World.Districts
{
    /// <summary>Sectie <c>DIST</c>: de districten (nummer, naam, kleur) en welk blok bij welk district hoort.</summary>
    public sealed class DistrictSection : ISaveSection
    {
        public const string SectionId = "DIST";

        private readonly DistrictMap _map;

        public DistrictSection(DistrictMap map)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            var districts = _map.Book.Districts.ToArray();
            writer.Write(districts.Length);
            foreach (var d in districts)
            {
                writer.Write(d.Id);
                writer.Write(d.Name);
                writer.Write(d.Rgb);
            }

            writer.WriteArray(_map.Blocks);
        }

        public Action Read(SaveReader reader, int version)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > DistrictBook.MaxDistricts)
            {
                throw reader.Corrupt($"onmogelijk aantal districten {count}");
            }

            var districts = new List<DistrictInfo>(count);
            for (var i = 0; i < count; i++)
            {
                districts.Add(new DistrictInfo(reader.ReadInt32(), reader.ReadString(), reader.ReadInt32()));
            }

            var blocks = new byte[_map.Blocks.Length];
            reader.ReadArray<byte>(blocks);
            reader.ExpectEnd();
            try
            {
                DistrictBook.Check(districts);
            }
            catch (ArgumentException e)
            {
                throw reader.Corrupt(e.Message);
            }

            var known = new HashSet<int>(districts.Select(d => d.Id));
            if (!_map.IsValid(blocks, known.Contains))
            {
                throw reader.Corrupt("blokken verwijzen naar een onbekend district");
            }

            return () =>
            {
                _map.Book.Restore(districts);
                _map.Restore(blocks);
            };
        }
    }
}
