using System;
using System.Collections.Generic;
using System.Linq;

namespace Cities.Gameplay.Scenarios
{
    public enum GoalState : byte
    {
        Open,
        Reached,
        Failed,
    }

    /// <summary>Een doel met een voorwaarde op een meetwaarde (E16-06, E22-07).</summary>
    public sealed class ScenarioGoal
    {
        public ScenarioGoal(string title, string metric, double target, bool atLeast = true, bool mustHold = false)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(metric))
            {
                throw new ArgumentException("Een doel heeft een titel en een meetwaarde nodig.");
            }

            Title = title;
            Metric = metric;
            Target = target;
            AtLeast = atLeast;
            MustHold = mustHold;
        }

        public string Title { get; }

        public string Metric { get; }

        public double Target { get; }

        /// <summary>Waar: de waarde moet minstens het doel halen. Onwaar: ze moet er hooguit onder blijven.</summary>
        public bool AtLeast { get; }

        /// <summary>Een beperking: zodra ze wordt geschonden is het scenario verloren.</summary>
        public bool MustHold { get; }

        public bool IsMet(double value) => AtLeast ? value >= Target : value <= Target;
    }

    /// <summary>
    /// Een scenario (E22-07) met doelen, beperkingen en winvoorwaarde. Gewonnen zodra alle doelen zijn gehaald, verloren zodra een
    /// beperking (<see cref="ScenarioGoal.MustHold"/>) wordt geschonden of de tijd om is.
    /// </summary>
    public sealed class Scenario
    {
        private readonly List<ScenarioGoal> _goals;

        public Scenario(string name, IEnumerable<ScenarioGoal> goals, int deadlineDays)
        {
            _goals = (goals ?? throw new ArgumentNullException(nameof(goals))).ToList();
            if (_goals.Count == 0 || deadlineDays < 1)
            {
                throw new ArgumentException("Een scenario heeft doelen en een tijdslimiet nodig.");
            }

            Name = name;
            DeadlineDays = deadlineDays;
        }

        public string Name { get; }

        public int DeadlineDays { get; }

        public IReadOnlyList<ScenarioGoal> Goals => _goals;

        /// <summary>Beoordeelt de stand op een dag met de gemeten waarden per naam.</summary>
        public GoalState Evaluate(int day, IReadOnlyDictionary<string, double> metrics)
        {
            if (metrics == null)
            {
                throw new ArgumentNullException(nameof(metrics));
            }

            var allReached = true;
            foreach (var goal in _goals)
            {
                var known = metrics.TryGetValue(goal.Metric, out var value);
                var met = known && goal.IsMet(value);
                if (goal.MustHold && known && !met)
                {
                    return GoalState.Failed;
                }

                if (!goal.MustHold && !met)
                {
                    allReached = false;
                }
            }

            if (allReached)
            {
                return GoalState.Reached;
            }

            return day > DeadlineDays ? GoalState.Failed : GoalState.Open;
        }
    }
}
