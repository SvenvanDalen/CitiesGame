using System.Collections.Generic;
using Cities.Core.Diagnostics;
using Cities.Core.Ids;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Simulation.Network
{
    /// <summary>
    /// Legt voor de testscene een klein wegennet aan rond <see cref="_center"/>, tot de aanleg-tool er is (E04-02):
    /// een kruispunt van tweebaanswegen, een vierbaans laan met fietspaden naar een T-splitsing, een bocht naar een
    /// doodlopend eind en een eenrichtingsweg. Zet de overlay <c>Lanegraaf</c> aan.
    /// </summary>
    public sealed class RoadNetworkSampleBehaviour : MonoBehaviour
    {
        [SerializeField] private RoadNetworkHostBehaviour _host;
        [SerializeField] private Vector3 _center = new Vector3(1000f, 0.05f, 1000f);

        private void Start()
        {
            var network = _host.Network;
            float3 center = _center;
            var twoLane = LaneProfiles.Road(1, bikeLanes: false, sidewalks: true);
            var avenue = LaneProfiles.Road(2, bikeLanes: true, sidewalks: true);
            var oneWay = LaneProfiles.Road(2, bikeLanes: false, sidewalks: true, oneWay: true);

            var crossing = network.AddNode(center);
            var north = network.AddNode(center + new float3(0f, 0f, 160f));
            var east = network.AddNode(center + new float3(140f, 0f, 0f));
            var south = network.AddNode(center + new float3(0f, 0f, -140f));
            var west = network.AddNode(center + new float3(-140f, 0f, 0f));
            Straight(network, crossing, north, avenue, 13.9f);
            Straight(network, crossing, east, twoLane, 13.9f);
            Straight(network, south, crossing, twoLane, 13.9f);
            Straight(network, west, crossing, oneWay, 13.9f);

            // T-splitsing aan het eind van de laan.
            Straight(network, north, network.AddNode(center + new float3(-150f, 0f, 160f)), twoLane, 13.9f);
            Straight(network, north, network.AddNode(center + new float3(150f, 0f, 160f)), twoLane, 13.9f);

            // Bocht van 90° naar een doodlopend eind.
            var bend = network.AddNode(center + new float3(240f, 0f, 100f));
            var from = network.GetNode(east).Position;
            var to = network.GetNode(bend).Position;
            network.AddSegment(east, bend, Bezier3.FromTangents(from, new float3(1f, 0f, 0f), to, new float3(0f, 0f, 1f)), twoLane, 8.3f);

            DebugRegistry.Shared.SetEnabled(LaneGraphOverlay.OverlayName, true);
        }

        private static void Straight(RoadNetwork network, EntityId<NodeKind> from, EntityId<NodeKind> to, IReadOnlyList<LaneSpec> profile, float speedLimit) =>
            network.AddSegment(from, to, Bezier3.Straight(network.GetNode(from).Position, network.GetNode(to).Position), profile, speedLimit);
    }
}
