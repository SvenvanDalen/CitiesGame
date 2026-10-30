using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Randomness
{
    /// <summary>
    /// Bewaakt dat simulatie- en wereldcode geen willekeur of tijd buiten de wereldseed om gebruikt. MonoBehaviours
    /// zijn uitgezonderd: die koppelen de simulatie aan frames en echte tijd.
    /// </summary>
    public class DeterminismSourceTests
    {
        private static readonly string[] Forbidden =
        {
            "UnityEngine.Random",
            "System.Random",
            "new Random(",
            "Random.Range(",
            "Random.value",
            "Random.insideUnitCircle",
            "DateTime.Now",
            "DateTime.UtcNow",
            "Environment.TickCount",
            "Guid.NewGuid",
            "Time.deltaTime",
            "Time.time",
            "Time.unscaledDeltaTime",
        };

        [TestCase("Simulation")]
        [TestCase("World")]
        public void SimulationCodeOnlyUsesTheWorldSeed(string folder)
        {
            var scripts = AssemblyLayeringTests.FindScriptsFolder();
            if (scripts == null)
            {
                Assert.Ignore("Map Assets/Scripts niet gevonden");
            }

            var violations = Directory.GetFiles(Path.Combine(scripts, folder), "*.cs", SearchOption.AllDirectories)
                .Where(file => !file.EndsWith("Behaviour.cs", StringComparison.Ordinal))
                .SelectMany(file => FindForbidden(File.ReadAllLines(file)).Select(token => $"{Path.GetFileName(file)}: {token}"))
                .ToList();

            Assert.That(violations, Is.Empty, "Gebruik DeterministicRandom via SimulationTick.Random en tijd via de tick");
        }

        [TestCase("var x = UnityEngine.Random.Range(0, 5);", "UnityEngine.Random")]
        [TestCase("var r = new System.Random(4);", "System.Random")]
        [TestCase("var stamp = DateTime.UtcNow;", "DateTime.UtcNow")]
        [TestCase("// UnityEngine.Random in commentaar", null)]
        [TestCase("/// <c>System.Random</c> in documentatie", null)]
        [TestCase("var random = source.At(tick);", null)]
        public void DetectsForbiddenCalls(string line, string expected)
        {
            Assert.That(FindForbidden(new[] { line }).FirstOrDefault(), Is.EqualTo(expected));
        }

        private static string[] FindForbidden(string[] lines) =>
            lines.Select(line => line.Trim())
                .Where(line => !line.StartsWith("//", StringComparison.Ordinal))
                .SelectMany(line => Forbidden.Where(token => line.Contains(token)))
                .ToArray();
    }
}
