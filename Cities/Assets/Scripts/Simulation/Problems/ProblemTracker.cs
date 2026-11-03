using System;
using System.Collections.Generic;

namespace Cities.Simulation.Problems
{
    /// <summary>
    /// Houdt per gebouw bij welke problemen er zijn en welke daarvan een icoon krijgen. Systemen melden elke stap de ruwe
    /// toestand met <see cref="Report"/>; een probleem wordt pas zichtbaar na <see cref="ShowAfter"/> opeenvolgende stappen en
    /// verdwijnt pas na <see cref="HideAfter"/> stappen zonder probleem, zodat een stroomstoot of een kortstondig vol afvalbakje
    /// de iconen niet laat knipperen. Brand is uitgezonderd: die toont meteen.
    /// </summary>
    public sealed class ProblemTracker
    {
        private sealed class Entry
        {
            public readonly int[] Streak = new int[ProblemSet.KindCount];
            public ProblemSet Visible;
        }

        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();

        public ProblemTracker(int showAfter = 3, int hideAfter = 5)
        {
            if (showAfter < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(showAfter), "Tenminste één stap.");
            }

            if (hideAfter < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(hideAfter), "Tenminste één stap.");
            }

            ShowAfter = showAfter;
            HideAfter = hideAfter;
        }

        /// <summary>Aantal stappen met het probleem voordat het icoon verschijnt.</summary>
        public int ShowAfter { get; }

        /// <summary>Aantal stappen zonder het probleem voordat het icoon verdwijnt.</summary>
        public int HideAfter { get; }

        /// <summary>Een zichtbaar probleem is verschenen (true) of verdwenen (false).</summary>
        public event Action<int, BuildingProblem, bool> Changed;

        /// <summary>Meldt de ruwe toestand van één probleem voor één gebouw in deze stap.</summary>
        public void Report(int building, BuildingProblem problem, bool active)
        {
            if (!_entries.TryGetValue(building, out var entry))
            {
                if (!active)
                {
                    return;
                }

                entry = new Entry();
                _entries.Add(building, entry);
            }

            var streak = NextStreak(entry.Streak[(int)problem], active);
            entry.Streak[(int)problem] = streak;
            var shown = entry.Visible.Contains(problem);
            if (!shown && active && (streak >= ShowAfter || problem == BuildingProblem.Fire))
            {
                entry.Visible = entry.Visible.With(problem);
                Changed?.Invoke(building, problem, true);
            }
            else if (shown && !active && -streak >= HideAfter)
            {
                Hide(building, entry, problem);
            }
        }

        /// <summary>Positief = opeenvolgende stappen met probleem, negatief = opeenvolgende stappen zonder.</summary>
        private static int NextStreak(int streak, bool active)
        {
            if (active)
            {
                return streak > 0 ? streak + 1 : 1;
            }

            return streak < 0 ? streak - 1 : -1;
        }

        private void Hide(int building, Entry entry, BuildingProblem problem)
        {
            entry.Visible = entry.Visible.Without(problem);
            Changed?.Invoke(building, problem, false);
            if (entry.Visible.IsEmpty)
            {
                _entries.Remove(building);
            }
        }

        /// <summary>De zichtbare problemen van een gebouw.</summary>
        public ProblemSet VisibleOf(int building) => _entries.TryGetValue(building, out var entry) ? entry.Visible : default;

        /// <summary>Vergeet een gebouw (gesloopt) en meldt dat zijn zichtbare problemen verdwijnen.</summary>
        public void Remove(int building)
        {
            if (!_entries.TryGetValue(building, out var entry))
            {
                return;
            }

            _entries.Remove(building);
            for (var i = 0; i < ProblemSet.KindCount; i++)
            {
                if (entry.Visible.Contains((BuildingProblem)i))
                {
                    Changed?.Invoke(building, (BuildingProblem)i, false);
                }
            }
        }

        /// <summary>Het aantal gebouwen met dit zichtbare probleem.</summary>
        public int Count(BuildingProblem problem)
        {
            var n = 0;
            foreach (var entry in _entries.Values)
            {
                n += entry.Visible.Contains(problem) ? 1 : 0;
            }

            return n;
        }

        /// <summary>Zet alle gebouwen met zichtbare problemen in <paramref name="into"/> (eerst geleegd).</summary>
        public void CollectVisible(List<KeyValuePair<int, ProblemSet>> into)
        {
            into.Clear();
            foreach (var pair in _entries)
            {
                if (!pair.Value.Visible.IsEmpty)
                {
                    into.Add(new KeyValuePair<int, ProblemSet>(pair.Key, pair.Value.Visible));
                }
            }
        }
    }
}
