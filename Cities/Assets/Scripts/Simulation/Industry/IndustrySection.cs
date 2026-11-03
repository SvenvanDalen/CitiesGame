using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;

namespace Cities.Simulation.Industry
{
    /// <summary>Sectie <c>INDU</c>: de winningen met hun voorraad (E14-02).</summary>
    public sealed class IndustrySection : ISaveSection
    {
        public const string SectionId = "INDU";

        private readonly IndustryNetwork _network;

        public IndustrySection(IndustryNetwork network)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            var sites = _network.Sites.ToArray();
            writer.Write(sites.Length);
            foreach (var s in sites)
            {
                writer.Write(s.Id);
                writer.Write((int)s.Kind);
                writer.Write(s.Level);
                writer.Write(s.Richness);
                writer.Write(s.Stock);
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > IndustryNetwork.MaxSites)
            {
                throw reader.Corrupt($"onmogelijk aantal winningen: {count}");
            }

            var sites = new List<(ExtractionSite Site, float Stock)>();
            var ids = new HashSet<int>();
            for (var i = 0; i < count; i++)
            {
                var entry = ReadSite(reader);
                if (!ids.Add(entry.Site.Id))
                {
                    throw reader.Corrupt("een winning staat twee keer in de opslag: " + entry.Site.Id);
                }

                sites.Add(entry);
            }

            reader.ExpectEnd();
            return () =>
            {
                foreach (var entry in sites)
                {
                    entry.Site.Restore(entry.Stock);
                }

                _network.Replace(sites.Select(s => s.Site));
            };
        }

        private static (ExtractionSite Site, float Stock) ReadSite(SaveReader reader)
        {
            var id = reader.ReadInt32();
            var kind = reader.ReadInt32();
            var level = reader.ReadInt32();
            var richness = reader.ReadSingle();
            var stock = reader.ReadSingle();
            if (kind < 0 || kind > (int)IndustryKind.Oil)
            {
                throw reader.Corrupt("onbekend industrietype: " + kind);
            }

            if (level < 1 || level > ExtractionSite.MaxLevel || !(richness >= 0f && richness <= 1f) || !(stock >= 0f))
            {
                throw reader.Corrupt("ongeldige winning " + id);
            }

            return (new ExtractionSite(id, (IndustryKind)kind, level, richness), stock);
        }
    }
}
