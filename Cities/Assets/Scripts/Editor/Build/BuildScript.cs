using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Cities.Editor.Build
{
    /// <summary>
    /// Bouwt de Windows64-player voor de release-pipeline (.github/workflows/release.yml).
    /// Aanroep: Unity -batchmode -quit -executeMethod Cities.Editor.Build.BuildScript.PerformWindowsBuild -buildVersion 0.12
    /// Optioneel: -buildOutput &lt;map&gt; (anders -customBuildPath van game-ci, anders ../build naast de projectmap).
    /// Resultaat: &lt;uitvoer&gt;/Game/Cities.exe plus &lt;uitvoer&gt;/Game/version.txt.
    /// </summary>
    public static class BuildScript
    {
        public const string GameFolder = "Game";
        public const string ExecutableName = "Cities.exe";
        public const string VersionFileName = "version.txt";

        public static void PerformWindowsBuild()
        {
            var args = Environment.GetCommandLineArgs();
            var version = ReadArgument(args, "-buildVersion");
            if (string.IsNullOrEmpty(version) || !version.All(c => char.IsDigit(c) || c == '.'))
            {
                Fail("Geef een versie mee met -buildVersion, bijvoorbeeld 0.12.");
                return;
            }

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Fail("Geen scenes ingeschakeld in Build Settings.");
                return;
            }

            var output = ReadArgument(args, "-buildOutput")
                ?? ReadArgument(args, "-customBuildPath")
                ?? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "build"));
            var gameDirectory = Path.Combine(output, GameFolder);
            Directory.CreateDirectory(gameDirectory);

            PlayerSettings.bundleVersion = version;
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(gameDirectory, ExecutableName),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Fail($"Build mislukt: {report.summary.result}, {report.summary.totalErrors} fouten.");
                return;
            }

            File.WriteAllText(Path.Combine(gameDirectory, VersionFileName), version);
            Debug.Log($"Build {version} klaar in {gameDirectory} ({report.summary.totalSize / (1024 * 1024)} MB).");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        private static string ReadArgument(string[] args, string name)
        {
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        private static void Fail(string message)
        {
            Debug.LogError(message);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
