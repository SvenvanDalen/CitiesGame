using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;

namespace Cities.Simulation.Milestones
{
    /// <summary>Sectie <c>MILE</c>: de gehaalde bevolkingsmijlpalen (E16-01).</summary>
    public sealed class MilestoneSection : ISaveSection
    {
        public const string SectionId = "MILE";

        private readonly MilestoneTracker _tracker;

        public MilestoneSection(MilestoneTracker tracker)
        {
            _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            writer.Write(_tracker.Achieved.Count);
            foreach (var a in _tracker.Achieved)
            {
                writer.Write(a.Milestone.Id);
                writer.Write(a.Tick);
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > _tracker.Catalog.Count)
            {
                throw reader.Corrupt($"onmogelijk aantal mijlpalen: {count}");
            }

            var loaded = new List<MilestoneAchievement>();
            var seen = new HashSet<string>();
            for (var i = 0; i < count; i++)
            {
                var id = reader.ReadString();
                var tick = reader.ReadInt64();
                var milestone = _tracker.Catalog.FirstOrDefault(m => m.Id == id);
                if (milestone == null || tick < 0 || !seen.Add(id))
                {
                    throw reader.Corrupt($"ongeldige mijlpaal '{id}'");
                }

                loaded.Add(new MilestoneAchievement(milestone, tick));
            }

            reader.ExpectEnd();
            return () => _tracker.Restore(loaded);
        }
    }
}
