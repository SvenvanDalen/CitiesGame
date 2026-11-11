using System;
using System.Collections.Generic;
using Cities.Core.Persistence;
using Unity.Mathematics;

namespace Cities.Simulation.Vehicles
{
    /// <summary>Sectie <c>ACCI</c>: schakelaar, seed en de lopende ongelukken (zonder hulpmelding; die wordt niet bewaard).</summary>
    public sealed class AccidentsSection : ISaveSection
    {
        public const string SectionId = "ACCI";

        private readonly AccidentModel _model;

        public AccidentsSection(AccidentModel model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            writer.Write(_model.Enabled);
            writer.Write(unchecked((long)_model.Seed));
            writer.Write(_model.NextId);
            writer.Write(_model.Active.Count);
            foreach (var a in _model.Active)
            {
                writer.Write(a.Id);
                writer.Write(unchecked((long)a.Segment));
                writer.Write(a.Position.x);
                writer.Write(a.Position.y);
                writer.Write(a.Remaining);
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var enabled = reader.ReadBool();
            var seed = unchecked((ulong)reader.ReadInt64());
            var nextId = reader.ReadInt32();
            var count = reader.ReadInt32();
            if (nextId < 1 || count < 0 || count > AccidentModel.MaxActive)
            {
                throw reader.Corrupt($"onmogelijke ongelukken ({count})");
            }

            var list = new List<Accident>();
            for (var i = 0; i < count; i++)
            {
                var id = reader.ReadInt32();
                var segment = unchecked((ulong)reader.ReadInt64());
                var x = reader.ReadSingle();
                var y = reader.ReadSingle();
                var remaining = reader.ReadSingle();
                if (id < 1 || id >= nextId || !(remaining > 0f))
                {
                    throw reader.Corrupt($"onmogelijk ongeluk {id}");
                }

                list.Add(new Accident(id, segment, new float2(x, y), remaining));
            }

            reader.ExpectEnd();
            return () =>
            {
                _model.Enabled = enabled;
                _model.Seed = seed;
                _model.Restore(nextId, list);
            };
        }
    }
}
