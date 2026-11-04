using System;
using Cities.Core.Persistence;

namespace Cities.Simulation.Milestones
{
    /// <summary>Sectie <c>UNLK</c>: de sandbox-schakelaar van het ontgrendelsysteem. De rest volgt uit <c>MILE</c> en de tabel (E16-02).</summary>
    public sealed class UnlockSection : ISaveSection
    {
        public const string SectionId = "UNLK";

        private readonly UnlockService _service;

        public UnlockSection(UnlockService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer) => writer.Write(_service.AllUnlocked);

        public Action Read(SaveReader reader, int version)
        {
            var all = reader.ReadBool();
            reader.ExpectEnd();
            return () => _service.Restore(all);
        }
    }
}
