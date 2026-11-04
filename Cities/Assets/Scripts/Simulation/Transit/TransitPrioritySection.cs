using System;
using System.Linq;
using Cities.Core.Persistence;

namespace Cities.Simulation.Transit
{
    /// <summary>Sectie <c>TPRI</c>: de lijnen en kruispunten met OV-voorrang. De tellers zijn diagnostiek en worden niet bewaard.</summary>
    public sealed class TransitPrioritySection : ISaveSection
    {
        public const string SectionId = "TPRI";

        private const int MaxEntries = 1_000_000;

        private readonly TransitPriority _priority;

        public TransitPrioritySection(TransitPriority priority)
        {
            _priority = priority ?? throw new ArgumentNullException(nameof(priority));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            WriteIds(writer, _priority.Lines.ToArray());
            WriteIds(writer, _priority.Intersections.ToArray());
        }

        public Action Read(SaveReader reader, int version)
        {
            var lines = ReadIds(reader, "lijnen");
            var intersections = ReadIds(reader, "kruispunten");
            reader.ExpectEnd();
            return () => _priority.Restore(lines, intersections);
        }

        private static void WriteIds(SaveWriter writer, int[] ids)
        {
            writer.Write(ids.Length);
            foreach (var id in ids)
            {
                writer.Write(id);
            }
        }

        private static int[] ReadIds(SaveReader reader, string what)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > MaxEntries)
            {
                throw reader.Corrupt($"onmogelijk aantal {what}: {count}");
            }

            var ids = new int[count];
            for (var i = 0; i < count; i++)
            {
                ids[i] = reader.ReadInt32();
                if (i > 0 && ids[i] <= ids[i - 1])
                {
                    throw reader.Corrupt($"{what} niet oplopend");
                }
            }

            return ids;
        }
    }
}
