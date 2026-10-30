using System.Collections.Generic;
using Cities.Core.Ids;
using Cities.Simulation.Network;
using Unity.Mathematics;

namespace Cities.World.Roads
{
    /// <summary>
    /// Koppelt het <see cref="RoadNetwork"/> aan <see cref="RoadJunctionBuilder"/>: bij een knooppunt met drie of meer
    /// armen houden de wegen eerder op en vult het kruispuntmesh het gat. Het plan volgt steeds uit het netwerk
    /// zelf, dus er is niets om bij te houden of op te slaan.
    /// </summary>
    public static class RoadJunctions
    {
        /// <summary>Een weg mag hoogstens zoveel van zijn lengte kwijt aan één kruispunt.</summary>
        public const float MaxTrimFraction = 0.45f;

        /// <summary>Is het knooppunt een kruispunt (drie of meer armen)?</summary>
        public static bool IsJunction(RoadNetwork network, EntityId<NodeKind> node) =>
            network.Contains(node) && network.GetNode(node).Segments.Length >= RoadJunctionBuilder.MinArms;

        /// <summary>De hartlijn van het segment zonder de stukken die in een kruispunt vallen.</summary>
        public static Bezier3 Trimmed(RoadNetwork network, EntityId<SegmentKind> id)
        {
            var segment = network.GetSegment(id);
            var from = TrimParameter(network, segment.Start, id);
            var to = 1f - TrimParameter(network, segment.End, id);
            return to - from < 0.05f ? segment.Curve : segment.Curve.Slice(from, to);
        }

        /// <summary>Het mesh van het kruispunt, of <c>null</c> als het knooppunt er geen is.</summary>
        public static RoadMeshData Build(RoadNetwork network, EntityId<NodeKind> node)
        {
            var plan = Plan(network, node);
            if (plan == null)
            {
                return null;
            }

            var mouths = new List<JunctionMouth>(plan.Count);
            foreach (var (_, _, mouth) in plan)
            {
                mouths.Add(mouth);
            }

            return RoadJunctionBuilder.Build(mouths);
        }

        private static float TrimParameter(RoadNetwork network, EntityId<NodeKind> node, EntityId<SegmentKind> segment)
        {
            var plan = Plan(network, node);
            if (plan != null)
            {
                foreach (var (id, parameter, _) in plan)
                {
                    if (id == segment)
                    {
                        return parameter;
                    }
                }
            }

            return 0f;
        }

        /// <summary>Per arm het segment, de parameter (vanaf het knooppunt) waar het ophoudt, en de afsnijlijn; <c>null</c> zonder kruispunt.</summary>
        private static List<(EntityId<SegmentKind> Segment, float Parameter, JunctionMouth Mouth)> Plan(RoadNetwork network, EntityId<NodeKind> node)
        {
            if (!IsJunction(network, node))
            {
                return null;
            }

            var center = network.GetNode(node);
            var curves = new List<Bezier3>();
            var arms = new List<JunctionArm>();
            foreach (var id in center.Segments)
            {
                var segment = network.GetSegment(id);
                var curve = segment.Start == node ? segment.Curve : segment.Curve.Reversed;
                var (carriageway, sidewalk) = Widths(network, segment);
                curves.Add(curve);
                arms.Add(new JunctionArm(curve.Direction(0f), carriageway, sidewalk));
            }

            var trims = RoadJunctionBuilder.Trims(arms);
            var plan = new List<(EntityId<SegmentKind>, float, JunctionMouth)>(arms.Count);
            for (var i = 0; i < arms.Count; i++)
            {
                var curve = curves[i];
                var trim = math.min(trims[i], curve.Length() * MaxTrimFraction);
                var t = Find(curve, center.Position, trim);
                plan.Add((center.Segments[i], t, new JunctionMouth(curve.Position(t), curve.Direction(t), arms[i].Carriageway, arms[i].Sidewalk)));
            }

            return plan;
        }

        /// <summary>De parameter waar de curve horizontaal <paramref name="distance"/> van <paramref name="origin"/> ligt (bisectie).</summary>
        private static float Find(Bezier3 curve, float3 origin, float distance)
        {
            var low = 0f;
            var high = 1f;
            for (var i = 0; i < 20; i++)
            {
                var middle = (low + high) * 0.5f;
                if (math.distance(curve.Position(middle).xz, origin.xz) < distance)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }

            return high;
        }

        private static (float Carriageway, float Sidewalk) Widths(RoadNetwork network, RoadSegment segment)
        {
            var carriageway = 0f;
            var sidewalk = 0f;
            foreach (var id in segment.Lanes)
            {
                var spec = network.GetLane(id).Spec;
                if ((spec.Types & LaneTypes.Pedestrian) != 0)
                {
                    sidewalk = math.max(sidewalk, spec.Width);
                }
                else
                {
                    carriageway = math.max(carriageway, math.abs(spec.Offset) + (spec.Width * 0.5f));
                }
            }

            return (math.max(carriageway, 0.5f), sidewalk);
        }
    }
}
