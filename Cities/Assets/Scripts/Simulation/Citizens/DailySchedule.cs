using Cities.Core.Randomness;

namespace Cities.Simulation.Citizens
{
    public enum Activity
    {
        Sleep = 0,
        Work = 1,
        School = 2,
        Shop = 3,
        Leisure = 4,
    }

    /// <summary>Waar de burger heen moet voor een activiteit; <see cref="Anywhere"/> is voor een dakloze die slaapt.</summary>
    public enum Destination
    {
        Home = 0,
        Work = 1,
        School = 2,
        Shop = 3,
        Park = 4,
        Anywhere = 5,
    }

    /// <summary>
    /// Het dagschema van een burger (E07-06): slapen, werk of school, winkelen en vrije tijd. Het schema is een pure functie van burger, dag en
    /// uur, dus een stad hoeft per burger niets te onthouden. Volwassenen met werk werken op werkdagen van 8 tot 17 uur, kinderen gaan op
    /// werkdagen van 8 tot 15 uur naar school; de rest van de dag, en het weekend, is vrije tijd met boodschappen aan het eind van de middag
    /// (gepensioneerden ook 's ochtends). Per burger verschuiven de tijden met een vaste spreiding van <see cref="MaxShift"/> uur
    /// (via <see cref="SeedHash"/>), zodat niet iedereen op hetzelfde uur de weg op gaat. Dag 0 is een maandag.
    /// </summary>
    public static class DailySchedule
    {
        public const int HoursPerDay = 24;
        public const int DaysPerWeek = 7;

        /// <summary>Maximale verschuiving in uren van de tijden per burger.</summary>
        public const int MaxShift = 1;

        // shortcut: vaste uren voor iedereen (plus spreiding); upgrade naar instelbare werktijden zodra beroepen of ploegendiensten bestaan.
        private const int SleepStart = 23;
        private const int SleepEnd = 7;
        private const int DayStart = 8;
        private const int WorkEnd = 17;
        private const int SchoolEnd = 15;
        private const int ShopEnd = 19;
        private const int MorningShopStart = 10;
        private const int MorningShopEnd = 12;
        private const int FirstWeekendDay = 5;

        public static bool IsWeekend(int day) => ((day % DaysPerWeek) + DaysPerWeek) % DaysPerWeek >= FirstWeekendDay;

        /// <summary>De activiteit van <paramref name="citizen"/> op <paramref name="day"/> om <paramref name="hour"/> (0 tot 23); <paramref name="id"/> bepaalt de spreiding.</summary>
        public static Activity ActivityAt(in Citizen citizen, uint id, int day, int hour)
        {
            hour = ((hour % HoursPerDay) + HoursPerDay) % HoursPerDay;
            var shift = (int)(SeedHash.Mix(id) % (MaxShift + 1));
            if (hour >= SleepStart + shift || hour < SleepEnd + shift)
            {
                return Activity.Sleep;
            }

            var group = citizen.Group;
            var busy = !IsWeekend(day) && hour >= DayStart + shift;
            if (busy && group == AgeGroup.Child && hour < SchoolEnd + shift)
            {
                return Activity.School;
            }

            if (busy && group == AgeGroup.Adult && citizen.HasWork && hour < WorkEnd + shift)
            {
                return Activity.Work;
            }

            var evening = group != AgeGroup.Child && hour >= WorkEnd + shift && hour < ShopEnd + shift;
            var morning = group == AgeGroup.Retired && hour >= MorningShopStart + shift && hour < MorningShopEnd + shift;
            return evening || morning ? Activity.Shop : Activity.Leisure;
        }

        public static Destination DestinationFor(in Citizen citizen, Activity activity)
        {
            switch (activity)
            {
                case Activity.Work:
                    return Destination.Work;
                case Activity.School:
                    return Destination.School;
                case Activity.Shop:
                    return Destination.Shop;
                case Activity.Sleep:
                    return citizen.HasHome ? Destination.Home : Destination.Anywhere;
                default:
                    // Vrije tijd: de helft van de huishoudens blijft thuis, de rest gaat naar het park.
                    return citizen.HasHome && (SeedHash.Mix(citizen.Household) & 1UL) == 0UL ? Destination.Home : Destination.Park;
            }
        }
    }
}
