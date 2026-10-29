using System;
using System.Collections.Generic;

namespace Cities.Core.Diagnostics
{
    /// <summary>
    /// Performancebudget voor één bevolkingsgrootte, op de referentiemachine uit <c>docs/PERFORMANCE.md</c>.
    /// Tijden in milliseconden, geheugen in MB.
    /// </summary>
    public readonly struct PerformanceBudget
    {
        /// <summary>De waarden per groep, in de volgorde van de tabel in <c>docs/PERFORMANCE.md</c>.</summary>
        public PerformanceBudget(
            int population,
            (int Target, int MinimumSpec) fps,
            (float Tick, float MainThread) simulationMs,
            (int Memory, int SaveFile) sizeMb,
            (float Save, float Load) seconds)
        {
            Population = population;
            TargetFps = fps.Target;
            MinimumSpecFps = fps.MinimumSpec;
            TickMs = simulationMs.Tick;
            MainThreadSimulationMs = simulationMs.MainThread;
            MemoryMb = sizeMb.Memory;
            SaveFileMb = sizeMb.SaveFile;
            SaveSeconds = seconds.Save;
            LoadSeconds = seconds.Load;
        }

        /// <summary>Bovengrens van de bevolking waarvoor dit budget geldt.</summary>
        public int Population { get; }

        /// <summary>Beelden per seconde op de aanbevolen specificatie (gemiddeld; het 1%-laagste mag 20% lager).</summary>
        public int TargetFps { get; }

        /// <summary>Beelden per seconde op de minimale specificatie.</summary>
        public int MinimumSpecFps { get; }

        /// <summary>Wandtijd van één simulatietick (alle jobs samen, kritiek pad) op snelheid 3×.</summary>
        public float TickMs { get; }

        /// <summary>Simulatiewerk dat per frame op de main thread blijft: plannen, sync points, commands.</summary>
        public float MainThreadSimulationMs { get; }

        /// <summary>Geheugen van het proces (CPU-kant, zonder videogeheugen).</summary>
        public int MemoryMb { get; }

        public int SaveFileMb { get; }

        /// <summary>Opslaan tot de speler verder kan; het schrijven zelf mag daarna op de achtergrond doorlopen.</summary>
        public float SaveSeconds { get; }

        /// <summary>Laden van een save tot de stad speelbaar is.</summary>
        public float LoadSeconds { get; }

        public float FrameMs => 1000f / TargetFps;
    }

    /// <summary>
    /// De budgetten per bevolkingsgrootte. Dezelfde getallen staan in <c>docs/PERFORMANCE.md</c>; wijzig ze samen.
    /// </summary>
    public static class PerformanceBudgets
    {
        /// <summary>Ticks per seconde op snelheid 3× (10 Hz × 3); een tick mag hooguit de helft van die tijd kosten.</summary>
        public const int TicksPerSecondAtFastest = 30;

        /// <summary>Toegestane GC-allocatie per simulatietick in de hot path.</summary>
        public const int TickAllocationBytes = 0;

        /// <summary>Toegestane GC-allocatie per frame buiten het openen van UI (gemiddeld over 60 frames).</summary>
        public const int FrameAllocationBytes = 1024;

        private static readonly PerformanceBudget[] Tiers =
        {
            new PerformanceBudget(10_000, (60, 30), (2f, 0.5f), (2_000, 20), (0.5f, 5f)),
            new PerformanceBudget(50_000, (60, 30), (4f, 0.8f), (2_500, 30), (0.75f, 8f)),
            new PerformanceBudget(100_000, (60, 30), (6f, 1f), (3_000, 45), (1f, 12f)),
            new PerformanceBudget(250_000, (50, 30), (10f, 1.5f), (4_000, 80), (1.5f, 20f)),
            new PerformanceBudget(500_000, (40, 25), (14f, 2f), (6_000, 140), (2f, 30f)),
            new PerformanceBudget(1_000_000, (30, 20), (16f, 3f), (9_000, 250), (3f, 45f)),
        };

        public static IReadOnlyList<PerformanceBudget> All => Tiers;

        /// <summary>Het budget voor een bevolking: de kleinste grootte die er minstens zo groot is; daarboven de grootste.</summary>
        public static PerformanceBudget For(int population)
        {
            if (population < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(population), population, "Bevolking kan niet negatief zijn.");
            }

            foreach (var tier in Tiers)
            {
                if (population <= tier.Population)
                {
                    return tier;
                }
            }

            return Tiers[Tiers.Length - 1];
        }
    }
}
