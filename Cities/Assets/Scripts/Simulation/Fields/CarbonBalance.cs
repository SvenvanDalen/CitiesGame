using System;
using Cities.Core.Persistence;
using Unity.Mathematics;

namespace Cities.Simulation.Fields
{
    /// <summary>
    /// Totale CO2-uitstoot van de stad met optionele klimaateffecten (E12-10). De bronnen zijn dezelfde als bij luchtvervuiling
    /// (<see cref="PollutionSource"/>); bossen nemen CO2 op. De netto uitstoot telt op tot een opwarming. Staan de klimaateffecten aan, dan
    /// maakt die opwarming hittegolven waarschijnlijker (<see cref="HeatwaveChanceFactor"/>) en de zomer warmer (<see cref="TemperatureShift"/>);
    /// staan ze uit, dan blijft het bij het bijhouden van het getal. De effecten zijn begrensd zodat ze nooit onhoudbaar worden. Waarden zijn schattingen.
    /// </summary>
    public sealed class CarbonBalance
    {
        /// <summary>Netto uitstoot (ton) die een graad opwarming geeft.</summary>
        public const double TonnesPerDegree = 5_000_000d;

        /// <summary>Grootste opwarming die het spel toepast, in graden.</summary>
        public const float MaxWarming = 3f;

        /// <summary>Extra kans op een hittegolf per graad opwarming (aandeel van de basiskans).</summary>
        public const float HeatwaveChancePerDegree = 0.3f;

        private readonly double[] _bySource = new double[Enum.GetValues(typeof(PollutionSource)).Length];

        public bool ClimateEffects { get; set; }

        /// <summary>Uitstoot per bron sinds de start, in ton.</summary>
        public double Emitted(PollutionSource source) => _bySource[(int)source];

        public double TotalEmitted
        {
            get
            {
                double total = 0;
                foreach (var value in _bySource)
                {
                    total += value;
                }

                return total;
            }
        }

        /// <summary>Wat bossen tot nu toe opnamen, in ton.</summary>
        public double Absorbed { get; private set; }

        /// <summary>Uitstoot min opname; kan niet onder nul komen (de stad begint zonder tegoed).</summary>
        public double Net => math.max(0d, TotalEmitted - Absorbed);

        public void Emit(PollutionSource source, double tonnes)
        {
            if (!(tonnes >= 0d))
            {
                throw new ArgumentOutOfRangeException(nameof(tonnes), tonnes, "Uitstoot kan niet negatief zijn.");
            }

            _bySource[(int)source] += tonnes;
        }

        public void Absorb(double tonnes)
        {
            if (!(tonnes >= 0d))
            {
                throw new ArgumentOutOfRangeException(nameof(tonnes), tonnes, "Opname kan niet negatief zijn.");
            }

            Absorbed += tonnes;
        }

        /// <summary>Opwarming in graden uit de netto uitstoot; 0 als de klimaateffecten uit staan.</summary>
        public float Warming => ClimateEffects ? (float)math.min(Net / TonnesPerDegree, MaxWarming) : 0f;

        /// <summary>Vermenigvuldiger op de kans op een hittegolf.</summary>
        public float HeatwaveChanceFactor => 1f + (HeatwaveChancePerDegree * Warming);

        /// <summary>Hoeveel graden warmer de zomer wordt.</summary>
        public float TemperatureShift => Warming;

        internal void Restore(bool climateEffects, double[] bySource, double absorbed)
        {
            ClimateEffects = climateEffects;
            Array.Copy(bySource, _bySource, _bySource.Length);
            Absorbed = absorbed;
        }

        internal int SourceCount => _bySource.Length;
    }

    /// <summary>Sectie <c>CO2B</c>: de CO2-balans van de stad (E12-10).</summary>
    public sealed class CarbonBalanceSection : ISaveSection
    {
        public const string SectionId = "CO2B";

        private readonly CarbonBalance _balance;

        public CarbonBalanceSection(CarbonBalance balance)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            writer.Write(_balance.ClimateEffects);
            writer.Write(_balance.SourceCount);
            for (var i = 0; i < _balance.SourceCount; i++)
            {
                writer.Write(_balance.Emitted((PollutionSource)i));
            }

            writer.Write(_balance.Absorbed);
        }

        public Action Read(SaveReader reader, int version)
        {
            var climate = reader.ReadBool();
            var count = reader.ReadInt32();
            if (count < 0 || count > 64)
            {
                throw reader.Corrupt($"onmogelijk aantal bronnen {count}");
            }

            var values = new double[_balance.SourceCount];
            for (var i = 0; i < count; i++)
            {
                var value = reader.ReadDouble();
                if (i < values.Length)
                {
                    values[i] = value;
                }
            }

            var absorbed = reader.ReadDouble();
            reader.ExpectEnd();
            foreach (var value in values)
            {
                if (!(value >= 0d))
                {
                    throw reader.Corrupt("negatieve uitstoot");
                }
            }

            if (!(absorbed >= 0d))
            {
                throw reader.Corrupt("negatieve opname");
            }

            return () => _balance.Restore(climate, values, absorbed);
        }
    }
}
