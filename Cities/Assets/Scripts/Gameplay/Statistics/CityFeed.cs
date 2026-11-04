using System;
using System.Collections.Generic;
using Cities.Core.Randomness;
using Unity.Mathematics;

namespace Cities.Gameplay.Statistics
{
    public enum Severity : byte
    {
        Info,
        Advice,
        Warning,
        Critical,
    }

    public readonly struct Notification
    {
        public Notification(int id, string key, Severity severity, string text, float2? location, double time)
        {
            Id = id;
            Key = key;
            Severity = severity;
            Text = text;
            Location = location;
            Time = time;
        }

        public int Id { get; }

        /// <summary>Sleutel van het soort probleem; gelijke sleutels worden samengevoegd.</summary>
        public string Key { get; }

        public Severity Severity { get; }

        public string Text { get; }

        /// <summary>Plek van het probleem op de kaart; de camera springt hierheen. Leeg bij een stadsbrede melding.</summary>
        public float2? Location { get; }

        public double Time { get; }
    }

    /// <summary>
    /// Meldingen (E17-09): stadsmeldingen en hulpberichten met een link naar de probleemplek. Dezelfde melding (zelfde sleutel) binnen
    /// <see cref="Cooldown"/> seconden wordt niet opnieuw geplaatst, zodat een aanhoudend probleem de lijst niet volspamt. Bij een vol overzicht
    /// verdwijnt de minst belangrijke en oudste melding.
    /// </summary>
    public sealed class NotificationCenter
    {
        public const int Capacity = 100;

        private readonly List<Notification> _items = new List<Notification>();
        private readonly Dictionary<string, double> _lastPosted = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly HashSet<int> _read = new HashSet<int>();
        private int _nextId = 1;

        public NotificationCenter(double cooldownSeconds = 600.0)
        {
            if (!(cooldownSeconds >= 0.0))
            {
                throw new ArgumentOutOfRangeException(nameof(cooldownSeconds), cooldownSeconds, "De wachttijd is nul of meer.");
            }

            Cooldown = cooldownSeconds;
        }

        public double Cooldown { get; }

        public IReadOnlyList<Notification> Items => _items;

        public int UnreadCount => _items.Count - _read.Count;

        /// <summary>Plaatst een melding; geeft het id, of -1 als dezelfde melding net al was geplaatst.</summary>
        public int Post(string key, Severity severity, string text, double now, float2? location = null)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Een melding heeft een sleutel en een tekst.", nameof(key));
            }

            if (_lastPosted.TryGetValue(key, out var last) && now - last < Cooldown)
            {
                return -1;
            }

            _lastPosted[key] = now;
            if (_items.Count >= Capacity)
            {
                DropLeastImportant();
            }

            var notification = new Notification(_nextId++, key, severity, text, location, now);
            _items.Add(notification);
            return notification.Id;
        }

        public bool TryGet(int id, out Notification notification)
        {
            foreach (var item in _items)
            {
                if (item.Id == id)
                {
                    notification = item;
                    return true;
                }
            }

            notification = default;
            return false;
        }

        /// <summary>Opent een melding: markeert hem als gelezen en geeft de plek waar de camera naartoe moet (leeg bij stadsbreed).</summary>
        public float2? Open(int id)
        {
            if (!TryGet(id, out var notification))
            {
                throw new ArgumentException($"Melding {id} bestaat niet.", nameof(id));
            }

            _read.Add(id);
            return notification.Location;
        }

        public bool Dismiss(int id)
        {
            var index = _items.FindIndex(n => n.Id == id);
            if (index < 0)
            {
                return false;
            }

            _items.RemoveAt(index);
            _read.Remove(id);
            return true;
        }

        public void Clear()
        {
            _items.Clear();
            _read.Clear();
        }

        /// <summary>Meldingen van ernstig naar licht, nieuwste eerst binnen dezelfde ernst.</summary>
        public List<Notification> ByImportance()
        {
            var sorted = new List<Notification>(_items);
            sorted.Sort((a, b) =>
            {
                var compare = b.Severity.CompareTo(a.Severity);
                return compare != 0 ? compare : b.Id.CompareTo(a.Id);
            });
            return sorted;
        }

        private void DropLeastImportant()
        {
            var worst = 0;
            for (var i = 1; i < _items.Count; i++)
            {
                if (_items[i].Severity < _items[worst].Severity)
                {
                    worst = i;
                }
            }

            _read.Remove(_items[worst].Id);
            _items.RemoveAt(worst);
        }
    }

    /// <summary>Wat de adviseurs van de stad weten: gemiddelden, tekorten en de plekken waar het het slechtst gaat.</summary>
    public readonly struct AdvisorInput
    {
        public AdvisorInput(float unemployment, float powerCoverage, float waterCoverage, double balance, float congestion, float2? worstTraffic)
        {
            Unemployment = unemployment;
            PowerCoverage = powerCoverage;
            WaterCoverage = waterCoverage;
            Balance = balance;
            Congestion = congestion;
            WorstTraffic = worstTraffic;
        }

        /// <summary>Werkloosheid, 0 tot 1.</summary>
        public float Unemployment { get; }

        /// <summary>Hoeveel van de vraag naar stroom gedekt is, 0 tot 1 of meer.</summary>
        public float PowerCoverage { get; }

        public float WaterCoverage { get; }

        /// <summary>Saldo van de stadskas.</summary>
        public double Balance { get; }

        /// <summary>Gemiddelde drukte op de wegen, 0 tot 1 of meer (1 is vol).</summary>
        public float Congestion { get; }

        public float2? WorstTraffic { get; }
    }

    /// <summary>
    /// Adviseurs (E17-09): kijken naar de toestand van de stad en plaatsen meldingen met een advies. Drempels zijn vast, zodat het gedrag
    /// voorspelbaar is; de wachttijd van <see cref="NotificationCenter"/> voorkomt herhaling.
    /// </summary>
    public static class Advisors
    {
        public const float HighUnemployment = 0.12f;
        public const float HighCongestion = 0.9f;

        public static int Evaluate(NotificationCenter center, AdvisorInput input, double now)
        {
            if (center is null)
            {
                throw new ArgumentNullException(nameof(center));
            }

            var posted = 0;
            posted += Count(input.PowerCoverage < 1f, () => center.Post("power", input.PowerCoverage < 0.7f ? Severity.Critical : Severity.Warning, "Stroomtekort: de centrales leveren te weinig. Bouw extra opwekking.", now));
            posted += Count(input.WaterCoverage < 1f, () => center.Post("water", input.WaterCoverage < 0.7f ? Severity.Critical : Severity.Warning, "Watertekort: breid de waterlevering uit.", now));
            posted += Count(input.Balance < 0.0, () => center.Post("deficit", Severity.Warning, "De stadskas staat in het rood. Verhoog belastingen of verlaag uitgaven.", now));
            posted += Count(input.Unemployment > HighUnemployment, () => center.Post("jobs", Severity.Advice, "Hoge werkloosheid: zone meer bedrijventerrein of kantoren.", now));
            posted += Count(input.Congestion > HighCongestion, () => center.Post("traffic", Severity.Warning, "De wegen zitten vol. Overweeg extra wegen of openbaar vervoer.", now, input.WorstTraffic));
            return posted;
        }

        private static int Count(bool condition, Func<int> post) => condition && post() >= 0 ? 1 : 0;
    }

    public enum CityEvent : byte
    {
        Fire,
        Flood,
        NewPark,
        TrafficJam,
        Concert,
        TaxRise,
        PowerOutage,
        Milestone,
    }

    public readonly struct CitizenMessage
    {
        public CitizenMessage(string handle, string text, double time)
        {
            Handle = handle;
            Text = text;
            Time = time;
        }

        public string Handle { get; }

        public string Text { get; }

        public double Time { get; }
    }

    /// <summary>
    /// Stadsberichten (E17-10): een optionele stroom korte berichten van burgers over gebeurtenissen. De tekst en afzender komen deterministisch
    /// uit de seed en de gebeurtenis; een snelheidsbegrenzer houdt de stroom rustig. Uitzetten (<see cref="Enabled"/>) laat alles weg.
    /// </summary>
    public sealed class CityMessages
    {
        public const int Capacity = 50;

        private static readonly string[] Handles = { "buurtbewoner", "fietser010", "stadsfan", "oudegaste", "forens_piet", "marktkraam", "nachtbraker", "kritische_klaas" };

        private static readonly string[][] Templates =
        {
            new[] { "Brand in de buurt! Hopelijk is iedereen veilig.", "Sirenes overal, wat gebeurt daar?", "De brandweer was er snel bij, goed werk." },
            new[] { "Het water stijgt, de straat staat blank.", "Wie heeft er zandzakken over?", "Dit moet echt beter beschermd worden." },
            new[] { "Eindelijk een nieuw park, heerlijk!", "Mooi groen erbij, de kinderen zijn dolblij.", "Het park is prachtig, bedankt." },
            new[] { "Weer file, ik sta al een uur stil.", "Wie bedenkt deze wegen eigenlijk?", "Neem de tram, dat gaat sneller." },
            new[] { "Wat een geweldig concert gisteren!", "Het was druk maar zo gezellig.", "Komt er nog een tweede avond?" },
            new[] { "Belasting omhoog? Echt niet nodig.", "Als de voorzieningen beter worden, vind ik het best.", "Mijn portemonnee voelt dit." },
            new[] { "Geen stroom, alles is donker.", "Kaarsen gevonden, maar dit duurt te lang.", "Wanneer komt het licht terug?" },
            new[] { "Onze stad groeit, wat trots!", "Mooi dat we weer een stap verder zijn.", "Op naar de volgende mijlpaal." },
        };

        private readonly List<CitizenMessage> _messages = new List<CitizenMessage>();
        private readonly ulong _seed;
        private readonly double _minInterval;
        private double _lastTime = double.NegativeInfinity;
        private int _count;

        public CityMessages(ulong seed, double minIntervalSeconds = 20.0)
        {
            if (!(minIntervalSeconds >= 0.0))
            {
                throw new ArgumentOutOfRangeException(nameof(minIntervalSeconds), minIntervalSeconds, "Het interval is nul of meer.");
            }

            _seed = seed;
            _minInterval = minIntervalSeconds;
        }

        public bool Enabled { get; set; } = true;

        public IReadOnlyList<CitizenMessage> Messages => _messages;

        /// <summary>Reageert op een gebeurtenis met een bericht; geeft false als de stroom uit staat of te snel zou gaan.</summary>
        public bool React(CityEvent cityEvent, double now, out CitizenMessage message)
        {
            message = default;
            var kind = (int)cityEvent;
            if (!Enabled || kind < 0 || kind >= Templates.Length || now - _lastTime < _minInterval)
            {
                return false;
            }

            var random = new DeterministicRandom(SeedHash.Combine(_seed, (ulong)kind, (ulong)_count));
            var options = Templates[kind];
            message = new CitizenMessage(Handles[random.NextInt(0, Handles.Length)], options[random.NextInt(0, options.Length)], now);
            _lastTime = now;
            _count++;
            if (_messages.Count >= Capacity)
            {
                _messages.RemoveAt(0);
            }

            _messages.Add(message);
            return true;
        }
    }

    public enum FlowMode : byte
    {
        Traffic,
        Transit,
    }

    /// <summary>De weergave van de stroom over één segment (E17-11).</summary>
    public readonly struct FlowStyle
    {
        public FlowStyle(float load, float3 color, float speed, int dots)
        {
            Load = load;
            Color = color;
            Speed = speed;
            Dots = dots;
        }

        /// <summary>Bezetting van het segment, 0 (leeg) tot 1 (vol, of meer).</summary>
        public float Load { get; }

        public float3 Color { get; }

        /// <summary>Snelheid van de stipjes in meter per seconde.</summary>
        public float Speed { get; }

        public int Dots { get; }
    }

    /// <summary>
    /// Flow-visualisatie (E17-11): animatie van verkeers- en ov-stromen op segmenten. Verkeer kleurt van groen via geel naar rood met de
    /// bezetting en gaat trager bij drukte; ov heeft één blauwe tint. Het aantal stipjes volgt de stroom en de stipjes schuiven langs het
    /// segment (<see cref="Offsets"/>), zodat een vol segment er langzaam en dicht uitziet.
    /// </summary>
    public static class FlowVisualizer
    {
        public const float DotSpacing = 8f;
        public const int MaxDots = 64;

        private static readonly float3 Green = new float3(0.2f, 0.8f, 0.3f);
        private static readonly float3 Yellow = new float3(0.95f, 0.85f, 0.2f);
        private static readonly float3 Red = new float3(0.9f, 0.2f, 0.15f);
        private static readonly float3 Blue = new float3(0.25f, 0.55f, 0.95f);

        public static FlowStyle For(float flowPerHour, float capacityPerHour, float segmentLength, float freeSpeed, FlowMode mode)
        {
            if (!(capacityPerHour > 0f) || !(segmentLength > 0f) || !(freeSpeed > 0f) || flowPerHour < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(capacityPerHour), "Capaciteit, lengte en snelheid zijn positief; de stroom niet negatief.");
            }

            var load = flowPerHour / capacityPerHour;
            var clamped = math.saturate(load);
            var color = mode == FlowMode.Transit ? Blue : clamped < 0.5f ? math.lerp(Green, Yellow, clamped * 2f) : math.lerp(Yellow, Red, (clamped - 0.5f) * 2f);
            var speed = mode == FlowMode.Transit ? freeSpeed : freeSpeed * math.lerp(1f, 0.15f, clamped * clamped);
            var dots = flowPerHour <= 0f ? 0 : math.clamp((int)math.ceil(segmentLength / DotSpacing * clamped), 1, MaxDots);
            return new FlowStyle(load, color, speed, dots);
        }

        /// <summary>Plaatsen (0 tot 1 langs het segment) van de stipjes op een tijdstip.</summary>
        public static void Offsets(FlowStyle style, float segmentLength, float time, float[] result)
        {
            if (result is null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (result.Length < style.Dots)
            {
                throw new ArgumentException("De uitvoer is te klein voor het aantal stipjes.", nameof(result));
            }

            var travelled = style.Speed * time / math.max(segmentLength, 0.0001f);
            for (var i = 0; i < style.Dots; i++)
            {
                var value = ((float)i / style.Dots) + travelled;
                result[i] = value - math.floor(value);
            }
        }
    }

    public readonly struct SearchEntry
    {
        public SearchEntry(int id, string name, string type, float2 position)
        {
            Id = id;
            Name = name;
            Type = type;
            Position = position;
        }

        public int Id { get; }

        public string Name { get; }

        public string Type { get; }

        /// <summary>Waar de camera naartoe springt.</summary>
        public float2 Position { get; }
    }

    /// <summary>
    /// Zoeken en lijsten (E18-09): gebouwen zoeken op naam of type en ernaartoe springen. Treffers beginnen bij een naam die met de zoekterm
    /// begint, dan een woord dat ermee begint, dan een naam die hem bevat; binnen een rang op alfabet. Hoofdletters tellen niet mee.
    /// </summary>
    public sealed class SearchIndex
    {
        private readonly Dictionary<int, SearchEntry> _entries = new Dictionary<int, SearchEntry>();

        public int Count => _entries.Count;

        public void Set(SearchEntry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                throw new ArgumentException("Een zoekresultaat heeft een naam.", nameof(entry));
            }

            _entries[entry.Id] = entry;
        }

        public bool Remove(int id) => _entries.Remove(id);

        public List<SearchEntry> Search(string query, string type = null, int max = 20)
        {
            if (max <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(max), max, "Er komt minstens één resultaat terug.");
            }

            var needle = (query ?? string.Empty).Trim();
            var ranked = new List<(int Rank, SearchEntry Entry)>();
            foreach (var entry in _entries.Values)
            {
                if (type != null && !string.Equals(entry.Type, type, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var rank = needle.Length == 0 ? 3 : Rank(entry, needle);
                if (rank >= 0)
                {
                    ranked.Add((rank, entry));
                }
            }

            ranked.Sort((a, b) =>
            {
                var compare = a.Rank.CompareTo(b.Rank);
                if (compare == 0)
                {
                    compare = string.Compare(a.Entry.Name, b.Entry.Name, StringComparison.OrdinalIgnoreCase);
                }

                return compare != 0 ? compare : a.Entry.Id.CompareTo(b.Entry.Id);
            });

            var result = new List<SearchEntry>(Math.Min(max, ranked.Count));
            for (var i = 0; i < ranked.Count && i < max; i++)
            {
                result.Add(ranked[i].Entry);
            }

            return result;
        }

        /// <summary>Alle soorten in de index, alfabetisch; voor het filter van de lijst.</summary>
        public List<string> Types()
        {
            var types = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in _entries.Values)
            {
                types.Add(entry.Type ?? string.Empty);
            }

            return new List<string>(types);
        }

        private static int Rank(SearchEntry entry, string needle)
        {
            var name = entry.Name;
            if (name.StartsWith(needle, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (name.IndexOf(" " + needle, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 1;
            }

            if (name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 2;
            }

            return entry.Type != null && entry.Type.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0 ? 3 : -1;
        }
    }
}
