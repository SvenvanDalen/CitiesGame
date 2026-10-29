using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Cities.Spikes.EcsBenchmark
{
    /// <summary>
    /// Draait beide varianten in een standalone build, schrijft een Markdown-tabel naast de executable en sluit af.
    /// </summary>
    public class BenchmarkRunner : MonoBehaviour
    {
        private void Start()
        {
            var results = new List<BenchmarkResult>();
            foreach (var count in BenchmarkScenario.AgentCounts)
            {
                results.Add(EntitiesBenchmark.Run(count));
                results.Add(CustomStoreBenchmark.Run(count));
            }

            var report = BenchmarkStatistics.ToMarkdownTable(results.ToArray()) +
                $"\nCPU: {SystemInfo.processorType} ({SystemInfo.processorCount} threads), RAM: {SystemInfo.systemMemorySize} MB, " +
                $"Unity {Application.unityVersion}, development build: {Debug.isDebugBuild}\n";
            var path = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "ecs-benchmark.md");
            File.WriteAllText(path, report);
            Debug.Log(report);
            Application.Quit();
        }
    }
}
