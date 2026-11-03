using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;

namespace Cities.Simulation.TouristSites
{
    /// <summary>Sectie <c>TRST</c>: de toeristische gebouwen (E14-08).</summary>
    public sealed class TouristSitesSection : ISaveSection
    {
        public const string SectionId = "TRST";

        private readonly TouristSiteRegistry _registry;

        public TouristSitesSection(TouristSiteRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            writer.Write(_registry.NextId);
            var sites = _registry.Sites.ToArray();
            writer.Write(sites.Length);
            foreach (var site in sites)
            {
                writer.Write(site.Id);
                writer.Write(site.DefinitionId);
                writer.Write((int)site.Profile.Kind);
                writer.Write(site.Profile.Attractiveness);
                writer.Write(site.Profile.Beds);
                writer.Write(site.Profile.ReachMeters);
                writer.Write(site.X);
                writer.Write(site.Y);
                writer.Write(site.Conditions.Pollution);
                writer.Write(site.Conditions.Noise);
                writer.Write(site.Conditions.Scenery);
                writer.Write(site.Conditions.Sunshine);
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var nextId = reader.ReadInt32();
            var count = reader.ReadInt32();
            if (count < 0 || count > TouristSiteRegistry.MaxSites)
            {
                throw reader.Corrupt($"onmogelijk aantal toeristische gebouwen: {count}");
            }

            var sites = new List<TouristSite>(count);
            for (var i = 0; i < count; i++)
            {
                sites.Add(ReadSite(reader));
            }

            reader.ExpectEnd();
            if (sites.Any(s => s.Id >= nextId) || sites.Select(s => s.Id).Distinct().Count() != count)
            {
                throw reader.Corrupt("volgnummer te laag of dubbel ID");
            }

            return () => _registry.Restore(sites, nextId);
        }

        private static TouristSite ReadSite(SaveReader reader)
        {
            var id = reader.ReadInt32();
            var definitionId = reader.ReadString();
            var kind = (TouristSiteKind)reader.ReadInt32();
            var attractiveness = reader.ReadSingle();
            var beds = reader.ReadInt32();
            var reach = reader.ReadSingle();
            var x = reader.ReadInt32();
            var y = reader.ReadInt32();
            var pollution = reader.ReadSingle();
            var noise = reader.ReadSingle();
            var scenery = reader.ReadSingle();
            var sunshine = reader.ReadSingle();
            try
            {
                return new TouristSite(
                    id,
                    definitionId,
                    new TouristSiteProfile(kind, attractiveness, beds, reach),
                    x,
                    y,
                    new TouristSiteConditions(pollution, noise, scenery, sunshine));
            }
            catch (ArgumentException e)
            {
                throw reader.Corrupt(e.Message);
            }
        }
    }
}
