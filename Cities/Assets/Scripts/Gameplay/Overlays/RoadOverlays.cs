using System;
using Cities.Core.Ids;
using Cities.Simulation.Network;

namespace Cities.Gameplay.Overlays
{
    /// <summary>Verkeersoverlay (E17-04): elk wegsegment gekleurd naar zijn drukte.</summary>
    public static class RoadOverlays
    {
        public const string TrafficId = "traffic";

        /// <summary>Breedte in meters van het lint over een weg.</summary>
        public const float RoadWidth = 6f;

        /// <param name="network">Het wegennet.</param>
        /// <param name="occupancy">Bezetting van een segment van 0 (leeg) tot 1 (bumper aan bumper), bijvoorbeeld uit <c>Congestion.Occupancy</c>.</param>
        /// <param name="version">Teller die oploopt als de drukte veranderde.</param>
        public static IEntityOverlaySource Traffic(RoadNetwork network, Func<EntityId<SegmentKind>, float> occupancy, Func<long> version = null)
        {
            if (network is null)
            {
                throw new ArgumentNullException(nameof(network));
            }

            if (occupancy is null)
            {
                throw new ArgumentNullException(nameof(occupancy));
            }

            var legend = new OverlayLegend("Verkeersdrukte", "% bezet", 0f, 100f, OverlayColorScale.Hot, "0");
            return new EntityOverlaySource(
                TrafficId,
                "Verkeer",
                OverlayTarget.Segment,
                legend,
                output =>
                {
                    foreach (var id in network.Segments())
                    {
                        var load = Math.Min(1f, Math.Max(0f, occupancy(id))) * 100f;
                        CurveRibbons.Add(output, id.Packed, network.GetSegment(id).Curve, RoadWidth, load);
                    }
                },
                version);
        }
    }
}
