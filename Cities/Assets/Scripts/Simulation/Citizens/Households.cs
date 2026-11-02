using System;
using Cities.Core.Ids;

namespace Cities.Simulation.Citizens
{
    /// <summary>Waarom een huishouden zou moeten verhuizen.</summary>
    public enum MoveReason
    {
        None = 0,
        Overcrowded = 1,
        NoWork = 2,
        Homeless = 3,
    }

    /// <summary>
    /// Huishoudens (E07-03). Een huishouden is geen eigen object maar de burgers met hetzelfde <see cref="Citizen.Household"/>-ID; de beller
    /// verzamelt de leden. Leden delen één woning, het huishouden verhuist als geheel (kinderen gaan dus altijd mee met hun ouders) en alleen
    /// een volwassene kan uit huis gaan.
    /// </summary>
    public static class Households
    {
        /// <summary>
        /// Kijkt of het huishouden weg moet: dakloos, meer leden dan <paramref name="homeCapacity"/> bewoners, of volwassenen die werk zoeken
        /// terwijl niemand in het huishouden werkt. Verhuizen is pas zinvol als er elders plek of werk is; dat bepaalt de beller.
        /// </summary>
        public static MoveReason Evaluate(ReadOnlySpan<Citizen> members, int homeCapacity)
        {
            if (members.IsEmpty)
            {
                return MoveReason.None;
            }

            if (!members[0].HasHome)
            {
                return MoveReason.Homeless;
            }

            if (members.Length > homeCapacity)
            {
                return MoveReason.Overcrowded;
            }

            var seeksWork = false;
            foreach (var member in members)
            {
                if (member.HasWork)
                {
                    return MoveReason.None;
                }

                seeksWork |= member.IsJobSeeker;
            }

            return seeksWork ? MoveReason.NoWork : MoveReason.None;
        }

        /// <summary>Laat alle leden in dezelfde woning wonen als één huishouden: een nieuw gezin vormen of het hele gezin verhuizen.</summary>
        public static void Settle(Span<Citizen> members, uint household, EntityId<BuildingKind> home)
        {
            if (household == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(household), "Een huishouden heeft een ID boven nul.");
            }

            for (var i = 0; i < members.Length; i++)
            {
                members[i].Household = household;
                members[i].Home = home;
            }
        }

        /// <summary>Een volwassene gaat in een eigen woning wonen; een kind blijft bij zijn ouders en een dakloze weg kan niet.</summary>
        public static bool TryLeaveParents(ref Citizen citizen, uint newHousehold, EntityId<BuildingKind> newHome)
        {
            if (newHousehold == 0 || !newHome.IsValid || citizen.Group == AgeGroup.Child)
            {
                return false;
            }

            citizen.Household = newHousehold;
            citizen.Home = newHome;
            return true;
        }
    }
}
