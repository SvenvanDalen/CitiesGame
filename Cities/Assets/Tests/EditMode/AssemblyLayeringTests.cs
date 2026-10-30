using System;
using System.Collections.Generic;
using System.IO;
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

        /// <summary>
        /// Runtime-code mag UnityEditor alleen binnen <c>#if UNITY_EDITOR</c> gebruiken, anders breekt de player-build.
        /// Gecontroleerd op de broncode, omdat Unity in de editor zelf editor-referenties injecteert (job-registratie).
        /// </summary>
        [TestCaseSource(nameof(RuntimeLayers))]
        public void RuntimeSourceUsesUnityEditorOnlyInEditorBlocks(string assemblyName)
        {
            var scripts = FindScriptsFolder();
            if (scripts == null)
            {
                Assert.Ignore("Map Assets/Scripts niet gevonden");
            }

            var folder = Path.Combine(scripts, assemblyName.Substring("Cities.".Length));
            if (!Directory.Exists(folder))
            {
                Assert.Ignore($"Map voor {assemblyName} ontbreekt");
            }

            var violations = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories)
                .Where(file => UsesUnityEditorOutsideEditorBlock(File.ReadAllLines(file)))
                .Select(file => Path.GetFileName(file))
                .ToList();

            Assert.That(violations, Is.Empty, $"{assemblyName} gebruikt UnityEditor buiten #if UNITY_EDITOR");
        }

        [TestCase("using UnityEditor;", true)]
        [TestCase("var path = UnityEditor.AssetDatabase.GetAssetPath(x);", true)]
        [TestCase("#if UNITY_EDITOR\nusing UnityEditor;\n#endif", false)]
        [TestCase("#if UNITY_EDITOR\n#if DEBUG\n#endif\nUnityEditor.EditorApplication.Beep();\n#endif", false)]
        [TestCase("// UnityEditor in commentaar", false)]
        [TestCase("using UnityEngine;", false)]
        public void DetectsUnityEditorUsage(string source, bool expected)
        {
            Assert.That(UsesUnityEditorOutsideEditorBlock(source.Split('\n')), Is.EqualTo(expected));
        }

        private static bool UsesUnityEditorOutsideEditorBlock(string[] lines)
        {
            // Telt geneste #if-blokken; binnen een #if UNITY_EDITOR-blok (op elke diepte) is UnityEditor toegestaan.
            var editorDepth = 0;
            var depth = 0;
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.StartsWith("#if", StringComparison.Ordinal))
                {
                    depth++;
                    if (editorDepth == 0 && line.Contains("UNITY_EDITOR") && !line.Contains("!UNITY_EDITOR"))
                    {
                        editorDepth = depth;
                    }
                }
                else if (line.StartsWith("#endif", StringComparison.Ordinal))
                {
                    if (depth == editorDepth)
                    {
                        editorDepth = 0;
                    }

                    depth--;
                }
                else if (editorDepth == 0 && !line.StartsWith("//", StringComparison.Ordinal) && line.Contains("UnityEditor"))
                {
                    return true;
                }
            }

            return false;
        }

        internal static string FindScriptsFolder()
        {
            // Unity draait met de projectmap als werkmap; dotnet test vanuit de repository of de bin-map.
            foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            {
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                {
                    foreach (var candidate in new[] { Path.Combine(dir.FullName, "Assets", "Scripts"), Path.Combine(dir.FullName, "Cities", "Assets", "Scripts") })
                    {
                        if (Directory.Exists(candidate))
                        {
                            return candidate;
                        }
                    }
                }
            }

            return null;
        }

        private static Assembly Load(string name) =>
            AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == name);
    }
}
