using System;
using Cities.Core.Persistence;

namespace Cities.Simulation.Economy
{
    /// <summary>Sectie <c>CYCL</c>: of de conjunctuur aan staat en de seed ervan (E11-12).</summary>
    public sealed class BusinessCycleSection : ISaveSection
    {
        public const string SectionId = "CYCL";

        private readonly Func<BusinessCycle> _get;
        private readonly Action<BusinessCycle> _set;

        public BusinessCycleSection(Func<BusinessCycle> get, Action<BusinessCycle> set)
        {
            _get = get ?? throw new ArgumentNullException(nameof(get));
            _set = set ?? throw new ArgumentNullException(nameof(set));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            var cycle = _get();
            writer.Write(cycle.Enabled);
            writer.Write(unchecked((long)cycle.Seed));
        }

        public Action Read(SaveReader reader, int version)
        {
            var enabled = reader.ReadBool();
            var seed = unchecked((ulong)reader.ReadInt64());
            reader.ExpectEnd();
            return () => _set(new BusinessCycle(seed, enabled));
        }
    }
}
