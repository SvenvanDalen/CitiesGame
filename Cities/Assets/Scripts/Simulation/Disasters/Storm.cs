using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Randomness;
using Unity.Mathematics;

namespace Cities.Simulation.Disasters
{
    /// <summary>Een stroomlijn zoals de storm hem ziet: wie en waar.</summary>
    public readonly struct StormPowerLine
    {
        public StormPowerLine(int id, float2 position)
        {
            Id = id;
            Position = position;
        }

        public int Id { get; }

        public float2 Position { get; }
    }

    /// <summary>Wat een dag storm deed: nieuw uitgevallen lijnen en de cellen waar de bliksem insloeg.</summary>
    public sealed class StormOutcome
    {
        internal StormOutcome(IReadOnlyList<int> linesDown, IReadOnlyList<int2> strikes, int fires)
        {
            LinesDown = linesDown;
            Strikes = strikes;
            Fires = fires;
        }

        /// <summary>ID's van lijnen die vandaag uitvielen (stroomuitval).</summary>
        public IReadOnlyList<int> LinesDown { get; }

        /// <summary>Cellen waar vandaag bliksem insloeg.</summary>
        public IReadOnlyList<int2> Strikes { get; }

        /// <summary>Aantal nieuwe branden dat de bliksem startte.</summary>
        public int Fires { get; }
    }

    /// <summary>
    /// Storm met bliksem (E15-08). Een storm ligt als cirkel op de kaart; elke dag vallen lijnen binnen die cirkel uit met een kans die
    /// met de kracht (1 tot 5) groeit, en slaat de bliksem een paar keer in op een deterministische plek binnen de cirkel. Een inslag
    /// op brandbare grond steekt een brand aan (<see cref="FireSpread"/>). Alles volgt uit de seed, dus een herhaling geeft hetzelfde.
    /// </summary>
    public sealed class Storm
    {
        public const int MaxStrength = 5;

        /// <summary>Kans per dag per krachtpunt dat een lijn in de storm uitvalt.</summary>
        public const float OutagePerStrength = 0.15f;

        /// <summary>Bliksemslagen per dag per krachtpunt.</summary>
        public const int StrikesPerStrength = 2;

        /// <summary>Windkracht (0 tot 1) die de storm per krachtpunt meegeeft, voor het vuur.</summary>
        public const float WindPerStrength = 0.2f;

        private readonly List<int> _linesDown = new List<int>();

        public Storm(int id, float2 center, float radius, int strength, ulong seed)
        {
            if (!(radius > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "De storm heeft een straal groter dan nul nodig.");
            }

            if (strength < 1 || strength > MaxStrength)
            {
                throw new ArgumentOutOfRangeException(nameof(strength), strength, "De kracht ligt tussen 1 en 5.");
            }

            Id = id;
            Center = center;
            Radius = radius;
            Strength = strength;
            Seed = seed;
        }

        // shortcut: vaste kosten per dag; de echte schade volgt uit StormOutcome, koppelen aan het Ledger zodra het net in de wereld staat.
        /// <summary>Een storm wordt een dag van tevoren voorspeld en raast twee dagen.</summary>
        public static DisasterSpec Spec { get; } = new DisasterSpec("Storm", 0.001f, 1, 2, 3, 20_000, 5_000);

        public int Id { get; }

        public float2 Center { get; }

        public float Radius { get; }

        public int Strength { get; }

        public ulong Seed { get; }

        /// <summary>Dagen die de storm al raast.</summary>
        public int AgeDays { get; private set; }

        public IReadOnlyList<int> LinesDown => _linesDown;

        /// <summary>De windkracht (0 tot 1) die de storm op een brand blaast.</summary>
        public float WindStrength => math.saturate(WindPerStrength * Strength);

        public bool Contains(float2 point) => math.distance(point, Center) <= Radius;

        /// <summary>
        /// Eén dag storm. <paramref name="fire"/> (mag null zijn) krijgt de inslagen op cellen van <paramref name="cellSize"/> meter;
        /// de oorsprong van de kaart ligt op (0,0). Een lijn die uitviel blijft uit tot hij hersteld wordt (<see cref="Repair"/>).
        /// </summary>
        public StormOutcome Advance(IReadOnlyList<StormPowerLine> lines, FireSpread fire, float cellSize)
        {
            if (lines is null)
            {
                throw new ArgumentNullException(nameof(lines));
            }

            if (!(cellSize > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), cellSize, "Een cel is groter dan nul meter.");
            }

            var day = (ulong)AgeDays;
            AgeDays++;

            var down = new List<int>();
            var chance = OutagePerStrength * Strength;
            foreach (var line in lines.Where(l => Contains(l.Position) && !_linesDown.Contains(l.Id)))
            {
                if (Unit(SeedHash.Combine(Seed, day, (ulong)line.Id)) < chance)
                {
                    down.Add(line.Id);
                }
            }

            _linesDown.AddRange(down);

            var strikes = new List<int2>();
            var fires = 0;
            for (var i = 0; i < StrikesPerStrength * Strength; i++)
            {
                var hash = SeedHash.Combine(Seed, day, 1_000_000UL + (ulong)i);
                var angle = Unit(hash) * math.PI * 2f;
                var distance = math.sqrt(Unit(SeedHash.Mix(hash))) * Radius;
                math.sincos(angle, out var sin, out var cos);
                var point = Center + (new float2(cos, sin) * distance);
                var cell = new int2((int)math.floor(point.x / cellSize), (int)math.floor(point.y / cellSize));
                strikes.Add(cell);
                if (Strike(fire, cell))
                {
                    fires++;
                }
            }

            return new StormOutcome(down, strikes, fires);
        }

        /// <summary>Herstelt een uitgevallen lijn (netbeheer); geeft aan of hij uit stond.</summary>
        public bool Repair(int lineId) => _linesDown.Remove(lineId);

        /// <summary>Zet de status terug uit een save.</summary>
        internal void Restore(int ageDays, IEnumerable<int> linesDown)
        {
            AgeDays = ageDays;
            _linesDown.Clear();
            _linesDown.AddRange(linesDown);
        }

        private static bool Strike(FireSpread fire, int2 cell)
        {
            var onMap = fire != null && cell.x >= 0 && cell.y >= 0 && cell.x < fire.Width && cell.y < fire.Height;
            if (!onMap || fire.IsBurning(cell.x, cell.y) || fire.FuelAt(cell.x, cell.y) <= 0f)
            {
                return false;
            }

            fire.Ignite(cell.x, cell.y);
            return true;
        }

        private static float Unit(ulong hash) => (hash >> 40) / (float)(1 << 24);
    }
}
