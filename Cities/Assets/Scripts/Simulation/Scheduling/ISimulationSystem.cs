using Cities.Simulation.Timing;
using Unity.Jobs;

namespace Cities.Simulation.Scheduling
{
    /// <summary>Wat een systeem per tick over de tijd weet.</summary>
    public readonly struct SimulationTick
    {
        public SimulationTick(long tick, float stepSeconds, GameDate date, float timeOfDay)
        {
            Tick = tick;
            StepSeconds = stepSeconds;
            Date = date;
            TimeOfDay = timeOfDay;
        }

        public long Tick { get; }

        /// <summary>Lengte van één simulatiestap in echte seconden bij 1x; simulaties rekenen hiermee, nooit met frametijd.</summary>
        public float StepSeconds { get; }

        public GameDate Date { get; }

        /// <summary>Tijd van de dag als fractie (0 = middernacht).</summary>
        public float TimeOfDay { get; }
    }

    /// <summary>
    /// Eén simulatiesysteem. Plant zijn werk als jobs in en geeft de handle terug; blokkeert de main thread niet.
    /// </summary>
    /// <remarks>
    /// <paramref name="dependency"/> in <see cref="Schedule"/> omvat de vorige tick en alle systemen waarna dit systeem
    /// is geregistreerd. Wie data van een ander systeem leest of schrijft, moet daarom na dat systeem geregistreerd zijn.
    /// Een systeem dat op de main thread moet werken, roept zelf <c>dependency.Complete()</c> aan; dat is een
    /// synchronisatiepunt en hoort een uitzondering te zijn.
    /// </remarks>
    public interface ISimulationSystem
    {
        /// <summary>Naam voor profiler, logs en foutmeldingen.</summary>
        string Name { get; }

        JobHandle Schedule(in SimulationTick tick, JobHandle dependency);
    }
}
