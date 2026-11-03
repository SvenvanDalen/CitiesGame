using Cities.Simulation.Citizens;

namespace Cities.Presentation.UI
{
    /// <summary>
    /// Teksten van de burger-inspector (E07-12): huis, werk, wat de burger nu doet en waarheen hij onderweg is, stemming en levensfase.
    /// Alles volgt uit de <see cref="Citizen"/> zelf en het dagschema, dus de inspector bewaart niets en heeft geen opslagsectie nodig.
    /// </summary>
    public static class CitizenInspectorText
    {
        public const string None = "geen";

        public static string LifeStage(in Citizen citizen)
        {
            switch (citizen.Group)
            {
                case AgeGroup.Child: return $"Kind ({citizen.Age} jaar)";
                case AgeGroup.Retired: return $"Gepensioneerde ({citizen.Age} jaar)";
                default: return $"Volwassene ({citizen.Age} jaar)";
            }
        }

        public static string Mood(in Citizen citizen)
        {
            if (citizen.Happiness >= 70)
            {
                return "Blij";
            }

            return citizen.Happiness >= 40 ? "Tevreden" : "Ontevreden";
        }

        public static string Education(in Citizen citizen)
        {
            switch (citizen.Education)
            {
                case 0: return "Laag";
                case 1: return "Middelbaar";
                default: return "Hoog";
            }
        }

        public static string Home(in Citizen citizen) => citizen.HasHome ? $"Gebouw {citizen.Home.Index}" : "Dakloos";

        public static string Work(in Citizen citizen)
        {
            if (citizen.HasWork)
            {
                return $"Gebouw {citizen.Work.Index}";
            }

            return citizen.Group == AgeGroup.Adult ? "Werkzoekend" : None;
        }

        public static string Doing(Activity activity)
        {
            switch (activity)
            {
                case Activity.Sleep: return "Slaapt";
                case Activity.Work: return "Werkt";
                case Activity.School: return "Op school";
                case Activity.Shop: return "Winkelt";
                default: return "Vrije tijd";
            }
        }

        public static string Route(Destination destination)
        {
            switch (destination)
            {
                case Destination.Home: return "Thuis";
                case Destination.Work: return "Werk";
                case Destination.School: return "School";
                case Destination.Shop: return "Winkel";
                case Destination.Park: return "Park";
                default: return "Onbepaald";
            }
        }

        /// <summary>De regels van het paneel voor <paramref name="citizen"/> met ID <paramref name="id"/> op <paramref name="day"/> om <paramref name="hour"/>.</summary>
        public static string[] Lines(in Citizen citizen, uint id, int day, int hour)
        {
            var activity = DailySchedule.ActivityAt(citizen, id, day, hour);
            return new[]
            {
                "Levensfase: " + LifeStage(citizen),
                "Stemming: " + Mood(citizen) + $" ({citizen.Happiness})",
                "Gezondheid: " + citizen.Health,
                "Opleiding: " + Education(citizen),
                "Woning: " + Home(citizen),
                "Werk: " + Work(citizen),
                "Nu: " + Doing(activity),
                "Bestemming: " + Route(DailySchedule.DestinationFor(citizen, activity)),
            };
        }
    }
}
