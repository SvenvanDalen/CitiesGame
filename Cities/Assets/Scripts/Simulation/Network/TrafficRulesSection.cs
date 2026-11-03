using System;
using System.Collections.Generic;
using Cities.Core.Ids;
using Cities.Core.Persistence;
using Cities.Simulation.Vehicles;

namespace Cities.Simulation.Network
{
    /// <summary>
    /// Sectie <c>TRFC</c>: de ingestelde verkeersregels per arm (segment, knooppunt, voorrangsregel en verbod in te rijden).
    /// Regels van segmenten die niet meer bestaan worden bij het opslaan weggelaten.
    /// </summary>
    public sealed class TrafficRulesSection : ISaveSection
    {
        public const string SectionId = "TRFC";

        private const int MaxEntries = 4_000_000;
        private const byte NoRule = 255;

        private readonly TrafficRules _rules;
        private readonly Func<EntityId<SegmentKind>, bool> _segmentExists;

        /// <param name="rules">De regels om te bewaren.</param>
        /// <param name="segmentExists">Of een segment nog in het net zit; <c>null</c> bewaart alles.</param>
        public TrafficRulesSection(TrafficRules rules, Func<EntityId<SegmentKind>, bool> segmentExists = null)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _segmentExists = segmentExists;
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            if (_segmentExists != null)
            {
                _rules.Prune(_segmentExists);
            }

            writer.Write(_rules.Count);
            foreach (var (segment, node, rule, noEntry) in _rules.Entries())
            {
                writer.Write((long)segment);
                writer.Write((long)node);
                writer.Write(rule.HasValue ? (byte)rule.Value : NoRule);
                writer.Write(noEntry);
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > MaxEntries)
            {
                throw reader.Corrupt($"onmogelijk aantal regels ({count})");
            }

            var entries = new List<(ulong Segment, ulong Node, RightOfWay? Rule, bool NoEntry)>(count);
            for (var i = 0; i < count; i++)
            {
                var segment = (ulong)reader.ReadInt64();
                var node = (ulong)reader.ReadInt64();
                var raw = reader.ReadByte();
                var noEntry = reader.ReadBool();
                if (raw != NoRule && !Enum.IsDefined(typeof(RightOfWay), (int)raw))
                {
                    throw reader.Corrupt($"onbekende voorrangsregel {raw}");
                }

                entries.Add((segment, node, raw == NoRule ? (RightOfWay?)null : (RightOfWay)raw, noEntry));
            }

            reader.ExpectEnd();
            return () => _rules.Restore(entries);
        }
    }
}
