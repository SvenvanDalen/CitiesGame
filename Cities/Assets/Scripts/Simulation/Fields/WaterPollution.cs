using System;
using Cities.Simulation.Utilities;
using Unity.Mathematics;

namespace Cities.Simulation.Fields
{
    public enum WaterDischarge : byte
    {
        Sewage,
        Industry,
    }

    /// <summary>
    /// Watervervuiling langs een rivier (E12-06). De rivier is een rij stukken van boven- naar benedenstrooms; riooluitlaten en industrie
    /// lozen in een stuk, de vervuiling stroomt per stap naar het volgende stuk en verdunt onderweg (<see cref="SelfCleaning"/>). Het niveau
    /// (0 tot 1) bepaalt de kwaliteit van de waterinname (<see cref="WaterQuality.Intake"/>) en de visserij (<see cref="FishingYield"/>).
    /// De waarden zijn schattingen.
    /// </summary>
    public sealed class WaterPollution
    {
        /// <summary>Deel van de vervuiling dat per stuk onderweg verdwijnt.</summary>
        public const float SelfCleaning = 0.25f;

        /// <summary>Concentratie waarbij het niveau 0,5 is.</summary>
        public const float HalfLevel = 30f;

        private readonly float[] _concentration;
        private readonly float[] _pending;

        public WaterPollution(int reaches)
        {
            if (reaches < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(reaches), reaches, "De rivier heeft minstens één stuk nodig.");
            }

            _concentration = new float[reaches];
            _pending = new float[reaches];
        }

        public int Reaches => _concentration.Length;

        /// <summary>Lozing per stap: een fabriek loost meer dan een riooluitlaat.</summary>
        public static float Emission(WaterDischarge source) => source switch
        {
            WaterDischarge.Sewage => 6f,
            WaterDischarge.Industry => 10f,
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Onbekende bron."),
        };

        /// <summary>Loost in het opgegeven stuk; het effect telt mee bij de volgende <see cref="Step"/>.</summary>
        public void Discharge(int reach, WaterDischarge source) => _pending[Check(reach)] += Emission(source);

        /// <summary>Eén stap: de vervuiling stroomt een stuk door (en verdunt), daarna komen de nieuwe lozingen erbij.</summary>
        public void Step()
        {
            for (var i = Reaches - 1; i >= 1; i--)
            {
                _concentration[i] = (_concentration[i - 1] * (1f - SelfCleaning)) + _pending[i];
            }

            _concentration[0] = _pending[0];
            Array.Clear(_pending, 0, _pending.Length);
        }

        /// <summary>Vervuilingsniveau van 0 tot 1 in een stuk; verzadigt, zodat pieken het bereik niet uitrekken.</summary>
        public float Level(int reach)
        {
            var c = _concentration[Check(reach)];
            return c / (c + HalfLevel);
        }

        /// <summary>Kwaliteit van water dat een pomp in dit stuk uit de rivier haalt.</summary>
        public float IntakeQuality(int reach) => WaterQuality.Intake(WaterSource.River, Level(reach));

        /// <summary>Opbrengst van de visserij in dit stuk als deel van de schone opbrengst (1 bij schoon water).</summary>
        public float FishingYield(int reach) => 1f - math.clamp(Level(reach), 0f, 1f);

        private int Check(int reach) =>
            reach >= 0 && reach < Reaches ? reach : throw new ArgumentOutOfRangeException(nameof(reach), reach, "Dat stuk bestaat niet.");
    }
}
