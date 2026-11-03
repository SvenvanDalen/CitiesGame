using System.Linq;
using Cities.Core.Ids;
using Cities.Simulation.Vehicles;

namespace Cities.Simulation.Network
{
    /// <summary>
    /// Bepaalt de voorrangsregel van een arm (E04-13). Een ingestelde regel gaat voor. Anders: de toegang tot een rotonde geeft
    /// voorrang, op een kruispunt met een duidelijk drukkere weg (hogere maximumsnelheid) geven de andere wegen voorrang, en bij
    /// gelijke wegen geldt rechts voor links. Een knooppunt met twee armen heeft niets te regelen.
    /// </summary>
    public static class RightOfWayResolver
    {
        private const float SpeedTolerance = 1e-3f;

        public static RightOfWay Resolve(RoadNetwork network, TrafficRules rules, EntityId<SegmentKind> arm, EntityId<NodeKind> node)
        {
            var set = rules.Override(arm, node);
            if (set.HasValue)
            {
                return set.Value;
            }

            if (Roundabout.IsRingSegment(network, arm))
            {
                return RightOfWay.Priority;
            }

            if (Roundabout.GivesWay(network, arm, node))
            {
                return RightOfWay.Yield;
            }

            var arms = network.GetNode(node).Segments.ToArray();
            if (arms.Length < 3)
            {
                return RightOfWay.Priority;
            }

            var speeds = arms.Select(s => network.GetSegment(s).SpeedLimit).ToArray();
            if (speeds.Max() - speeds.Min() < SpeedTolerance)
            {
                return RightOfWay.RightBeforeLeft;
            }

            return network.GetSegment(arm).SpeedLimit >= speeds.Max() - SpeedTolerance ? RightOfWay.Priority : RightOfWay.Yield;
        }
    }
}
