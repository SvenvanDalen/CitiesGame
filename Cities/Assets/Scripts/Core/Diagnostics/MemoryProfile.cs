using System;
using System.Collections.Generic;

namespace Cities.Core.Diagnostics
{
    /// <summary>Geheugen van één systeem op het moment van meten.</summary>
    public readonly struct MemoryUsage
    {
        public MemoryUsage(string system, long bytes)
        {
            System = system;
            Bytes = bytes;
        }

        public string System { get; }

        public long Bytes { get; }

        public double Megabytes => Bytes / (1024.0 * 1024.0);
    }

    /// <summary>
    /// Geheugenprofiel per systeem (E23-08). Systemen melden zich aan met een functie die hun eigen geheugen in bytes
    /// teruggeeft (native arrays, lijsten, caches); <see cref="Measure"/> maakt er een lijst van, grootste eerst, en
    /// <see cref="Check"/> legt het totaal naast het budget uit <see cref="PerformanceBudgets"/>.
    /// </summary>
    public sealed class MemoryProfile
    {
        private readonly SortedDictionary<string, Func<long>> _reporters = new SortedDictionary<string, Func<long>>(StringComparer.Ordinal);

        public int Count => _reporters.Count;

        /// <summary>Meldt een systeem aan; een tweede aanmelding met dezelfde naam vervangt de eerste.</summary>
        public void Register(string system, Func<long> bytes)
        {
            if (string.IsNullOrWhiteSpace(system))
            {
                throw new ArgumentException("Een systeemnaam is verplicht.", nameof(system));
            }

            _reporters[system] = bytes ?? throw new ArgumentNullException(nameof(bytes));
        }

        public bool Unregister(string system) => _reporters.Remove(system);

        /// <summary>Meet alle systemen, het grootste eerst; een systeem dat een fout geeft telt als 0.</summary>
        public List<MemoryUsage> Measure()
        {
            var usages = new List<MemoryUsage>(_reporters.Count);
            foreach (var pair in _reporters)
            {
                long bytes;
                try
                {
                    bytes = Math.Max(0, pair.Value());
                }
                catch (Exception exception)
                {
                    SystemLog.Sink.Write(LogLevel.Warning, "Memory", $"Geheugenmeting van {pair.Key} mislukte: {exception.Message}");
                    bytes = 0;
                }

                usages.Add(new MemoryUsage(pair.Key, bytes));
            }

            usages.Sort((a, b) =>
            {
                var bySize = b.Bytes.CompareTo(a.Bytes);
                return bySize != 0 ? bySize : string.CompareOrdinal(a.System, b.System);
            });
            return usages;
        }

        public long TotalBytes()
        {
            long total = 0;
            foreach (var usage in Measure())
            {
                total += usage.Bytes;
            }

            return total;
        }

        /// <summary>Totaal tegenover het geheugenbudget voor <paramref name="population"/>; <c>null</c> als het past.</summary>
        public string Check(int population)
        {
            var budgetMb = PerformanceBudgets.For(population).MemoryMb;
            var totalMb = TotalBytes() / (1024.0 * 1024.0);
            return totalMb <= budgetMb ? null : $"Geheugen van de systemen is {totalMb:0.#} MB; het budget voor {population} inwoners is {budgetMb} MB.";
        }

        /// <summary>Tekst met één regel per systeem, voor de debug-overlay en het logboek.</summary>
        public string Describe()
        {
            var lines = new List<string>();
            foreach (var usage in Measure())
            {
                lines.Add($"{usage.System}: {usage.Megabytes:0.##} MB");
            }

            return string.Join("\n", lines);
        }
    }
}
