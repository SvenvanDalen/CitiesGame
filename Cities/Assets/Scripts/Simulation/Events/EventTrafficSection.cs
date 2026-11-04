using System;
using System.Collections.Generic;
using Cities.Core.Persistence;

namespace Cities.Simulation.Events
{
    /// <summary>Sectie <c>EVTR</c>: verkeersmaatregelen per evenementenlocatie (E08-17).</summary>
    public sealed class EventTrafficSection : ISaveSection
    {
        public const string SectionId = "EVTR";

        private const int MaxItems = 10000;

        private readonly EventTrafficPlanner _planner;

        public EventTrafficSection(EventTrafficPlanner planner)
        {
            _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            writer.Write(_planner.AllSettings.Count);
            foreach (var pair in _planner.AllSettings)
            {
                writer.Write(pair.Key);
                writer.Write(pair.Value.ExtraBuses);
                writer.Write(pair.Value.ClosedLanes);
                writer.Write(pair.Value.OneWay);
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > MaxItems)
            {
                throw reader.Corrupt($"onmogelijk aantal maatregelen: {count}");
            }

            var items = new List<KeyValuePair<int, EventTrafficSettings>>();
            for (var i = 0; i < count; i++)
            {
                var venueId = reader.ReadInt32();
                var buses = reader.ReadInt32();
                var lanes = reader.ReadInt32();
                var oneWay = reader.ReadBool();
                try
                {
                    items.Add(new KeyValuePair<int, EventTrafficSettings>(venueId, new EventTrafficSettings(buses, lanes, oneWay)));
                }
                catch (ArgumentException e)
                {
                    throw reader.Corrupt(e.Message);
                }
            }

            reader.ExpectEnd();
            return () => _planner.Restore(items);
        }
    }
}
