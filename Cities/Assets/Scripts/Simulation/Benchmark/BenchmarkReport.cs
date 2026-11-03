using System;
using System.Globalization;
using System.Text;
using Cities.Core.Diagnostics;
using Cities.Simulation.Scheduling;

namespace Cities.Simulation.Benchmark
{
    /// <summary>
    /// Uitkomst van een meting (E23-02): de tijd per systeem en of de tick binnen het budget van <see cref="PerformanceBudgets"/> bleef.
    /// De meting faalt op een vast budget; vergelijken met de vorige run hoort bij de CI-koppeling.
    /// </summary>
    public readonly struct BenchmarkReport
    {
        private BenchmarkReport(bool passed, string text)
        {
            Passed = passed;
            Text = text;
        }

        public bool Passed { get; }

        public string Text { get; }

        public static BenchmarkReport Evaluate(long population, SystemTimings timings)
        {
            if (timings is null)
            {
                throw new ArgumentNullException(nameof(timings));
            }

            var budget = PerformanceBudgets.For((int)Math.Min(population, int.MaxValue));
            var tick = timings.Tick;
            var passed = tick.Samples > 0 && tick.AverageMs <= budget.TickMs;
            var text = new StringBuilder();
            text.Append(passed ? "GESLAAGD" : "GEFAALD").Append(": ").Append(population).Append(" inwoners, tick ")
                .Append(Ms(tick.AverageMs)).Append(" (budget ").Append(Ms(budget.TickMs)).Append(')');
            foreach (var system in timings.Systems)
            {
                text.Append("\n  ").Append(system).Append(": ").Append(Ms(timings.Get(system).AverageMs));
            }

            return new BenchmarkReport(passed, text.ToString());
        }

        private static string Ms(double value) => value.ToString("0.00", CultureInfo.InvariantCulture) + " ms";
    }
}
