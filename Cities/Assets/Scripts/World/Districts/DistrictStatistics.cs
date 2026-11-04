using System;
using System.Collections.Generic;

namespace Cities.World.Districts
{
    /// <summary>Een burger met zijn woonplek, voor de districtstatistieken (E13-02).</summary>
    public readonly struct DistrictResident
    {
        public DistrictResident(float x, float z, bool isWorkingAge, bool hasWork)
        {
            X = x;
            Z = z;
            IsWorkingAge = isWorkingAge;
            HasWork = hasWork;
        }

        public float X { get; }

        public float Z { get; }

        /// <summary>Behoort tot de beroepsbevolking (volwassen, niet gepensioneerd).</summary>
        public bool IsWorkingAge { get; }

        public bool HasWork { get; }
    }

    /// <summary>Een werkplek (gebouw of bedrijf) met aantal banen en bezette banen.</summary>
    public readonly struct DistrictWorkplace
    {
        public DistrictWorkplace(float x, float z, int jobs, int filled)
        {
            X = x;
            Z = z;
            Jobs = Math.Max(0, jobs);
            Filled = Math.Min(Math.Max(0, filled), Jobs);
        }

        public float X { get; }

        public float Z { get; }

        public int Jobs { get; }

        public int Filled { get; }
    }

    /// <summary>Een bedrag aan weekinkomsten van de stad (belasting, handel) dat op een plek is verdiend.</summary>
    public readonly struct DistrictIncome
    {
        public DistrictIncome(float x, float z, long amount)
        {
            X = x;
            Z = z;
            Amount = amount;
        }

        public float X { get; }

        public float Z { get; }

        public long Amount { get; }
    }

    /// <summary>De cijfers van één district; <see cref="Id"/> 0 is alles buiten districten.</summary>
    public sealed class DistrictReport
    {
        public DistrictReport(int id, string name, float areaSquareMeters)
        {
            Id = id;
            Name = name;
            Area = areaSquareMeters;
        }

        public int Id { get; }

        public string Name { get; }

        /// <summary>Oppervlak in vierkante meters.</summary>
        public float Area { get; }

        public int Population { get; internal set; }

        /// <summary>Inwoners in de beroepsbevolking.</summary>
        public int Workforce { get; internal set; }

        /// <summary>Inwoners in de beroepsbevolking met een baan.</summary>
        public int Employed { get; internal set; }

        /// <summary>Banen die in het district zitten.</summary>
        public int Jobs { get; internal set; }

        /// <summary>Banen in het district die bezet zijn.</summary>
        public int FilledJobs { get; internal set; }

        /// <summary>Gemiddeld vervuilingsniveau (0 tot 1) over de blokken van het district.</summary>
        public float AveragePollution { get; internal set; }

        /// <summary>Hoogste vervuilingsniveau van een blok in het district.</summary>
        public float PeakPollution { get; internal set; }

        /// <summary>Inkomsten per week uit dit district.</summary>
        public long Income { get; internal set; }

        public int Unemployed => Workforce - Employed;

        /// <summary>Werkloosheid als aandeel van de beroepsbevolking (0 tot 1); 0 zonder beroepsbevolking.</summary>
        public float UnemploymentRate => Workforce == 0 ? 0f : (float)Unemployed / Workforce;

        /// <summary>Vacatures: banen die nog niet bezet zijn.</summary>
        public int Vacancies => Jobs - FilledJobs;

        /// <summary>Inwoners per vierkante kilometer; 0 zonder oppervlak.</summary>
        public float DensityPerKm2 => Area <= 0f ? 0f : Population / (Area / 1_000_000f);

        /// <summary>Inkomsten per inwoner; 0 zonder inwoners.</summary>
        public long IncomePerResident => Population == 0 ? 0L : Income / Population;
    }

    /// <summary>
    /// Statistieken per district (E13-02): bevolking, banen, vervuiling en inkomsten. De invoer komt via kleine
    /// waardetypes en een vervuilingsfunctie, zodat de telling los staat van burgers, gebouwen, velden en boekhouding.
    /// Er is geen eigen toestand; het resultaat wordt telkens opnieuw berekend en dus niet opgeslagen.
    /// </summary>
    public static class DistrictStatistics
    {
        /// <summary>
        /// Telt alles per district. Het resultaat bevat altijd rij 0 (buiten districten) en daarna elk district op nummer.
        /// <paramref name="pollutionAt"/> geeft het vervuilingsniveau (0 tot 1) op een wereldpositie en wordt in het midden van elk blok gelezen.
        /// </summary>
        public static IReadOnlyList<DistrictReport> Compute(
            DistrictMap map,
            IEnumerable<DistrictResident> residents,
            IEnumerable<DistrictWorkplace> workplaces,
            IEnumerable<DistrictIncome> income,
            Func<float, float, float> pollutionAt)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            var rows = new Dictionary<int, DistrictReport>
            {
                [DistrictBook.WholeCity] = new DistrictReport(DistrictBook.WholeCity, "Buiten districten", map.Area(DistrictBook.WholeCity)),
            };
            var ordered = new List<DistrictReport> { rows[DistrictBook.WholeCity] };
            foreach (var info in map.Book.Districts)
            {
                var row = new DistrictReport(info.Id, info.Name, map.Area(info.Id));
                rows[info.Id] = row;
                ordered.Add(row);
            }

            foreach (var r in residents ?? Array.Empty<DistrictResident>())
            {
                var row = rows[map.AtPosition(r.X, r.Z)];
                row.Population++;
                if (r.IsWorkingAge)
                {
                    row.Workforce++;
                    row.Employed += r.HasWork ? 1 : 0;
                }
            }

            foreach (var w in workplaces ?? Array.Empty<DistrictWorkplace>())
            {
                var row = rows[map.AtPosition(w.X, w.Z)];
                row.Jobs += w.Jobs;
                row.FilledJobs += w.Filled;
            }

            foreach (var i in income ?? Array.Empty<DistrictIncome>())
            {
                rows[map.AtPosition(i.X, i.Z)].Income += i.Amount;
            }

            AddPollution(map, rows, pollutionAt);
            return ordered;
        }

        private static void AddPollution(DistrictMap map, Dictionary<int, DistrictReport> rows, Func<float, float, float> pollutionAt)
        {
            if (pollutionAt == null)
            {
                return;
            }

            var sums = new Dictionary<int, double>();
            var size = map.BlockSize;
            for (var bz = 0; bz < map.BlocksZ; bz++)
            {
                for (var bx = 0; bx < map.BlocksX; bx++)
                {
                    var id = map.At(bx, bz);
                    var level = Math.Min(Math.Max(pollutionAt((bx + 0.5f) * size, (bz + 0.5f) * size), 0f), 1f);
                    sums[id] = (sums.TryGetValue(id, out var sum) ? sum : 0d) + level;
                    rows[id].PeakPollution = Math.Max(rows[id].PeakPollution, level);
                }
            }

            foreach (var pair in sums)
            {
                rows[pair.Key].AveragePollution = (float)(pair.Value / map.BlockCount(pair.Key));
            }
        }
    }
}
