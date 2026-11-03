using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Ids;
using Cities.Simulation.Vehicles;

namespace Cities.Simulation.Network
{
    /// <summary>
    /// Verkeersregels per arm (E04-13): een arm is een segment bij een knooppunt, dus het verkeer dat daar het kruispunt nadert.
    /// Alleen wat de speler afwijkend instelt wordt bewaard: een eigen voorrangsregel of een verbod in te rijden. Zonder instelling
    /// volgt de regel uit het net (zie <see cref="RightOfWayResolver"/>). De maximumsnelheid per segment staat in <see cref="RoadSegment"/>.
    /// </summary>
    public sealed class TrafficRules
    {
        private readonly SortedDictionary<(ulong Segment, ulong Node), ArmRule> _arms = new SortedDictionary<(ulong Segment, ulong Node), ArmRule>();

        public int Count => _arms.Count;

        /// <summary>De ingestelde voorrangsregel van deze arm, of <c>null</c> als hij uit het net volgt.</summary>
        public RightOfWay? Override(EntityId<SegmentKind> segment, EntityId<NodeKind> node) =>
            _arms.TryGetValue((segment.Packed, node.Packed), out var arm) ? arm.Rule : null;

        /// <summary>Verboden in te rijden: verkeer mag vanaf dit knooppunt dit segment niet op.</summary>
        public bool IsNoEntry(EntityId<SegmentKind> segment, EntityId<NodeKind> node) =>
            _arms.TryGetValue((segment.Packed, node.Packed), out var arm) && arm.NoEntry;

        /// <summary>Zet een eigen regel; <c>null</c> haalt hem weg.</summary>
        public void SetRightOfWay(EntityId<SegmentKind> segment, EntityId<NodeKind> node, RightOfWay? rule)
        {
            if (rule.HasValue && !Enum.IsDefined(typeof(RightOfWay), rule.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(rule), rule, "Onbekende voorrangsregel.");
            }

            Update(segment, node, arm => new ArmRule(rule, arm.NoEntry));
        }

        public void SetNoEntry(EntityId<SegmentKind> segment, EntityId<NodeKind> node, bool noEntry) =>
            Update(segment, node, arm => new ArmRule(arm.Rule, noEntry));

        /// <summary>Wist alle regels van een segment, bijvoorbeeld als het gesloopt is.</summary>
        public void Clear(EntityId<SegmentKind> segment) => RemoveWhere(k => k.Segment == segment.Packed);

        /// <summary>Wist de regels van segmenten waarvoor <paramref name="alive"/> <c>false</c> geeft.</summary>
        public void Prune(Func<EntityId<SegmentKind>, bool> alive) =>
            RemoveWhere(k => !alive(EntityId<SegmentKind>.FromPacked(k.Segment)));

        /// <summary>De arms met een instelling, op volgorde van ID zodat opslaan deterministisch is.</summary>
        internal IEnumerable<(ulong Segment, ulong Node, RightOfWay? Rule, bool NoEntry)> Entries() =>
            _arms.Select(p => (p.Key.Segment, p.Key.Node, p.Value.Rule, p.Value.NoEntry));

        internal void Restore(IEnumerable<(ulong Segment, ulong Node, RightOfWay? Rule, bool NoEntry)> entries)
        {
            _arms.Clear();
            foreach (var e in entries)
            {
                _arms[(e.Segment, e.Node)] = new ArmRule(e.Rule, e.NoEntry);
            }
        }

        private void RemoveWhere(Func<(ulong Segment, ulong Node), bool> match)
        {
            foreach (var key in _arms.Keys.Where(match).ToList())
            {
                _arms.Remove(key);
            }
        }

        private void Update(EntityId<SegmentKind> segment, EntityId<NodeKind> node, Func<ArmRule, ArmRule> change)
        {
            var key = (segment.Packed, node.Packed);
            _arms.TryGetValue(key, out var arm);
            var next = change(arm);
            if (next.Rule.HasValue || next.NoEntry)
            {
                _arms[key] = next;
            }
            else
            {
                _arms.Remove(key);
            }
        }

        private readonly struct ArmRule
        {
            public ArmRule(RightOfWay? rule, bool noEntry)
            {
                Rule = rule;
                NoEntry = noEntry;
            }

            public RightOfWay? Rule { get; }

            public bool NoEntry { get; }
        }
    }
}
