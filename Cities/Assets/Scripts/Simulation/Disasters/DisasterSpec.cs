using System;

namespace Cities.Simulation.Disasters
{
    /// <summary>
    /// Beschrijving van één type ramp (E15-01), puur data zodat elk rampentype (brand, aardbeving, ...) hetzelfde contract volgt:
    /// de kans per dag dat hij vanzelf optreedt, hoeveel dagen waarschuwing er vooraf is, hoe lang het effect duurt, hoe lang de nasleep
    /// duurt en wat dat per dag kost.
    /// </summary>
    public sealed class DisasterSpec
    {
        public DisasterSpec(string name, float chancePerDay, int warningDays, int durationDays, int aftermathDays, long damagePerDay, long aftermathCostPerDay)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Een ramp heeft een naam nodig.", nameof(name));
            }

            if (!(chancePerDay >= 0f && chancePerDay <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(chancePerDay), chancePerDay, "De kans per dag ligt tussen 0 en 1.");
            }

            if (warningDays < 0 || aftermathDays < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(warningDays), "Waarschuwing en nasleep kunnen niet negatief zijn.");
            }

            if (durationDays < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(durationDays), durationDays, "Een ramp duurt minstens een dag.");
            }

            if (damagePerDay < 0 || aftermathCostPerDay < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damagePerDay), "Kosten kunnen niet negatief zijn.");
            }

            Name = name;
            ChancePerDay = chancePerDay;
            WarningDays = warningDays;
            DurationDays = durationDays;
            AftermathDays = aftermathDays;
            DamagePerDay = damagePerDay;
            AftermathCostPerDay = aftermathCostPerDay;
        }

        public string Name { get; }

        public float ChancePerDay { get; }

        public int WarningDays { get; }

        public int DurationDays { get; }

        public int AftermathDays { get; }

        public long DamagePerDay { get; }

        public long AftermathCostPerDay { get; }
    }
}
