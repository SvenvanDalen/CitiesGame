using System;

namespace Cities.Simulation.Commands
{
    /// <summary>Uitkomst van het valideren van een command, met de kosten voor de preview.</summary>
    public readonly struct CommandValidation : IEquatable<CommandValidation>
    {
        private CommandValidation(bool isValid, string reason, long cost)
        {
            IsValid = isValid;
            Reason = reason;
            Cost = cost;
        }

        public bool IsValid { get; }

        /// <summary>
        /// Korte, stabiele sleutel die zegt waarom het command ongeldig is (bijvoorbeeld <c>insufficient-funds</c>);
        /// de UI vertaalt die naar tekst. Leeg bij een geldig command.
        /// </summary>
        public string Reason { get; }

        /// <summary>Kosten in hele munteenheden; negatief bij een terugbetaling, zoals bij slopen.</summary>
        public long Cost { get; }

        public static CommandValidation Valid(long cost = 0) => new CommandValidation(true, string.Empty, cost);

        public static CommandValidation Invalid(string reason, long cost = 0)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Een ongeldig command heeft een reden nodig.", nameof(reason));
            }

            return new CommandValidation(false, reason, cost);
        }

        public bool Equals(CommandValidation other) =>
            IsValid == other.IsValid && Cost == other.Cost && string.Equals(Reason, other.Reason, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is CommandValidation other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(IsValid, Reason, Cost);

        public override string ToString() => IsValid ? $"Valid (cost {Cost})" : $"Invalid: {Reason} (cost {Cost})";

        public static bool operator ==(CommandValidation left, CommandValidation right) => left.Equals(right);

        public static bool operator !=(CommandValidation left, CommandValidation right) => !left.Equals(right);
    }
}
