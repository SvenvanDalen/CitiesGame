using System;

namespace Cities.Simulation.Commands
{
    /// <summary>Wat er met een command is gebeurd toen de simulatie het verwerkte.</summary>
    public enum CommandOutcome
    {
        Executed,
        Rejected,
    }

    /// <summary>
    /// Verslag van één verwerkt command. Bruikbaar voor de UI (melding bij afwijzing),
    /// voor undo (E01-12) en om een sessie na te spelen bij bugreproductie.
    /// </summary>
    public readonly struct CommandRecord
    {
        public CommandRecord(long sequence, long tick, ISimulationCommand command, CommandOutcome outcome, CommandValidation validation)
        {
            Sequence = sequence;
            Tick = tick;
            Command = command ?? throw new ArgumentNullException(nameof(command));
            Outcome = outcome;
            Validation = validation;
        }

        /// <summary>Volgnummer in de volgorde van indienen; uniek per bus.</summary>
        public long Sequence { get; }

        /// <summary>De laatste tick die vóór dit command is uitgevoerd; het command geldt vanaf de tick erna.</summary>
        public long Tick { get; }

        public ISimulationCommand Command { get; }

        public CommandOutcome Outcome { get; }

        /// <summary>De validatie op het moment van uitvoeren, inclusief reden en werkelijke kosten.</summary>
        public CommandValidation Validation { get; }
    }
}
