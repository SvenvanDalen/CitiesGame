using System;
using Cities.Core.Persistence;

namespace Cities.Simulation.Policies
{
    /// <summary>Sectie <c>PLCY</c>: welk beleid waar geldt. De regels zelf staan in de definities; opgeslagen zijn alleen ID en district.</summary>
    public sealed class PolicySection : ISaveSection
    {
        public const string SectionId = "PLCY";

        private const int MaxPolicies = 100_000;

        private readonly PolicyBook _book;

        public PolicySection(PolicyBook book)
        {
            _book = book ?? throw new ArgumentNullException(nameof(book));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            writer.Write(_book.Count);
            foreach (var (id, district) in _book.Active)
            {
                writer.Write(id);
                writer.Write(district);
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > MaxPolicies)
            {
                throw reader.Corrupt($"onmogelijk aantal beleidsmaatregelen {count}");
            }

            var policies = new (string Id, int District)[count];
            for (var i = 0; i < count; i++)
            {
                policies[i] = (reader.ReadString(), reader.ReadInt32());
            }

            reader.ExpectEnd();
            try
            {
                _book.Check(policies);
            }
            catch (ArgumentException e)
            {
                throw reader.Corrupt(e.Message);
            }

            return () => _book.Restore(policies);
        }
    }
}
