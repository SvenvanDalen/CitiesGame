using System;
using System.Collections.Generic;
using Cities.Simulation.Vehicles;
using Unity.Mathematics;

namespace Cities.Gameplay.Overlays
{
    /// <summary>Waar een segment ligt, voor de flow-overlay.</summary>
    public readonly struct SegmentLine
    {
        public SegmentLine(float3 from, float3 to, float width)
        {
            From = from;
            To = to;
            Width = width;
        }

        public float3 From { get; }

        public float3 To { get; }

        public float Width { get; }
    }

    /// <summary>Wat de flow-overlay toont.</summary>
    public enum TrafficFlowMode : byte
    {
        /// <summary>Intensiteit in voertuigen per uur.</summary>
        Flow = 0,

        /// <summary>Bezetting van het segment in procenten; knelpunten staan op 100.</summary>
        Congestion = 1,
    }

    /// <summary>Flow-overlay (E08-13): kleurt segmenten naar intensiteit of drukte uit <see cref="TrafficData"/>.</summary>
    public sealed class TrafficFlowOverlay : IEntityOverlaySource
    {
        /// <summary>Intensiteit in voertuigen per uur waarbij de schaal volledig rood is.</summary>
        public const float MaxFlowPerHour = 1800f;

        private readonly TrafficData _data;
        private readonly Func<ulong, SegmentLine?> _locate;
        private readonly TrafficFlowMode _mode;

        public TrafficFlowOverlay(TrafficData data, Func<ulong, SegmentLine?> locate, TrafficFlowMode mode)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _locate = locate ?? throw new ArgumentNullException(nameof(locate));
            _mode = mode;
            Legend = mode == TrafficFlowMode.Flow
                ? new OverlayLegend("Verkeersintensiteit", "voertuigen/u", 0f, MaxFlowPerHour, OverlayColorScale.Hot, "0")
                : new OverlayLegend("Drukte", "%", 0f, 100f, OverlayColorScale.Hot, "0");
        }

        public string Id => _mode == TrafficFlowMode.Flow ? "traffic-flow" : "traffic-congestion";

        public string Name => Legend.Title;

        public OverlayTarget Target => OverlayTarget.Segment;

        public OverlayLegend Legend { get; }

        public long Version => _data.Version;

        public void Collect(List<OverlayEntityValue> output)
        {
            if (output is null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            foreach (var segment in _data.Segments)
            {
                var line = _locate(segment);
                if (line is null)
                {
                    continue;
                }

                var stats = _data.Get(segment);
                var value = ValueOf(stats);
                output.Add(new OverlayEntityValue(segment, line.Value.From, line.Value.To, line.Value.Width, value));
            }
        }

        private float ValueOf(SegmentTrafficStats stats)
        {
            if (_mode == TrafficFlowMode.Flow)
            {
                return stats.VehiclesPerHour;
            }

            return stats.Bottleneck ? 100f : stats.Occupancy * 100f;
        }
    }
}
