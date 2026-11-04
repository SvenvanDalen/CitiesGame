using System;
using Cities.Core.Persistence;

namespace Cities.Simulation.Disasters
{
    /// <summary>Wat de hulpverlening op één dag deed.</summary>
    public readonly struct ReliefDay
    {
        public ReliefDay(int rescued, int died, int sheltered)
        {
            Rescued = rescued;
            Died = died;
            Sheltered = sheltered;
        }

        public int Rescued { get; }

        /// <summary>Opgesloten mensen die de dag niet haalden omdat er niemand kwam.</summary>
        public int Died { get; }

        /// <summary>Daklozen die er bij kwamen in de noodopvang.</summary>
        public int Sheltered { get; }
    }

    /// <summary>
    /// De nasleep van een ramp die hulp nodig heeft (E15-11): mensen die vastzitten (bereikbaar over de weg of afgesneden), en daklozen.
    /// Wordt dagelijks bijgewerkt door <see cref="ReliefForces.Step"/>.
    /// </summary>
    public sealed class ReliefOperation
    {
        public ReliefOperation(int trapped, int cutOff, int homeless)
        {
            if (trapped < 0 || cutOff < 0 || homeless < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(trapped), "Aantallen kunnen niet negatief zijn.");
            }

            Trapped = trapped;
            CutOff = cutOff;
            Homeless = homeless;
        }

        /// <summary>Vastzittende mensen die teams over de weg kunnen bereiken.</summary>
        public int Trapped { get; internal set; }

        /// <summary>Vastzittende mensen in afgesneden gebied: alleen helikopters komen er.</summary>
        public int CutOff { get; internal set; }

        /// <summary>Daklozen die nog geen noodopvang hebben.</summary>
        public int Homeless { get; internal set; }

        public int Sheltered { get; internal set; }

        public int Rescued { get; internal set; }

        public int Died { get; internal set; }

        public int Days { get; internal set; }

        /// <summary>Is er niemand meer die op hulp wacht? Daklozen zonder bed tellen mee.</summary>
        public bool Finished => Trapped == 0 && CutOff == 0 && Homeless == 0;
    }

    /// <summary>
    /// Hulpverlening na een ramp (E15-11): reddingsteams halen vastzittende mensen over de weg uit puin, helikopters bereiken ook afgesneden gebied,
    /// en noodopvang vangt daklozen op. Wie na een dag nog niet gered is, heeft minder kans (<see cref="DeathShare"/> per dag). Helikopters
    /// gaan eerst naar afgesneden gebied en helpen daarna mee op de weg.
    /// </summary>
    public sealed class ReliefForces
    {
        /// <summary>Mensen die één reddingsteam per dag uit het puin haalt.</summary>
        public const int RescuePerTeamPerDay = 6;

        /// <summary>Mensen die één helikopter per dag redt.</summary>
        public const int RescuePerHelicopterPerDay = 10;

        /// <summary>Deel van de nog niet geredde vastzittenden dat elke dag overlijdt (naar boven afgerond op een geheel aantal).</summary>
        public const float DeathShare = 0.1f;

        private int _teams;
        private int _helicopters;
        private int _shelterBeds;

        public int Teams
        {
            get => _teams;
            set => _teams = NotNegative(value, nameof(Teams));
        }

        public int Helicopters
        {
            get => _helicopters;
            set => _helicopters = NotNegative(value, nameof(Helicopters));
        }

        /// <summary>Bedden in de noodopvang.</summary>
        public int ShelterBeds
        {
            get => _shelterBeds;
            set => _shelterBeds = NotNegative(value, nameof(ShelterBeds));
        }

        /// <summary>De lopende hulpoperatie, of <c>null</c>.</summary>
        public ReliefOperation Operation { get; private set; }

        /// <summary>Begint een hulpoperatie; een lopende wordt vervangen.</summary>
        public ReliefOperation Begin(int trapped, int cutOff, int homeless)
        {
            Operation = new ReliefOperation(trapped, cutOff, homeless);
            return Operation;
        }

        /// <summary>Een dag hulp: redden, opvangen, en wie nog wacht verliest kans. Zonder operatie gebeurt er niets.</summary>
        public ReliefDay Step()
        {
            var op = Operation;
            if (op == null)
            {
                return default;
            }

            var heliLeft = Helicopters * RescuePerHelicopterPerDay;
            var fromCutOff = Math.Min(op.CutOff, heliLeft);
            heliLeft -= fromCutOff;
            var fromRoad = Math.Min(op.Trapped, (Teams * RescuePerTeamPerDay) + heliLeft);

            op.CutOff -= fromCutOff;
            op.Trapped -= fromRoad;
            var rescued = fromCutOff + fromRoad;

            var free = Math.Max(0, ShelterBeds - op.Sheltered);
            var housed = Math.Min(op.Homeless, free);
            op.Homeless -= housed;
            op.Sheltered += housed;

            var diedCutOff = (int)Math.Ceiling(op.CutOff * DeathShare);
            var diedRoad = (int)Math.Ceiling(op.Trapped * DeathShare);
            op.CutOff -= diedCutOff;
            op.Trapped -= diedRoad;
            var died = diedCutOff + diedRoad;

            op.Rescued += rescued;
            op.Died += died;
            op.Days++;
            return new ReliefDay(rescued, died, housed);
        }

        /// <summary>Sluit de operatie af, bijvoorbeeld als de nasleep voorbij is.</summary>
        public void End() => Operation = null;

        internal void Restore(int teams, int helicopters, int beds, ReliefOperation operation)
        {
            _teams = teams;
            _helicopters = helicopters;
            _shelterBeds = beds;
            Operation = operation;
        }

        private static int NotNegative(int value, string name) =>
            value >= 0 ? value : throw new ArgumentOutOfRangeException(name, value, "Het aantal kan niet negatief zijn.");
    }

    /// <summary>Sectie <c>RELF</c>: de hulpdiensten en een eventuele lopende hulpoperatie (E15-11).</summary>
    public sealed class ReliefSection : ISaveSection
    {
        public const string SectionId = "RELF";

        private const int Max = 10_000_000;

        private readonly ReliefForces _forces;

        public ReliefSection(ReliefForces forces)
        {
            _forces = forces ?? throw new ArgumentNullException(nameof(forces));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            writer.Write(_forces.Teams);
            writer.Write(_forces.Helicopters);
            writer.Write(_forces.ShelterBeds);
            var op = _forces.Operation;
            writer.Write(op != null);
            if (op == null)
            {
                return;
            }

            writer.Write(op.Trapped);
            writer.Write(op.CutOff);
            writer.Write(op.Homeless);
            writer.Write(op.Sheltered);
            writer.Write(op.Rescued);
            writer.Write(op.Died);
            writer.Write(op.Days);
        }

        public Action Read(SaveReader reader, int version)
        {
            var teams = reader.ReadInt32();
            var helicopters = reader.ReadInt32();
            var beds = reader.ReadInt32();
            if (!InRange(teams) || !InRange(helicopters) || !InRange(beds))
            {
                throw reader.Corrupt($"onmogelijke hulpdiensten: {teams} teams, {helicopters} helikopters, {beds} bedden");
            }

            ReliefOperation op = null;
            if (reader.ReadBool())
            {
                op = new ReliefOperation(Checked(reader, reader.ReadInt32()), Checked(reader, reader.ReadInt32()), Checked(reader, reader.ReadInt32()))
                {
                    Sheltered = Checked(reader, reader.ReadInt32()),
                    Rescued = Checked(reader, reader.ReadInt32()),
                    Died = Checked(reader, reader.ReadInt32()),
                    Days = Checked(reader, reader.ReadInt32()),
                };
            }

            reader.ExpectEnd();
            return () => _forces.Restore(teams, helicopters, beds, op);
        }

        private static bool InRange(int value) => value >= 0 && value <= Max;

        private static int Checked(SaveReader reader, int value) =>
            InRange(value) ? value : throw reader.Corrupt($"onmogelijk aantal {value} in de hulpoperatie");
    }
}
