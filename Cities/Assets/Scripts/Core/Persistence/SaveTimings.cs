using System;
using Cities.Core.Diagnostics;

namespace Cities.Core.Persistence
{
    /// <summary>
    /// Houdt de duur van het laatste opslaan en laden bij (E23-09) en legt die naast het budget uit
    /// <see cref="PerformanceBudgets"/> voor de bevolking van dat moment.
    /// </summary>
    public sealed class SaveTimings
    {
        public double LastSaveSeconds { get; private set; }

        public double LastLoadSeconds { get; private set; }

        public long LastSaveBytes { get; private set; }

        public void RecordSave(TimeSpan duration, long bytes)
        {
            LastSaveSeconds = duration.TotalSeconds;
            LastSaveBytes = bytes;
        }

        public void RecordLoad(TimeSpan duration) => LastLoadSeconds = duration.TotalSeconds;

        /// <summary>Meldingen voor wat het budget overschrijdt, of een lege lijst als alles binnen budget is.</summary>
        public string[] Violations(int population)
        {
            var budget = PerformanceBudgets.For(population);
            var found = new System.Collections.Generic.List<string>(3);
            if (LastSaveSeconds > budget.SaveSeconds)
            {
                found.Add($"Opslaan duurde {LastSaveSeconds:0.##} s; het budget is {budget.SaveSeconds:0.##} s.");
            }

            if (LastLoadSeconds > budget.LoadSeconds)
            {
                found.Add($"Laden duurde {LastLoadSeconds:0.##} s; het budget is {budget.LoadSeconds:0.##} s.");
            }

            var megabytes = LastSaveBytes / (1024.0 * 1024.0);
            if (megabytes > budget.SaveFileMb)
            {
                found.Add($"De save is {megabytes:0.#} MB; het budget is {budget.SaveFileMb} MB.");
            }

            return found.ToArray();
        }
    }
}
