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

        /// <summary>OV-voorrang (E09-11): een lopende groenfase wordt hoogstens zoveel seconden verlengd.</summary>
        public const float TransitExtension = 10f;

        /// <summary>OV-voorrang: verlengen kan alleen als het groen binnen zoveel seconden afloopt.</summary>
        public const float TransitWindow = 6f;

        /// <summary>OV-voorrang: een lopende groenfase wordt nooit korter dan dit afgekapt.</summary>
        public const float MinGreen = 6f;

        private readonly float[] _green;
        private int _phase;
        private float _elapsed;

        // Verlenging (positief) of inkorting (negatief) van de huidige fase; vervalt bij de volgende fase.
        private float _bonus;

        // Fasewisselingen sinds de laatste OV-ingreep; hoogstens één ingreep per volledige ronde houdt de andere richtingen eerlijk.
        private int _sinceGrant;

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
            _sinceGrant = groups;
        }

        public int Groups => _green.Length;

        public float Clearance { get; }

        /// <summary>Uit: alle richtingen staan open en voorrangsregels bepalen wie gaat.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>De groep die nu aan de beurt is (ook tijdens de ontruiming erna).</summary>
        public int Phase => _phase;

        public bool Clearing => _elapsed >= CurrentGreen;

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

        /// <summary>De groentijd van de lopende fase, inclusief OV-verlenging of -inkorting.</summary>
        public float CurrentGreen => _green[_phase] + _bonus;

        /// <summary>
        /// Een lijnvoertuig meldt zich bij de lus voor <paramref name="group"/> (E09-11). Staat de groep open en loopt het groen bijna af,
        /// dan wordt het groen verlengd; is de groep als volgende aan de beurt, dan wordt het groen van de huidige fase ingekort. Hoogstens
        /// één ingreep per ronde. Geeft terug of de regeling is aangepast.
        /// </summary>
        public bool TryTransitPriority(int group)
        {
            CheckGroup(group);
            if (!Enabled || _sinceGrant < _green.Length)
            {
                return false;
            }

            if (IsOpen(group))
            {
                if (CurrentGreen - _elapsed > TransitWindow)
                {
                    return false;
                }

                _bonus += TransitExtension;
                _sinceGrant = 0;
                return true;
            }

            var cutAt = Math.Max(_elapsed, MinGreen);
            if (Clearing || group != (_phase + 1) % _green.Length || cutAt >= CurrentGreen)
            {
                return false;
            }

            _bonus = cutAt - _green[_phase];
            _sinceGrant = 0;
            return true;
        }

        /// <summary>Seconden tot <paramref name="group"/> weer groen krijgt; 0 als de groep nu open is.</summary>
        public float SecondsUntilOpen(int group)
        {
            if (IsOpen(group))
            {
                return 0f;
            }

            var seconds = CurrentGreen + Clearance - _elapsed;
            for (var q = (_phase + 1) % _green.Length; q != group; q = (q + 1) % _green.Length)
            {
                seconds += _green[q] + Clearance;
            }

            return seconds;
        }

        public void Tick(float dt)
        {
            if (!(dt >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(dt), dt, "De tijdstap kan niet negatief zijn.");
            }

            _elapsed += dt;
            while (_elapsed >= CurrentGreen + Clearance)
            {
                _elapsed -= CurrentGreen + Clearance;
                _phase = (_phase + 1) % _green.Length;
                _bonus = 0f;
                _sinceGrant = Math.Min(_sinceGrant + 1, _green.Length);
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
