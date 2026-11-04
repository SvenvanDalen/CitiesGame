using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;
using Unity.Mathematics;

namespace Cities.Simulation.Disasters
{
    /// <summary>
    /// Alle dijken van de stad (E15-12). Bij een overstroming (<see cref="Flood"/>) beschermt een dijk het land binnen zijn bereik zolang
    /// hij het peil houdt: de waterdiepte erachter is dan nul. Bezwijkt hij, dan staat het land net zo onder water als zonder dijk.
    /// </summary>
    public sealed class LeveeNetwork
    {
        private readonly List<Levee> _levees = new List<Levee>();

        public int NextId { get; private set; } = 1;

        public IReadOnlyList<Levee> Levees => _levees;

        public Levee Add(float2 start, float2 end, float crestLevel, float reach, float condition = 1f)
        {
            var levee = new Levee(NextId, start, end, crestLevel, reach, condition);
            NextId++;
            _levees.Add(levee);
            return levee;
        }

        public bool Remove(int id) => _levees.RemoveAll(l => l.Id == id) > 0;

        /// <summary>De dijk die een punt beschermt tegen dit peil, of <c>null</c> als er geen is die het houdt.</summary>
        public Levee ProtectorOf(float2 point, float waterLevel) =>
            _levees.Where(l => l.Covers(point) && l.Holds(waterLevel)).OrderBy(l => l.DistanceTo(point)).FirstOrDefault();

        /// <summary>De waterdiepte op een plek, rekening houdend met de dijken: nul als een dijk het peil houdt.</summary>
        public float Depth(float2 point, float waterLevel, float groundHeight) =>
            ProtectorOf(point, waterLevel) != null ? 0f : Flood.Depth(waterLevel, groundHeight);

        /// <summary>Schade van 0 tot 1 aan een gebouw van <paramref name="buildingHeight"/> meter, met de dijken erbij.</summary>
        public float Damage(float2 point, float waterLevel, float groundHeight, float buildingHeight) =>
            Flood.Damage(Depth(point, waterLevel, groundHeight), buildingHeight);

        internal void Restore(int nextId, IEnumerable<Levee> levees)
        {
            NextId = nextId;
            _levees.Clear();
            _levees.AddRange(levees);
        }
    }

    /// <summary>Sectie <c>LEVE</c>: de dijken met hun ligging, kruinhoogte, bereik en toestand (E15-12).</summary>
    public sealed class LeveeSection : ISaveSection
    {
        public const string SectionId = "LEVE";

        private const int MaxLevees = 100_000;

        private readonly LeveeNetwork _network;

        public LeveeSection(LeveeNetwork network)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            writer.Write(_network.NextId);
            writer.Write(_network.Levees.Count);
            foreach (var levee in _network.Levees)
            {
                writer.Write(levee.Id);
                writer.Write(levee.Start.x);
                writer.Write(levee.Start.y);
                writer.Write(levee.End.x);
                writer.Write(levee.End.y);
                writer.Write(levee.CrestLevel);
                writer.Write(levee.Reach);
                writer.Write(levee.Condition);
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var nextId = reader.ReadInt32();
            var count = reader.ReadInt32();
            if (nextId < 1 || count < 0 || count > MaxLevees)
            {
                throw reader.Corrupt($"onmogelijke dijken: volgnummer {nextId}, aantal {count}");
            }

            var levees = new List<Levee>(count);
            for (var i = 0; i < count; i++)
            {
                var id = reader.ReadInt32();
                var start = new float2(reader.ReadSingle(), reader.ReadSingle());
                var end = new float2(reader.ReadSingle(), reader.ReadSingle());
                var crest = reader.ReadSingle();
                var reach = reader.ReadSingle();
                var condition = reader.ReadSingle();
                var valid = id >= 1 && id < nextId && reach > 0f && condition >= 0f && condition <= 1f && math.distance(start, end) > 0f;
                if (!valid)
                {
                    throw reader.Corrupt($"onmogelijke dijk {id}");
                }

                levees.Add(new Levee(id, start, end, crest, reach, condition));
            }

            reader.ExpectEnd();
            return () => _network.Restore(nextId, levees);
        }
    }
}
