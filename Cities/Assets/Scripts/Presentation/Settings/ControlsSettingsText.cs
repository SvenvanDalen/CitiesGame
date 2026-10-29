using Cities.Gameplay.Controls;

namespace Cities.Presentation.Settings
{
    /// <summary>Teksten van het besturingsscherm.</summary>
    public static class ControlsSettingsText
    {
        public const string Title = "Besturing";
        public const string Hint = "Klik op een toets en druk daarna op de nieuwe toets of muisknop. Esc annuleert.";
        public const string Waiting = "Druk op een toets…";
        public const string ColumnAction = "Actie";
        public const string ColumnPrimary = "Primair";
        public const string ColumnAlternative = "Alternatief";
        public const string Reset = "Herstel";
        public const string ResetTooltip = "Standaardtoetsen voor deze actie terugzetten";
        public const string ResetAll = "Alles herstellen";
        public const string Done = "Klaar";
        public const string FixedTooltip = "Vaste toets; niet te wijzigen";
        public const string EmptyTooltip = "Geen toets; klik om er een te kiezen";
        public const string Canceled = "Herbinden geannuleerd.";
        public const string ResetAllDone = "Alle toetsen staan weer op de standaard.";

        public static string Listening(string label) => $"Druk op een toets of muisknop voor ‘{label}’. Esc annuleert.";

        public static string RowReset(string label) => $"‘{label}’ gebruikt weer de standaardtoetsen.";

        /// <summary>Melding na het kiezen van een toets; <paramref name="displayName"/> geeft de naam van een slot.</summary>
        public static string Outcome(RebindOutcome outcome, System.Func<SlotRef, string> displayName)
        {
            switch (outcome.Status)
            {
                case RebindStatus.Canceled:
                    return Canceled;
                case RebindStatus.Unchanged:
                    return $"‘{outcome.Target.Row.Label}’ houdt {displayName(outcome.Target)}.";
                default:
                    var message = $"‘{outcome.Target.Row.Label}’ gebruikt nu {displayName(outcome.Target)}.";
                    if (!outcome.Swapped.IsValid)
                    {
                        return message;
                    }

                    var other = displayName(outcome.Swapped);
                    var otherKey = other == "—" ? "geen toets meer" : other;
                    return $"{message} Die toets hoorde bij ‘{outcome.Swapped.Row.Label}’; dat heeft nu {otherKey}.";
            }
        }
    }
}
