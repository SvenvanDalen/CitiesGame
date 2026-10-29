using System;
using System.Linq;
using System.Text;

namespace Cities.Spikes.EcsBenchmark
{
    public static class BenchmarkStatistics
    {
        public static BenchmarkResult Summarize(string name, int agentCount, double[] ticks, double[] churn)
        {
            var sorted = ticks.OrderBy(t => t).ToArray();
            return new BenchmarkResult
            {
                Name = name,
                AgentCount = agentCount,
                MedianMs = Percentile(sorted, 0.5),
                P95Ms = Percentile(sorted, 0.95),
                MaxMs = sorted[sorted.Length - 1],
                ChurnMedianMs = Percentile(churn.OrderBy(t => t).ToArray(), 0.5),
            };
        }

        public static string ToMarkdownRow(BenchmarkResult r) =>
            FormattableString.Invariant(
                $"| {r.Name} | {r.AgentCount:N0} | {r.MedianMs:F3} | {r.P95Ms:F3} | {r.MaxMs:F3} | {r.ChurnMedianMs:F3} | {r.SetupMs:F1} | {r.MemoryBytes / (1024.0 * 1024.0):F2} |");

        public static string ToMarkdownTable(params BenchmarkResult[] results)
        {
            var builder = new StringBuilder();
            builder.AppendLine("| Variant | Agents | Mediaan tick (ms) | p95 tick (ms) | Max tick (ms) | Mediaan verloop (ms) | Opbouw (ms) | Geheugen agents (MB) |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");
            foreach (var result in results)
            {
                builder.AppendLine(ToMarkdownRow(result));
            }

            return builder.ToString();
        }

        private static double Percentile(double[] sorted, double fraction)
        {
            var index = (int)Math.Ceiling(fraction * sorted.Length) - 1;
            return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
        }
    }
}
