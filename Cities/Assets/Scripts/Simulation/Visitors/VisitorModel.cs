using System;

namespace Cities.Simulation.Visitors
{
    /// <summary>Hoe bezoekers de stad bereiken (de buitenverbindingen van E14-04).</summary>
    public enum VisitorChannel
    {
        Road = 0,
        Rail = 1,
        Air = 2,
        Sea = 3,
    }

    /// <summary>
    /// De kleine afstemming met de buitenverbindingen (E14-04): hoeveel bezoekers per week er via een kanaal kunnen aankomen.
    /// Een snelweg-, trein-, vlieg- of havenverbinding implementeert dit; zonder verbinding is de capaciteit nul.
    /// </summary>
    public interface IOutsideConnections
    {
        int WeeklyCapacity(VisitorChannel channel);
    }

    /// <summary>Vaste capaciteiten per kanaal; handig als eenvoudige implementatie en in tests.</summary>
    public sealed class FixedOutsideConnections : IOutsideConnections
    {
        private readonly int[] _capacity = new int[VisitorProfiles.ChannelCount];

        public FixedOutsideConnections Set(VisitorChannel channel, int weeklyCapacity)
        {
            _capacity[(int)channel] = Math.Max(0, weeklyCapacity);
            return this;
        }

        public int WeeklyCapacity(VisitorChannel channel) => _capacity[(int)channel];
    }

    /// <summary>Gedrag van bezoekers per kanaal: het aandeel dagjesmensen (geen hotelbed nodig).</summary>
    public static class VisitorProfiles
    {
        public const int ChannelCount = 4;

        /// <summary>Percentage van de aankomsten dat dezelfde dag weer vertrekt.</summary>
        public static int DayTripPercent(VisitorChannel channel)
        {
            switch (channel)
            {
                case VisitorChannel.Road:
                    return 40;
                case VisitorChannel.Rail:
                    return 30;
                case VisitorChannel.Sea:
                    return 10;
                default:
                    return 5;
            }
        }
    }

    /// <summary>Een hotel (E14-07): bedden, sterren en nachtprijs. Meer sterren trekt meer besteding per gast.</summary>
    public sealed class VisitorHotel
    {
        public const int MaxStars = 5;

        public VisitorHotel(int id, int beds, int stars, long nightlyRate)
        {
            if (beds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(beds), "Een hotel heeft minstens één bed.");
            }

            if (stars < 1 || stars > MaxStars)
            {
                throw new ArgumentOutOfRangeException(nameof(stars), "Sterren liggen tussen 1 en 5.");
            }

            if (nightlyRate < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nightlyRate), "Een nachtprijs kan niet negatief zijn.");
            }

            Id = id;
            Beds = beds;
            Stars = stars;
            NightlyRate = nightlyRate;
        }

        public int Id { get; }

        public int Beds { get; }

        public int Stars { get; }

        public long NightlyRate { get; }

        /// <summary>Wat een gast per nacht buiten het hotel uitgeeft (eten, winkels).</summary>
        public long SpendPerNight => 30 + (10L * Stars);
    }

    /// <summary>Een attractie die bezoekers trekt (E14-07): aantrekkingskracht, dagcapaciteit en entree.</summary>
    public sealed class VisitorSite
    {
        public VisitorSite(int id, string name, int appeal, int capacityPerDay, long ticketPrice, long upkeepPerWeek)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Een attractie heeft een naam nodig.", nameof(name));
            }

            if (appeal <= 0 || capacityPerDay <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(appeal), "Aantrekkingskracht en capaciteit zijn positief.");
            }

            if (ticketPrice < 0 || upkeepPerWeek < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ticketPrice), "Entree en onderhoud kunnen niet negatief zijn.");
            }

            Id = id;
            Name = name;
            Appeal = appeal;
            CapacityPerDay = capacityPerDay;
            TicketPrice = ticketPrice;
            UpkeepPerWeek = upkeepPerWeek;
        }

        public int Id { get; }

        public string Name { get; }

        /// <summary>Bezoekers per dag die deze attractie op de volle reputatie wil trekken.</summary>
        public int Appeal { get; }

        public int CapacityPerDay { get; }

        public long TicketPrice { get; }

        public long UpkeepPerWeek { get; }
    }

    /// <summary>Uitkomst van één week bezoekers.</summary>
    public sealed class VisitorWeek
    {
        /// <summary>Gewenste aankomsten voor de beperking door de buitenverbindingen.</summary>
        public long Demand { get; internal set; }

        public long Arrivals { get; internal set; }

        public long DayTrippers { get; internal set; }

        /// <summary>Gasten die een hotelbed kregen.</summary>
        public long HotelGuests { get; internal set; }

        /// <summary>Gasten die geen bed vonden en vertrokken.</summary>
        public long TurnedAway { get; internal set; }

        public long NightsSold { get; internal set; }

        /// <summary>Bezoeken die de gasten aan attracties wilden brengen.</summary>
        public long VisitsWanted { get; internal set; }

        public long SiteVisits { get; internal set; }

        /// <summary>Hotelomzet (privé, niet voor de stad).</summary>
        public long HotelRevenue { get; internal set; }

        /// <summary>Uitgaven in de stad buiten het hotel.</summary>
        public long Spending { get; internal set; }

        public long Tickets { get; internal set; }

        public long Taxes { get; internal set; }

        public long Upkeep { get; internal set; }

        public int Reputation { get; internal set; }
    }
}
