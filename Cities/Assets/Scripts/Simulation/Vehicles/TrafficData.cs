using System;
using System.Collections.Generic;

namespace Cities.Simulation.Vehicles
{
    /// <summary>Meetwaarden van één segment in <see cref="TrafficData"/>.</summary>
    public readonly struct SegmentTrafficStats
    {
        public SegmentTrafficStats(int vehiclesLastWindow, float vehiclesPerHour, float meanTravelTime, float meanSpeed, float occupancy, bool bottleneck)
        {
            VehiclesLastWindow = vehiclesLastWindow;
            VehiclesPerHour = vehiclesPerHour;
            MeanTravelTime = meanTravelTime;
            MeanSpeed = meanSpeed;
            Occupancy = occupancy;
            Bottleneck = bottleneck;
        }

        /// <summary>Aantal voertuigen dat het segment in het laatst afgesloten venster verliet.</summary>
        public int VehiclesLastWindow { get; }

        /// <summary>Intensiteit in voertuigen per uur, uit het laatst afgesloten venster.</summary>
        public float VehiclesPerHour { get; }

        /// <summary>Gemiddelde reistijd in seconden over alle doorgangen; <see cref="float.NaN"/> zonder metingen.</summary>
        public float MeanTravelTime { get; }

        /// <summary>Gemiddelde snelheid in m/s over alle doorgangen; <see cref="float.NaN"/> zonder metingen.</summary>
        public float MeanSpeed { get; }

        /// <summary>Laatst gemeten bezetting (0 tot 1).</summary>
        public float Occupancy { get; }

        /// <summary>Of het segment nu een knelpunt is.</summary>
        public bool Bottleneck { get; }
    }

    /// <summary>
    /// Verkeersdata (E08-13): tellers per segment (intensiteit in vensters), gemiddelde reistijd en snelheid, de laatst gemeten bezetting
    /// en knelpuntdetectie. De simulatie meldt doorgangen met <see cref="RecordPass"/> en bezetting met <see cref="Sample"/>; de
    /// flow-overlay en statistiekenpanelen lezen via <see cref="Get"/>. De gegevens zijn afgeleid en worden niet opgeslagen.
    /// </summary>
    public sealed class TrafficData
    {
        /// <summary>Standaardlengte van een telvenster in seconden.</summary>
        public const float DefaultWindow = 300f;

        /// <summary>Bezetting vanaf waar een segment als druk geldt.</summary>
        public const float BottleneckOccupancy = 0.7f;

        /// <summary>Bezetting waaronder een knelpunt weer oplost (hysterese).</summary>
        public const float ClearOccupancy = 0.5f;

        /// <summary>Seconden dat een segment druk moet blijven voordat het een knelpunt wordt.</summary>
        public const float BottleneckDelay = 30f;

        private readonly float _window;
        private readonly Dictionary<ulong, SegmentRecord> _segments = new Dictionary<ulong, SegmentRecord>();
        private float _windowTime;
        private long _passes;
        private double _totalTravelTime;

        public TrafficData(float window = DefaultWindow)
        {
            if (!(window > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(window), window, "Het venster moet langer zijn dan nul.");
            }

            _window = window;
        }

        /// <summary>Verandert bij elke nieuwe meting of venstersluiting; overlays bakken dan opnieuw.</summary>
        public long Version { get; private set; }

        /// <summary>Aantal gemeten doorgangen over alle segmenten.</summary>
        public long TotalPasses => _passes;

        /// <summary>Gemiddelde reistijd per segmentdoorgang over de hele stad in seconden; <see cref="float.NaN"/> zonder metingen.</summary>
        public float NetworkMeanTravelTime => _passes == 0 ? float.NaN : (float)(_totalTravelTime / _passes);

        /// <summary>Segmenten met metingen.</summary>
        public IEnumerable<ulong> Segments => _segments.Keys;

        /// <summary>Een voertuig verliet <paramref name="segment"/> na <paramref name="travelSeconds"/> seconden over <paramref name="distance"/> meter.</summary>
        public void RecordPass(ulong segment, float travelSeconds, float distance)
        {
            if (!(travelSeconds > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(travelSeconds), travelSeconds, "De reistijd moet groter zijn dan nul.");
            }

            if (distance < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(distance), distance, "De afstand mag niet negatief zijn.");
            }

            var record = Record(segment);
            record.Passes++;
            record.WindowCount++;
            record.TravelTime += travelSeconds;
            record.Distance += distance;
            _passes++;
            _totalTravelTime += travelSeconds;
            Version++;
        }

        /// <summary>Meldt de bezetting van <paramref name="segment"/> en laat de knelpuntdetectie <paramref name="dt"/> seconden doorlopen.</summary>
        public void Sample(ulong segment, float occupancy, float dt)
        {
            if (dt < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(dt), dt, "De tijdstap mag niet negatief zijn.");
            }

            var record = Record(segment);
            record.Occupancy = Math.Clamp(occupancy, 0f, 1f);
            var was = record.Bottleneck;
            if (record.Occupancy >= BottleneckOccupancy)
            {
                record.BusyTime += dt;
                record.Bottleneck = record.Bottleneck || record.BusyTime >= BottleneckDelay;
            }
            else if (record.Occupancy < ClearOccupancy)
            {
                record.BusyTime = 0f;
                record.Bottleneck = false;
            }

            if (was != record.Bottleneck)
            {
                Version++;
            }
        }

        /// <summary>Laat de klok doorlopen; een afgelopen venster wordt afgesloten en begint opnieuw.</summary>
        public void Advance(float dt)
        {
            if (dt < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(dt), dt, "De tijdstap mag niet negatief zijn.");
            }

            _windowTime += dt;
            if (_windowTime < _window)
            {
                return;
            }

            _windowTime %= _window;
            foreach (var record in _segments.Values)
            {
                record.LastWindowCount = record.WindowCount;
                record.WindowCount = 0;
            }

            Version++;
        }

        /// <summary>Meetwaarden van een segment; zonder metingen een leeg resultaat.</summary>
        public SegmentTrafficStats Get(ulong segment)
        {
            if (!_segments.TryGetValue(segment, out var r))
            {
                return new SegmentTrafficStats(0, 0f, float.NaN, float.NaN, 0f, false);
            }

            var perHour = r.LastWindowCount * 3600f / _window;
            var travel = r.Passes == 0 ? float.NaN : r.TravelTime / r.Passes;
            var speed = r.Passes == 0 || r.TravelTime <= 0f ? float.NaN : r.Distance / r.TravelTime;
            return new SegmentTrafficStats(r.LastWindowCount, perHour, travel, speed, r.Occupancy, r.Bottleneck);
        }

        /// <summary>Alle segmenten die nu een knelpunt zijn, zonder volgorde.</summary>
        public void CollectBottlenecks(List<ulong> output)
        {
            if (output is null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            foreach (var pair in _segments)
            {
                if (pair.Value.Bottleneck)
                {
                    output.Add(pair.Key);
                }
            }
        }

        /// <summary>Vergeet een segment dat is gesloopt.</summary>
        public void Forget(ulong segment)
        {
            if (_segments.Remove(segment))
            {
                Version++;
            }
        }

        private SegmentRecord Record(ulong segment)
        {
            if (!_segments.TryGetValue(segment, out var record))
            {
                record = new SegmentRecord();
                _segments.Add(segment, record);
            }

            return record;
        }

        private sealed class SegmentRecord
        {
            public int Passes;
            public int WindowCount;
            public int LastWindowCount;
            public float TravelTime;
            public float Distance;
            public float Occupancy;
            public float BusyTime;
            public bool Bottleneck;
        }
    }
}
