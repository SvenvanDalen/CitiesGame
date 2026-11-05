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

        public StartupProgress(IEnumerable<string> steps)
        {
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

            _done.Add(step);
        }

        /// <summary>Meldt een stap op de gedeelde voortgang, als die er is.</summary>
        public static void Report(string step) => Shared?.Complete(step);
    }
}
