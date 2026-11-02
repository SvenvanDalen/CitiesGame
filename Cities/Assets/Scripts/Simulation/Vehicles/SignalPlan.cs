using System;
using System.Collections.Generic;
using System.Linq;

namespace Cities.Simulation.Vehicles
{
    /// <summary>
    /// De verkeerslichtenregeling van één kruispunt (E08-04). Aanrijrichtingen zijn in groepen verdeeld die tegelijk groen krijgen; de
    /// groepen komen om de beurt aan de beurt, met na elke groen-fase een korte ontruimingstijd waarin alles rood is. De groentijd per
    /// groep is aan te passen (<see cref="SetGreen"/>) en de regeling kan per kruispunt uit (<see cref="Enabled"/>): dan staat alles open
    /// en regelt voorrang het verkeer. <see cref="IsOpen"/> geeft het inverse van het <c>endBlocked</c>-signaal voor <see cref="LaneTraffic.Step"/>.
    /// </summary>
    public sealed class SignalPlan
    {
        public const float DefaultGreen = 20f;
        public const float DefaultClearance = 4f;

        /// <summary>Aanrijrichtingen binnen deze hoek (graden) van tegenover elkaar horen in dezelfde groep.</summary>
        public const float OppositeTolerance = 30f;

        private readonly float[] _green;
        private int _phase;
        private float _elapsed;

        public SignalPlan(int groups, float clearance = DefaultClearance)
        {
            if (groups < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(groups), groups, "Een regeling heeft minstens één groep nodig.");
            }

            if (!(clearance >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(clearance), clearance, "De ontruimingstijd kan niet negatief zijn.");
            }

            _green = Enumerable.Repeat(DefaultGreen, groups).ToArray();
            Clearance = clearance;
        }

        public int Groups => _green.Length;

        public float Clearance { get; }

        /// <summary>Uit: alle richtingen staan open en voorrangsregels bepalen wie gaat.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>De groep die nu aan de beurt is (ook tijdens de ontruiming erna).</summary>
        public int Phase => _phase;

        public bool Clearing => _elapsed >= _green[_phase];

        public float GreenOf(int group) => _green[CheckGroup(group)];

        public void SetGreen(int group, float seconds)
        {
            if (!(seconds > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "De groentijd moet positief zijn.");
            }

            _green[CheckGroup(group)] = seconds;
        }

        public bool IsOpen(int group) => !Enabled || (CheckGroup(group) == _phase && !Clearing);

        public void Tick(float dt)
        {
            if (!(dt >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(dt), dt, "De tijdstap kan niet negatief zijn.");
            }

            _elapsed += dt;
            while (_elapsed >= _green[_phase] + Clearance)
            {
                _elapsed -= _green[_phase] + Clearance;
                _phase = (_phase + 1) % _green.Length;
            }
        }

        /// <summary>
        /// Automatische fasen: aanrijrichtingen (kompasgraden) die tegenover elkaar liggen delen een groep, de rest krijgt een eigen groep.
        /// Geeft per aanrijrichting het groepsnummer terug; maak de regeling met zoveel groepen als er verschillende nummers zijn.
        /// </summary>
        public static int[] AutoGroups(IReadOnlyList<float> headings)
        {
            if (headings is null)
            {
                throw new ArgumentNullException(nameof(headings));
            }

            var groups = new int[headings.Count];
            var next = 0;
            for (var i = 0; i < groups.Length; i++)
            {
                var partner = Enumerable.Range(0, i).Where(j => IsOpposite(headings[i], headings[j])).DefaultIfEmpty(-1).First();
                groups[i] = partner >= 0 ? groups[partner] : next++;
            }

            return groups;
        }

        private static bool IsOpposite(float a, float b) => Math.Abs((((a - b) % 360f) + 360f) % 360f - 180f) <= OppositeTolerance;

        private int CheckGroup(int group) =>
            group >= 0 && group < _green.Length ? group : throw new ArgumentOutOfRangeException(nameof(group), group, "Onbekende groep.");
    }
}
