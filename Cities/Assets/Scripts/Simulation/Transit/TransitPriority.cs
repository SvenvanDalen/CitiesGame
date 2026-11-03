using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Simulation.Network;
using Cities.Simulation.Vehicles;

namespace Cities.Simulation.Transit
{
    /// <summary>
    /// OV-prioriteit (E09-11): welke lijnen voorrang krijgen en op welke kruispunten. Een lijnvoertuig van een lijn met voorrang rijdt op
    /// een kruispunt met voorrang door in plaats van te wachten (<see cref="Approach.TransitPriority"/>) en vraagt bij een verkeerslicht
    /// groen aan (<see cref="SignalPlan.TryTransitPriority"/>). Busbanen zelf zijn lanes van type <see cref="LaneTypes.Bus"/>
    /// (<see cref="LaneProfiles.Road"/> met <c>busLanes</c>); <see cref="MayUse"/> zegt wie ze mag gebruiken.
    /// </summary>
    public sealed class TransitPriority
    {
        private readonly HashSet<int> _lines = new HashSet<int>();
        private readonly HashSet<int> _intersections = new HashSet<int>();

        /// <summary>Lijnnummers met OV-voorrang, oplopend.</summary>
        public IEnumerable<int> Lines => _lines.OrderBy(l => l);

        /// <summary>Kruispuntnummers waar OV-voorrang geldt, oplopend.</summary>
        public IEnumerable<int> Intersections => _intersections.OrderBy(i => i);

        /// <summary>Aantal keren dat een lijnvoertuig een aanpassing van een verkeerslicht kreeg.</summary>
        public int SignalGrants { get; private set; }

        /// <summary>Aantal aanvragen dat een regeling niet kon of mocht inwilligen.</summary>
        public int SignalDenied { get; private set; }

        public bool HasPriority(int line) => _lines.Contains(line);

        public bool IsPriorityIntersection(int intersection) => _intersections.Contains(intersection);

        public void SetLine(int line, bool enabled) => Toggle(_lines, line, enabled);

        public void SetIntersection(int intersection, bool enabled) => Toggle(_intersections, intersection, enabled);

        /// <summary>Of een voertuig van <paramref name="line"/> op <paramref name="intersection"/> voorrang heeft.</summary>
        public bool Applies(int line, int intersection) => _lines.Contains(line) && _intersections.Contains(intersection);

        /// <summary>Markeert de aanrijding met OV-voorrang als de lijn en het kruispunt daarvoor zijn aangemeld.</summary>
        public Approach Mark(Approach approach, int line, int intersection) =>
            Applies(line, intersection) ? approach.WithTransitPriority() : approach;

        /// <summary>Vraagt groen aan voor <paramref name="group"/> van <paramref name="plan"/>; telt het resultaat.</summary>
        public bool RequestSignal(SignalPlan plan, int line, int intersection, int group)
        {
            if (plan is null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            var granted = Applies(line, intersection) && plan.TryTransitPriority(group);
            if (granted)
            {
                SignalGrants++;
            }
            else
            {
                SignalDenied++;
            }

            return granted;
        }

        /// <summary>Of een voertuig met <paramref name="vehicle"/> deze lane mag gebruiken; een busbaan is alleen voor bussen.</summary>
        public static bool MayUse(LaneSpec lane, LaneTypes vehicle) => (lane.Types & vehicle) != LaneTypes.None;

        /// <summary>Of een lane een busbaan is: alleen voor bussen, niet voor auto's.</summary>
        public static bool IsBusLane(LaneSpec lane) => (lane.Types & LaneTypes.Bus) != 0 && (lane.Types & LaneTypes.Car) == 0;

        public void Restore(IEnumerable<int> lines, IEnumerable<int> intersections)
        {
            _lines.Clear();
            _intersections.Clear();
            _lines.UnionWith(lines);
            _intersections.UnionWith(intersections);
            SignalGrants = 0;
            SignalDenied = 0;
        }

        private static void Toggle(HashSet<int> set, int id, bool enabled)
        {
            if (enabled)
            {
                set.Add(id);
            }
            else
            {
                set.Remove(id);
            }
        }
    }
}
