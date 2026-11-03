using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;

namespace Cities.Simulation.Visitors
{
    /// <summary>Sectie <c>VIST</c>: hotels, attracties en reputatie van het toerisme (E14-07).</summary>
    public sealed class VisitorSection : ISaveSection
    {
        public const string SectionId = "VIST";

        private readonly VisitorEconomy _economy;

        public VisitorSection(VisitorEconomy economy)
        {
            _economy = economy ?? throw new ArgumentNullException(nameof(economy));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            writer.Write(_economy.Reputation);
            writer.Write(_economy.NextHotelId);
            var hotels = _economy.Hotels.ToArray();
            writer.Write(hotels.Length);
            foreach (var h in hotels)
            {
                writer.Write(h.Id);
                writer.Write(h.Beds);
                writer.Write(h.Stars);
                writer.Write(h.NightlyRate);
            }

            writer.Write(_economy.NextSiteId);
            var sites = _economy.Sites.ToArray();
            writer.Write(sites.Length);
            foreach (var s in sites)
            {
                writer.Write(s.Id);
                writer.Write(s.Name);
                writer.Write(s.Appeal);
                writer.Write(s.CapacityPerDay);
                writer.Write(s.TicketPrice);
                writer.Write(s.UpkeepPerWeek);
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var reputation = reader.ReadInt32();
            if (reputation < 0 || reputation > 100)
            {
                throw reader.Corrupt($"reputatie buiten bereik: {reputation}");
            }

            var nextHotelId = reader.ReadInt32();
            var hotels = new List<VisitorHotel>();
            var hotelCount = ReadCount(reader, "hotels");
            for (var i = 0; i < hotelCount; i++)
            {
                hotels.Add(ReadHotel(reader));
            }

            var nextSiteId = reader.ReadInt32();
            var sites = new List<VisitorSite>();
            var siteCount = ReadCount(reader, "attracties");
            for (var i = 0; i < siteCount; i++)
            {
                sites.Add(ReadSite(reader));
            }

            reader.ExpectEnd();
            if (hotels.Any(h => h.Id >= nextHotelId) || hotels.Select(h => h.Id).Distinct().Count() != hotelCount)
            {
                throw reader.Corrupt("hotelvolgnummer te laag of dubbel ID");
            }

            if (sites.Any(s => s.Id >= nextSiteId) || sites.Select(s => s.Id).Distinct().Count() != siteCount)
            {
                throw reader.Corrupt("attractievolgnummer te laag of dubbel ID");
            }

            return () => _economy.Restore(hotels, sites, nextHotelId, nextSiteId, reputation);
        }

        private static VisitorHotel ReadHotel(SaveReader reader)
        {
            var id = reader.ReadInt32();
            var beds = reader.ReadInt32();
            var stars = reader.ReadInt32();
            var rate = reader.ReadInt64();
            try
            {
                return new VisitorHotel(id, beds, stars, rate);
            }
            catch (ArgumentException e)
            {
                throw reader.Corrupt(e.Message);
            }
        }

        private static VisitorSite ReadSite(SaveReader reader)
        {
            var id = reader.ReadInt32();
            var name = reader.ReadString();
            var appeal = reader.ReadInt32();
            var capacity = reader.ReadInt32();
            var ticket = reader.ReadInt64();
            var upkeep = reader.ReadInt64();
            try
            {
                return new VisitorSite(id, name, appeal, capacity, ticket, upkeep);
            }
            catch (ArgumentException e)
            {
                throw reader.Corrupt(e.Message);
            }
        }

        private static int ReadCount(SaveReader reader, string what)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > VisitorEconomy.MaxItems)
            {
                throw reader.Corrupt($"onmogelijk aantal {what}: {count}");
            }

            return count;
        }
    }
}
