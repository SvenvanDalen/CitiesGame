using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Cities.Tests.EditMode
{
    /// <summary>
    /// Bewaakt de laagindeling uit docs/CONVENTIONS.md: een assembly mag alleen naar lagere lagen verwijzen.
    /// </summary>
    public class AssemblyLayeringTests
    {
        private static readonly string[] Layers =
        {
            "Cities.Core",
            "Cities.Simulation",
            "Cities.World",
            "Cities.Gameplay",
            "Cities.Presentation",
            "Cities.Editor",
        };

        private static IEnumerable<string> RuntimeLayers => Layers.Take(Layers.Length - 1);

        [TestCaseSource(nameof(Layers))]
        public void AssemblyReferencesOnlyLowerLayers(string assemblyName)
        {
            var assembly = Load(assemblyName);
            if (assembly == null)
            {
                Assert.Ignore($"{assemblyName} bevat nog geen code");
            }

            var allowed = new HashSet<string>(Layers.TakeWhile(l => l != assemblyName));
            var violations = assembly.GetReferencedAssemblies()
                .Select(r => r.Name)
                .Where(n => n.StartsWith("Cities.", StringComparison.Ordinal) && !allowed.Contains(n))
                .ToList();

            Assert.That(violations, Is.Empty, $"{assemblyName} verwijst naar een gelijke of hogere laag");
        }

        [TestCaseSource(nameof(RuntimeLayers))]
        public void RuntimeAssemblyDoesNotReferenceUnityEditor(string assemblyName)
        {
            var assembly = Load(assemblyName);
            if (assembly == null)
            {
                Assert.Ignore($"{assemblyName} bevat nog geen code");
            }

            var references = assembly.GetReferencedAssemblies().Select(r => r.Name);
            Assert.That(references, Has.None.StartsWith("UnityEditor"));
        }

        private static Assembly Load(string name) =>
            AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == name);
    }
}
