using System;
using System.Collections.Generic;

namespace Cities.Core.Diagnostics
{
    /// <summary>
    /// Voortgang van het opstarten van een stad (E26-01): een vaste lijst stappen die een voor een worden afgerond. Het laadscherm leest
    /// <see cref="Fraction"/> en <see cref="Current"/>; de generatoren melden met <see cref="Complete"/> dat hun stap klaar is.
    /// </summary>
    public sealed class StartupProgress
    {
        private readonly List<string> _steps;
        private readonly HashSet<string> _done = new HashSet<string>();
        private readonly List<(string Step, double Seconds)> _timings = new List<(string, double)>();
        private readonly Func<double> _now;
        private readonly double _started;
        private double _last;

        /// <summary>Budget voor het genereren van een stad in de build (E26-16): daarboven hoort de generatie op de achtergrond te draaien.</summary>
        public const double BudgetSeconds = 5.0;

        public StartupProgress(IEnumerable<string> steps)
            : this(steps, () => System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency)
        {
        }

        /// <param name="now">Klok in seconden; tests geven hier een eigen klok mee.</param>
        public StartupProgress(IEnumerable<string> steps, Func<double> now)
        {
            _now = now ?? throw new ArgumentNullException(nameof(now));
            _started = _now();
            _last = _started;
            if (steps is null)
            {
                throw new ArgumentNullException(nameof(steps));
            }

            _steps = new List<string>(steps);
            if (_steps.Count == 0)
            {
                throw new ArgumentException("Minstens één stap.", nameof(steps));
            }
        }

        /// <summary>De standaardstappen van de hoofdscène.</summary>
        public static StartupProgress ForMainScene() => new StartupProgress(new[] { "Terrein", "Bossen", "Wegen en water", "Stad" });

        /// <summary>De gedeelde voortgang van de lopende start; <c>null</c> als er niet gestart wordt.</summary>
        public static StartupProgress Shared { get; set; }

        public IReadOnlyList<string> Steps => _steps;

        /// <summary>Aandeel afgeronde stappen, 0 tot 1.</summary>
        public float Fraction => (float)_done.Count / _steps.Count;

        public bool IsComplete => _done.Count == _steps.Count;

        /// <summary>De eerste nog niet afgeronde stap, of <c>null</c> als alles klaar is.</summary>
        public string Current
        {
            get
            {
                foreach (var step in _steps)
                {
                    if (!_done.Contains(step))
                    {
                        return step;
                    }
                }

                return null;
            }
        }

        /// <summary>Rondt een stap af; een onbekende stap geeft een fout, dubbel afronden is onschadelijk.</summary>
        public void Complete(string step)
        {
            if (!_steps.Contains(step))
            {
                throw new ArgumentException($"Onbekende stap '{step}'.", nameof(step));
            }

            if (_done.Add(step))
            {
                var time = _now();
                _timings.Add((step, time - _last));
                _last = time;
            }
        }

        /// <summary>Hoe lang elke afgeronde stap duurde, in volgorde van afronden.</summary>
        public IReadOnlyList<(string Step, double Seconds)> Timings => _timings;

        /// <summary>Tijd tussen het begin en de laatst afgeronde stap.</summary>
        public double TotalSeconds => _last - _started;

        public bool OverBudget => TotalSeconds > BudgetSeconds;

        /// <summary>Eén regel voor het log: totale tijd en per stap, met een waarschuwing als het budget is overschreden.</summary>
        public string Summary()
        {
            var parts = new List<string>();
            foreach (var (step, seconds) in _timings)
            {
                parts.Add($"{step} {seconds:0.0} s");
            }

            return $"Stad gemaakt in {TotalSeconds:0.0} s ({string.Join(", ", parts)})" + (OverBudget ? $", boven het budget van {BudgetSeconds:0} s" : string.Empty);
        }

        /// <summary>Meldt een stap op de gedeelde voortgang, als die er is.</summary>
        public static void Report(string step) => Shared?.Complete(step);
    }
}
